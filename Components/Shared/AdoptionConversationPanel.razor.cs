using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using PawConnect.Data;
using PawConnect.Services;

namespace PawConnect.Components.Shared;

public partial class AdoptionConversationPanel
{
    [Parameter] public int AdoptionRequestId { get; set; }

    [Parameter] public string BackHref { get; set; } = "/";

    [Parameter] public string BackText { get; set; } = "Back";

    [Inject] private IConversationService ConversationService { get; set; } = default!;

    [Inject] private IMessageService MessageService { get; set; } = default!;

    [Inject] private IMessageAttachmentStorageService AttachmentStorageService { get; set; } = default!;

    [Inject] private IConversationRealtimeNotifier RealtimeNotifier { get; set; } = default!;

    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Inject] private IDialogService DialogService { get; set; } = default!;

    [Inject] private ILogger<AdoptionConversationPanel> Logger { get; set; } = default!;

    private readonly SendMessageModel _sendModel = new();
    private readonly List<MessageDto> _messages = [];
    private readonly HashSet<int> _reactionMessagesInProgress = [];
    private const int MaxMessageLength = Services.MessageService.MaxMessageLength;
    private static readonly TimeSpan TypingThrottleInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TypingAutoStopDelay = TimeSpan.FromSeconds(4);
    private static readonly IReadOnlyList<ReactionButton> AvailableReactions =
    [
        new("Like", "Like"),
        new("Heart", "Heart"),
        new("Thanks", "Thanks"),
        new("Seen", "Seen"),
        new("Important", "Important")
    ];

    private ConversationDto? _conversation;
    private IDisposable? _subscription;
    private IDisposable? _reactionSubscription;
    private IDisposable? _typingSubscription;
    private string? _currentUserId;
    private string _currentUserRoleLabel = "Adopter";
    private string? _error;
    private IBrowserFile? _selectedFile;
    private string? _selectedFileError;
    private int? _editingMessageId;
    private string? _editingBody;
    private string? _editError;
    private bool _isLoading = true;
    private bool _isSending;
    private bool _isEditingMessage;
    private bool _hasSentTypingStart;
    private bool _otherParticipantTyping;
    private string _typingIndicatorText = string.Empty;
    private DateTime _lastTypingStartSentAt = DateTime.MinValue;
    private CancellationTokenSource? _typingAutoStopCts;

    private bool CanSendMessage =>
        !_isSending &&
        string.IsNullOrWhiteSpace(_selectedFileError) &&
        (!string.IsNullOrWhiteSpace(_sendModel.Body) || _selectedFile is not null);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current user could not be found.";
                return;
            }

            _currentUserRoleLabel = authState.User.IsInRole(IdentitySeedData.ShelterRole)
                ? "Shelter"
                : "Adopter";
            _conversation = await ConversationService.GetOrCreateForAdoptionRequestAsync(AdoptionRequestId, _currentUserId);
            _messages.AddRange(await MessageService.GetMessagesAsync(_conversation.Id, _currentUserId));
            await MessageService.MarkConversationAsReadAsync(_conversation.Id, _currentUserId);
            _subscription = RealtimeNotifier.Subscribe(_conversation.Id, OnMessageReceivedAsync);
            _reactionSubscription = RealtimeNotifier.SubscribeReaction(_conversation.Id, OnReactionUpdatedAsync);
            _typingSubscription = RealtimeNotifier.SubscribeTyping(_conversation.Id, OnTypingUpdatedAsync);
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not load adoption conversation for request {AdoptionRequestId}.", AdoptionRequestId);
            _error = "Messages could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ToggleReactionAsync(int messageId, string reactionType)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId) || !_reactionMessagesInProgress.Add(messageId))
        {
            return;
        }

        try
        {
            var update = await MessageService.ToggleReactionAsync(messageId, reactionType, _currentUserId);
            ApplyReactionUpdate(update);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not toggle reaction {ReactionType} for message {MessageId}.", reactionType, messageId);
            Snackbar.Add("Reaction could not be updated right now.", Severity.Error);
        }
        finally
        {
            _reactionMessagesInProgress.Remove(messageId);
        }
    }

    private async Task SendMessageAsync()
    {
        if (_conversation is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            return;
        }

        _isSending = true;
        Stream? attachmentStream = null;

        try
        {
            await StopTypingAsync();
            var attachments = new List<MessageAttachmentUpload>();
            if (_selectedFile is not null)
            {
                attachmentStream = _selectedFile.OpenReadStream(AttachmentStorageService.MaxFileSizeBytes);
                attachments.Add(new MessageAttachmentUpload(
                    _selectedFile.Name,
                    _selectedFile.ContentType,
                    _selectedFile.Size,
                    attachmentStream));
            }

            await MessageService.SendMessageAsync(
                _conversation.Id,
                _currentUserId,
                _sendModel.Body ?? string.Empty,
                attachments);

            _sendModel.Body = string.Empty;
            RemoveSelectedAttachment();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not send message for conversation {ConversationId}.", _conversation.Id);
            Snackbar.Add("Message could not be sent right now.", Severity.Error);
        }
        finally
        {
            attachmentStream?.Dispose();
            _isSending = false;
        }
    }

    private void OnAttachmentSelected(InputFileChangeEventArgs args)
    {
        _selectedFile = null;
        _selectedFileError = null;

        var file = args.File;
        if (file.Size <= 0)
        {
            _selectedFileError = "Attachment cannot be empty.";
            return;
        }

        if (file.Size > AttachmentStorageService.MaxFileSizeBytes)
        {
            _selectedFileError = "Attachment must be 5 MB or smaller.";
            return;
        }

        var extension = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AttachmentStorageService.AllowedExtensions.Contains(extension))
        {
            _selectedFileError = "Allowed attachments are JPG, PNG, WEBP, and PDF files.";
            return;
        }

        _selectedFile = file;
    }

    private void RemoveSelectedAttachment()
    {
        _selectedFile = null;
        _selectedFileError = null;
    }

    private async Task OnComposerBodyChangedAsync(string value)
    {
        _sendModel.Body = value;

        if (string.IsNullOrWhiteSpace(value))
        {
            await StopTypingAsync();
            return;
        }

        await StartTypingAsync();
        ScheduleTypingAutoStop();
    }

    private async Task OnMessageReceivedAsync(MessageDto message)
    {
        var displayMessage = message with
        {
            IsOwnMessage = message.SenderUserId == _currentUserId
        };

        var existingIndex = _messages.FindIndex(existing => existing.Id == message.Id);
        if (existingIndex >= 0)
        {
            _messages[existingIndex] = displayMessage;
        }
        else
        {
            _messages.Add(displayMessage);
        }

        if (existingIndex < 0 &&
            !displayMessage.IsOwnMessage &&
            _conversation is not null &&
            !string.IsNullOrWhiteSpace(_currentUserId))
        {
            await MessageService.MarkConversationAsReadAsync(_conversation.Id, _currentUserId);
        }

        await InvokeAsync(StateHasChanged);
    }

    private void StartEditingMessage(MessageDto message)
    {
        _editingMessageId = message.Id;
        _editingBody = message.Body;
        _editError = null;
    }

    private void CancelEditingMessage()
    {
        _editingMessageId = null;
        _editingBody = null;
        _editError = null;
    }

    private async Task OpenReportMessageDialogAsync(MessageDto message)
    {
        var parameters = new DialogParameters
        {
            ["MessageId"] = message.Id
        };

        var dialog = await DialogService.ShowAsync<ReportMessageDialog>("Report message", parameters);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            Snackbar.Add("Message report submitted for admin review.", Severity.Success);
        }
    }

    private async Task SaveEditedMessageAsync()
    {
        if (_editingMessageId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            return;
        }

        _isEditingMessage = true;
        _editError = null;

        try
        {
            var updatedMessage = await MessageService.EditMessageAsync(
                _editingMessageId.Value,
                _editingBody ?? string.Empty,
                _currentUserId);

            await OnMessageReceivedAsync(updatedMessage);
            CancelEditingMessage();
        }
        catch (InvalidOperationException ex)
        {
            _editError = ex.Message;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not edit message {MessageId}.", _editingMessageId);
            _editError = "Message could not be edited right now.";
        }
        finally
        {
            _isEditingMessage = false;
        }
    }

    private async Task OnReactionUpdatedAsync(MessageReactionUpdateDto update)
    {
        ApplyReactionUpdate(update);
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnTypingUpdatedAsync(MessageTypingIndicatorDto update)
    {
        if (update.UserId == _currentUserId)
        {
            return;
        }

        _otherParticipantTyping = update.IsTyping;
        _typingIndicatorText = update.IsTyping
            ? $"{update.SenderRole} is typing..."
            : string.Empty;

        await InvokeAsync(StateHasChanged);
    }

    private async Task StartTypingAsync()
    {
        if (_conversation is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (_hasSentTypingStart && now - _lastTypingStartSentAt < TypingThrottleInterval)
        {
            return;
        }

        _hasSentTypingStart = true;
        _lastTypingStartSentAt = now;
        await RealtimeNotifier.PublishTypingAsync(new MessageTypingIndicatorDto(
            _conversation.Id,
            _currentUserId,
            _currentUserRoleLabel,
            IsTyping: true));
    }

    private async Task StopTypingAsync()
    {
        if (!_hasSentTypingStart || _conversation is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            return;
        }

        _typingAutoStopCts?.Cancel();
        _typingAutoStopCts?.Dispose();
        _typingAutoStopCts = null;
        _hasSentTypingStart = false;

        await RealtimeNotifier.PublishTypingAsync(new MessageTypingIndicatorDto(
            _conversation.Id,
            _currentUserId,
            _currentUserRoleLabel,
            IsTyping: false));
    }

    private void ScheduleTypingAutoStop()
    {
        _typingAutoStopCts?.Cancel();
        _typingAutoStopCts?.Dispose();
        var cts = new CancellationTokenSource();
        _typingAutoStopCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TypingAutoStopDelay, cts.Token);
                await InvokeAsync(StopTypingAsync);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void ApplyReactionUpdate(MessageReactionUpdateDto update)
    {
        var messageIndex = _messages.FindIndex(message => message.Id == update.MessageId);
        if (messageIndex < 0)
        {
            return;
        }

        var existingMessage = _messages[messageIndex];
        var reactions = update.Reactions
            .Select(summary =>
            {
                var currentUserReacted = existingMessage.Reactions
                    .FirstOrDefault(existing => string.Equals(existing.ReactionType, summary.ReactionType, StringComparison.OrdinalIgnoreCase))
                    ?.ReactedByCurrentUser == true;

                if (string.Equals(update.ChangedByUserId, _currentUserId, StringComparison.Ordinal) &&
                    string.Equals(update.ReactionType, summary.ReactionType, StringComparison.OrdinalIgnoreCase))
                {
                    currentUserReacted = !update.Removed;
                }

                return summary with { ReactedByCurrentUser = currentUserReacted };
            })
            .Where(summary => summary.Count > 0 || summary.ReactedByCurrentUser)
            .ToList();

        _messages[messageIndex] = existingMessage with { Reactions = reactions };
    }

    private static string FormatMessageTime(DateTime createdAt)
    {
        return createdAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
    }

    private static string FormatFileSize(long size)
    {
        return size >= 1024 * 1024
            ? $"{size / 1024d / 1024d:0.#} MB"
            : $"{Math.Max(1, size / 1024d):0.#} KB";
    }

    private string GetSelectedFileIcon()
    {
        return _selectedFile?.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
            ? Icons.Material.Filled.Image
            : Icons.Material.Filled.PictureAsPdf;
    }

    private static MessageReactionSummaryDto? GetReactionSummary(MessageDto message, string reactionType)
    {
        return message.Reactions.FirstOrDefault(reaction =>
            string.Equals(reaction.ReactionType, reactionType, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsReactionBusy(int messageId)
    {
        return _reactionMessagesInProgress.Contains(messageId);
    }

    private static bool CanEditMessage(MessageDto message)
    {
        return message.IsOwnMessage &&
            DateTime.UtcNow - message.CreatedAt.ToUniversalTime() <= Services.MessageService.MessageEditWindow;
    }

    private static bool CanReportMessage(MessageDto message)
    {
        return !message.IsOwnMessage;
    }

    public void Dispose()
    {
        if (_hasSentTypingStart && _conversation is not null && !string.IsNullOrWhiteSpace(_currentUserId))
        {
            _ = RealtimeNotifier.PublishTypingAsync(new MessageTypingIndicatorDto(
                _conversation.Id,
                _currentUserId,
                _currentUserRoleLabel,
                IsTyping: false));
        }

        _typingAutoStopCts?.Cancel();
        _typingAutoStopCts?.Dispose();
        _subscription?.Dispose();
        _reactionSubscription?.Dispose();
        _typingSubscription?.Dispose();
    }

    private sealed class SendMessageModel
    {
        public string? Body { get; set; }
    }

    private sealed record ReactionButton(string ReactionType, string DisplayText);
}
