using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Hubs;

namespace PawConnect.Services;

public class MessageService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    INotificationService notificationService,
    IConversationRealtimeNotifier realtimeNotifier,
    IMessageAttachmentStorageService attachmentStorageService,
    IHubContext<AdoptionChatHub>? hubContext = null) : IMessageService
{
    public const int MaxMessageLength = 2000;
    public static readonly TimeSpan MessageEditWindow = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        int conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await LoadConversationForAccessAsync(context, conversationId, cancellationToken);
        ConversationService.EnsureCanAccessRequest(conversation?.AdoptionRequest, userId);

        var request = conversation!.AdoptionRequest!;
        var messages = await context.Messages
            .Include(message => message.SenderUser)
            .Include(message => message.ReadReceipts)
            .Include(message => message.Attachments)
            .Include(message => message.Reactions)
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return messages
            .Select(message => ToMessageDto(message, request, userId))
            .ToList();
    }

    public async Task<MessageDto> SendMessageAsync(
        int conversationId,
        string senderUserId,
        string body,
        CancellationToken cancellationToken = default)
    {
        return await SendMessageAsync(
            conversationId,
            senderUserId,
            body,
            Array.Empty<MessageAttachmentUpload>(),
            cancellationToken);
    }

    public async Task<MessageDto> SendMessageAsync(
        int conversationId,
        string senderUserId,
        string? body,
        IReadOnlyList<MessageAttachmentUpload> attachments,
        CancellationToken cancellationToken = default)
    {
        attachments ??= Array.Empty<MessageAttachmentUpload>();
        var normalizedBody = NormalizeMessageBody(body, attachments.Count > 0);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await LoadConversationForAccessAsync(context, conversationId, cancellationToken);
        ConversationService.EnsureCanAccessRequest(conversation?.AdoptionRequest, senderUserId);

        var request = conversation!.AdoptionRequest!;
        var now = DateTime.UtcNow;
        var message = new Message
        {
            ConversationId = conversationId,
            SenderUserId = senderUserId,
            Body = normalizedBody,
            CreatedAt = now
        };

        conversation.LastMessageAt = now;
        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            foreach (var attachmentUpload in attachments)
            {
                var storedAttachment = await attachmentStorageService.SaveAsync(
                    conversationId,
                    attachmentUpload.OriginalFileName,
                    attachmentUpload.ContentType,
                    attachmentUpload.Content,
                    attachmentUpload.FileSizeBytes,
                    cancellationToken);

                context.MessageAttachments.Add(new MessageAttachment
                {
                    MessageId = message.Id,
                    OriginalFileName = storedAttachment.OriginalFileName,
                    StoredFileName = storedAttachment.StoredFileName,
                    FilePathOrKey = storedAttachment.FilePathOrKey,
                    ContentType = storedAttachment.ContentType,
                    FileSizeBytes = storedAttachment.FileSizeBytes,
                    UploadedAt = now,
                    UploadedByUserId = senderUserId
                });
            }

            if (attachments.Count > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            context.Messages.Remove(message);
            await context.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        var savedMessage = await context.Messages
            .Include(m => m.SenderUser)
            .Include(m => m.ReadReceipts)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
            .AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, cancellationToken);

        var dto = ToMessageDto(savedMessage, request, senderUserId) with { IsOwnMessage = false };
        await MarkConversationAsReadAsync(conversationId, senderUserId, cancellationToken);
        await NotifyRecipientAsync(request, senderUserId, cancellationToken);
        await realtimeNotifier.PublishAsync(dto);
        if (hubContext is not null)
        {
            await hubContext.Clients
                .Group(AdoptionChatHub.GetConversationGroupName(conversationId))
                .SendAsync("ReceiveMessage", dto, cancellationToken);
        }

        return ToMessageDto(savedMessage, request, senderUserId) with { IsOwnMessage = true };
    }

    public async Task<MessageDto> EditMessageAsync(
        int messageId,
        string newBody,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedBody = NormalizeMessageBody(newBody, hasAttachments: false);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var message = await context.Messages
            .Include(m => m.SenderUser)
            .Include(m => m.ReadReceipts)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
            .Include(m => m.Conversation)
            .ThenInclude(c => c!.AdoptionRequest)
            .ThenInclude(r => r!.Adopter)
            .Include(m => m.Conversation)
            .ThenInclude(c => c!.AdoptionRequest)
            .ThenInclude(r => r!.Dog)
            .ThenInclude(d => d!.Shelter)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        var request = message?.Conversation?.AdoptionRequest;
        ConversationService.EnsureCanAccessRequest(request, currentUserId);

        if (message!.SenderUserId != currentUserId)
        {
            throw new InvalidOperationException("You can only edit your own messages.");
        }

        if (DateTime.UtcNow - message.CreatedAt > MessageEditWindow)
        {
            throw new InvalidOperationException("Messages can only be edited within 15 minutes.");
        }

        message.Body = normalizedBody;
        message.EditedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        var updatedMessage = ToMessageDto(message, request!, currentUserId);
        var broadcastMessage = updatedMessage with { IsOwnMessage = false };
        await realtimeNotifier.PublishAsync(broadcastMessage);
        if (hubContext is not null)
        {
            await hubContext.Clients
                .Group(AdoptionChatHub.GetConversationGroupName(message.ConversationId))
                .SendAsync("ReceiveMessageUpdated", broadcastMessage, cancellationToken);
        }

        return updatedMessage;
    }

    public async Task<MessageAttachmentFile?> GetAttachmentFileAsync(
        int attachmentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await context.MessageAttachments
            .Include(a => a.Message)
            .ThenInclude(m => m!.Conversation)
            .ThenInclude(c => c!.AdoptionRequest)
            .ThenInclude(r => r!.Adopter)
            .Include(a => a.Message)
            .ThenInclude(m => m!.Conversation)
            .ThenInclude(c => c!.AdoptionRequest)
            .ThenInclude(r => r!.Dog)
            .ThenInclude(d => d!.Shelter)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);

        var request = attachment?.Message?.Conversation?.AdoptionRequest;
        ConversationService.EnsureCanAccessRequest(request, userId);

        try
        {
            var stream = await attachmentStorageService.OpenReadAsync(attachment!.FilePathOrKey, cancellationToken);
            return new MessageAttachmentFile(stream, attachment.OriginalFileName, attachment.ContentType);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public async Task<MessageReactionUpdateDto> AddReactionAsync(
        int messageId,
        string reactionType,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var parsedReactionType = ParseReactionType(reactionType);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var message = await LoadMessageForAccessAsync(context, messageId, cancellationToken);
        ConversationService.EnsureCanAccessRequest(message?.Conversation?.AdoptionRequest, currentUserId);

        var exists = await context.MessageReactions.AnyAsync(
            reaction =>
                reaction.MessageId == messageId &&
                reaction.UserId == currentUserId &&
                reaction.ReactionType == parsedReactionType,
            cancellationToken);

        if (!exists)
        {
            context.MessageReactions.Add(new MessageReaction
            {
                MessageId = messageId,
                UserId = currentUserId,
                ReactionType = parsedReactionType,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        return await BuildAndPublishReactionUpdateAsync(
            context,
            message!,
            currentUserId,
            parsedReactionType,
            removed: false,
            cancellationToken);
    }

    public async Task<MessageReactionUpdateDto> RemoveReactionAsync(
        int messageId,
        string reactionType,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var parsedReactionType = ParseReactionType(reactionType);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var message = await LoadMessageForAccessAsync(context, messageId, cancellationToken);
        ConversationService.EnsureCanAccessRequest(message?.Conversation?.AdoptionRequest, currentUserId);

        var existing = await context.MessageReactions.FirstOrDefaultAsync(
            reaction =>
                reaction.MessageId == messageId &&
                reaction.UserId == currentUserId &&
                reaction.ReactionType == parsedReactionType,
            cancellationToken);

        if (existing is not null)
        {
            context.MessageReactions.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        return await BuildAndPublishReactionUpdateAsync(
            context,
            message!,
            currentUserId,
            parsedReactionType,
            removed: true,
            cancellationToken);
    }

    public async Task<MessageReactionUpdateDto> ToggleReactionAsync(
        int messageId,
        string reactionType,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var parsedReactionType = ParseReactionType(reactionType);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var message = await LoadMessageForAccessAsync(context, messageId, cancellationToken);
        ConversationService.EnsureCanAccessRequest(message?.Conversation?.AdoptionRequest, currentUserId);

        var existing = await context.MessageReactions.FirstOrDefaultAsync(
            reaction =>
                reaction.MessageId == messageId &&
                reaction.UserId == currentUserId &&
                reaction.ReactionType == parsedReactionType,
            cancellationToken);

        var removed = existing is not null;
        if (existing is null)
        {
            context.MessageReactions.Add(new MessageReaction
            {
                MessageId = messageId,
                UserId = currentUserId,
                ReactionType = parsedReactionType,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            context.MessageReactions.Remove(existing);
        }

        await context.SaveChangesAsync(cancellationToken);

        return await BuildAndPublishReactionUpdateAsync(
            context,
            message!,
            currentUserId,
            parsedReactionType,
            removed,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MessageReactionSummaryDto>> GetReactionsForMessageAsync(
        int messageId,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var message = await LoadMessageForAccessAsync(context, messageId, cancellationToken);
        ConversationService.EnsureCanAccessRequest(message?.Conversation?.AdoptionRequest, currentUserId);

        return await BuildReactionSummaryAsync(context, messageId, currentUserId, cancellationToken);
    }

    public async Task MarkConversationAsReadAsync(
        int conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await LoadConversationForAccessAsync(context, conversationId, cancellationToken);
        ConversationService.EnsureCanAccessRequest(conversation?.AdoptionRequest, userId);

        var messageIds = await context.Messages
            .Where(message => message.ConversationId == conversationId && message.SenderUserId != userId)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);

        if (messageIds.Count == 0)
        {
            return;
        }

        var existingReceiptIds = await context.MessageReadReceipts
            .Where(receipt => receipt.UserId == userId && messageIds.Contains(receipt.MessageId))
            .Select(receipt => receipt.MessageId)
            .ToListAsync(cancellationToken);

        var existing = existingReceiptIds.ToHashSet();
        var now = DateTime.UtcNow;
        foreach (var messageId in messageIds.Where(messageId => !existing.Contains(messageId)))
        {
            context.MessageReadReceipts.Add(new MessageReadReceipt
            {
                MessageId = messageId,
                UserId = userId,
                ReadAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Conversation?> LoadConversationForAccessAsync(
        ApplicationDbContext context,
        int conversationId,
        CancellationToken cancellationToken)
    {
        return await context.Conversations
            .Include(c => c.AdoptionRequest)
            .ThenInclude(r => r!.Adopter)
            .Include(c => c.AdoptionRequest)
            .ThenInclude(r => r!.Dog)
            .ThenInclude(d => d!.Shelter)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
    }

    private static async Task<Message?> LoadMessageForAccessAsync(
        ApplicationDbContext context,
        int messageId,
        CancellationToken cancellationToken)
    {
        return await context.Messages
            .Include(message => message.Conversation)
            .ThenInclude(conversation => conversation!.AdoptionRequest)
            .ThenInclude(request => request!.Adopter)
            .Include(message => message.Conversation)
            .ThenInclude(conversation => conversation!.AdoptionRequest)
            .ThenInclude(request => request!.Dog)
            .ThenInclude(dog => dog!.Shelter)
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
    }

    private static string NormalizeMessageBody(string? body, bool hasAttachments)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            if (hasAttachments)
            {
                return string.Empty;
            }

            throw new InvalidOperationException("Message cannot be empty.");
        }

        var normalized = body.Trim();
        if (normalized.Length > MaxMessageLength)
        {
            throw new InvalidOperationException($"Message must be {MaxMessageLength} characters or fewer.");
        }

        return normalized;
    }

    private static MessageDto ToMessageDto(Message message, AdoptionRequest request, string currentUserId)
    {
        var senderIsAdopter = message.SenderUserId == request.AdopterId;
        var senderDisplayName = senderIsAdopter
            ? ConversationService.GetAdopterDisplayName(request)
            : ConversationService.GetShelterDisplayName(request);

        return new MessageDto(
            message.Id,
            message.ConversationId,
            senderDisplayName,
            senderIsAdopter ? "Adopter" : "Shelter",
            message.Body,
            message.CreatedAt,
            message.EditedAt,
            message.EditedAt.HasValue,
            message.SenderUserId == currentUserId,
            message.ReadReceipts.Count > 0,
            message.SenderUserId,
            message.Attachments
                .OrderBy(attachment => attachment.Id)
                .Select(ToAttachmentDto)
                .ToList(),
            BuildReactionSummary(message.Reactions, currentUserId)
                .ToList());
    }

    private static MessageAttachmentDto ToAttachmentDto(MessageAttachment attachment)
    {
        return new MessageAttachmentDto(
            attachment.Id,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.FileSizeBytes,
            $"/message-attachments/{attachment.Id}",
            attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<MessageReactionUpdateDto> BuildAndPublishReactionUpdateAsync(
        ApplicationDbContext context,
        Message message,
        string currentUserId,
        MessageReactionType changedReactionType,
        bool removed,
        CancellationToken cancellationToken)
    {
        var update = new MessageReactionUpdateDto(
            message.ConversationId,
            message.Id,
            currentUserId,
            changedReactionType.ToString(),
            removed,
            await BuildReactionSummaryAsync(context, message.Id, currentUserId, cancellationToken));

        await realtimeNotifier.PublishReactionAsync(update);
        if (hubContext is not null)
        {
            await hubContext.Clients
                .Group(AdoptionChatHub.GetConversationGroupName(message.ConversationId))
                .SendAsync("ReceiveReactionUpdate", update, cancellationToken);
        }

        return update;
    }

    private static async Task<IReadOnlyList<MessageReactionSummaryDto>> BuildReactionSummaryAsync(
        ApplicationDbContext context,
        int messageId,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var reactions = await context.MessageReactions
            .Where(reaction => reaction.MessageId == messageId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return BuildReactionSummary(reactions, currentUserId);
    }

    private static List<MessageReactionSummaryDto> BuildReactionSummary(
        IEnumerable<MessageReaction> reactions,
        string currentUserId)
    {
        return reactions
            .GroupBy(reaction => reaction.ReactionType)
            .OrderBy(group => group.Key)
            .Select(group => new MessageReactionSummaryDto(
                group.Key.ToString(),
                GetReactionDisplayText(group.Key),
                group.Count(),
                group.Any(reaction => reaction.UserId == currentUserId)))
            .ToList();
    }

    private static MessageReactionType ParseReactionType(string reactionType)
    {
        if (string.IsNullOrWhiteSpace(reactionType) ||
            !Enum.TryParse<MessageReactionType>(reactionType.Trim(), ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new InvalidOperationException("Unsupported message reaction.");
        }

        return parsed;
    }

    public static string GetReactionDisplayText(MessageReactionType reactionType)
    {
        return reactionType switch
        {
            MessageReactionType.Like => "Like",
            MessageReactionType.Heart => "Heart",
            MessageReactionType.Thanks => "Thanks",
            MessageReactionType.Seen => "Seen",
            MessageReactionType.Important => "Important",
            _ => reactionType.ToString()
        };
    }

    private async Task NotifyRecipientAsync(AdoptionRequest request, string senderUserId, CancellationToken cancellationToken)
    {
        var recipientUserId = GetRecipientUserId(request, senderUserId);
        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            return;
        }

        var dogName = string.IsNullOrWhiteSpace(request.Dog?.Name) ? "the selected dog" : request.Dog.Name;
        var link = request.AdopterId == recipientUserId
            ? $"/my-adoption-requests/{request.Id}/messages"
            : $"/shelter/adoption-requests/{request.Id}/messages";

        await notificationService.CreateNotificationAsync(
            recipientUserId,
            "New adoption message",
            $"You received a new message about {dogName}.",
            NotificationCategory.Adoption,
            NotificationType.Info,
            link,
            "AdoptionRequest",
            request.Id.ToString(),
            TimeSpan.FromSeconds(20));
    }

    private static string? GetRecipientUserId(AdoptionRequest request, string senderUserId)
    {
        if (request.AdopterId == senderUserId)
        {
            return request.Dog?.Shelter?.ApplicationUserId;
        }

        return request.Dog?.Shelter?.ApplicationUserId == senderUserId ? request.AdopterId : null;
    }
}
