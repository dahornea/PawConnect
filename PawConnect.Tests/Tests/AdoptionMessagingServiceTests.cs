using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class AdoptionMessagingServiceTests
{
    [Fact]
    public async Task GetOrCreateForAdoptionRequestAsync_AllowsOnlyRequestAdopter()
    {
        var test = await CreateMessagingTestContextAsync();

        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);

        Assert.Equal(test.RequestId, conversation.AdoptionRequestId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.ConversationService.GetOrCreateForAdoptionRequestAsync(test.RequestId, TestDbContextFactory.SecondAdopterId));
    }

    [Fact]
    public async Task GetOrCreateForAdoptionRequestAsync_AllowsOnlyOwningShelter()
    {
        var test = await CreateMessagingTestContextAsync();

        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.ShelterUserId);

        Assert.Equal(test.RequestId, conversation.AdoptionRequestId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.ConversationService.GetOrCreateForAdoptionRequestAsync(test.RequestId, TestDbContextFactory.OtherShelterUserId));
    }

    [Fact]
    public async Task SendMessageAsync_PersistsMessageAndCreatesRecipientNotification()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);

        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "  Could we discuss the visit time?  ");

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        var savedMessage = await verificationContext.Messages.SingleAsync();
        var notification = await verificationContext.Notifications.SingleAsync();

        Assert.Equal("Could we discuss the visit time?", message.Body);
        Assert.Equal("Could we discuss the visit time?", savedMessage.Body);
        Assert.Equal(TestDbContextFactory.ShelterUserId, notification.UserId);
        Assert.Equal(NotificationCategory.Adoption, notification.Category);
        Assert.Contains(test.RequestId.ToString(), notification.Link);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessageAsync_RejectsEmptyMessages(string body)
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.SendMessageAsync(conversation.Id, TestDbContextFactory.AdopterId, body));

        Assert.Equal("Message cannot be empty.", exception.Message);
    }

    [Fact]
    public async Task SendMessageAsync_RejectsMessagesOverLimit()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var longMessage = new string('a', MessageService.MaxMessageLength + 1);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.SendMessageAsync(conversation.Id, TestDbContextFactory.AdopterId, longMessage));

        Assert.Equal($"Message must be {MessageService.MaxMessageLength} characters or fewer.", exception.Message);
    }

    [Fact]
    public async Task EditMessageAsync_AllowsSenderToEditOwnRecentMessage()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Original message");

        var edited = await test.MessageService.EditMessageAsync(
            message.Id,
            "Updated message",
            TestDbContextFactory.AdopterId);

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        var savedMessage = await verificationContext.Messages.SingleAsync();

        Assert.Equal("Updated message", edited.Body);
        Assert.True(edited.IsEdited);
        Assert.NotNull(edited.EditedAt);
        Assert.Equal("Updated message", savedMessage.Body);
        Assert.NotNull(savedMessage.EditedAt);
        Assert.Equal(conversation.Id, edited.ConversationId);
    }

    [Fact]
    public async Task EditMessageAsync_RejectsEditAfterTimeWindow()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Original message");

        await using (var context = await test.ContextFactory.CreateDbContextAsync())
        {
            var savedMessage = await context.Messages.SingleAsync(m => m.Id == message.Id);
            savedMessage.CreatedAt = DateTime.UtcNow - MessageService.MessageEditWindow - TimeSpan.FromMinutes(1);
            await context.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.EditMessageAsync(message.Id, "Too late", TestDbContextFactory.AdopterId));

        Assert.Equal("Messages can only be edited within 15 minutes.", exception.Message);
    }

    [Fact]
    public async Task EditMessageAsync_BlocksOtherConversationParticipant()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Adopter message");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.EditMessageAsync(message.Id, "Shelter edit", TestDbContextFactory.ShelterUserId));

        Assert.Equal("You can only edit your own messages.", exception.Message);
    }

    [Fact]
    public async Task EditMessageAsync_BlocksUserWithoutConversationAccess()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Adopter message");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.EditMessageAsync(message.Id, "Other user edit", TestDbContextFactory.SecondAdopterId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EditMessageAsync_RejectsEmptyBody(string newBody)
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Original message");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.EditMessageAsync(message.Id, newBody, TestDbContextFactory.AdopterId));

        Assert.Equal("Message cannot be empty.", exception.Message);
    }

    [Fact]
    public async Task EditMessageAsync_RejectsBodyOverLimit()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Original message");
        var longMessage = new string('a', MessageService.MaxMessageLength + 1);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.EditMessageAsync(message.Id, longMessage, TestDbContextFactory.AdopterId));

        Assert.Equal($"Message must be {MessageService.MaxMessageLength} characters or fewer.", exception.Message);
    }

    [Fact]
    public async Task SendMessageAsync_AllowsAttachmentOnlyMessageAndSavesMetadata()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        await using var stream = CreateContentStream("image bytes");

        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "   ",
            [new MessageAttachmentUpload("visit-photo.jpg", "image/jpeg", stream.Length, stream)]);

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        var attachment = await verificationContext.MessageAttachments.SingleAsync();

        Assert.Empty(message.Body);
        Assert.Single(message.Attachments);
        Assert.Equal("visit-photo.jpg", attachment.OriginalFileName);
        Assert.Equal("image/jpeg", attachment.ContentType);
        Assert.Equal(TestDbContextFactory.AdopterId, attachment.UploadedByUserId);
        Assert.False(Path.IsPathRooted(attachment.FilePathOrKey));
        Assert.StartsWith($"uploads/messages/{conversation.Id}/", attachment.FilePathOrKey);
    }

    [Theory]
    [InlineData("script.exe", "application/octet-stream")]
    [InlineData("../visit-photo.jpg", "image/jpeg")]
    [InlineData("visit-photo.jpg", "application/pdf")]
    public async Task SendMessageAsync_RejectsUnsafeOrUnsupportedAttachments(string fileName, string contentType)
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        await using var stream = CreateContentStream("file bytes");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.SendMessageAsync(
                conversation.Id,
                TestDbContextFactory.AdopterId,
                "See attached.",
                [new MessageAttachmentUpload(fileName, contentType, stream.Length, stream)]));

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await verificationContext.Messages.ToListAsync());
        Assert.Empty(await verificationContext.MessageAttachments.ToListAsync());
    }

    [Fact]
    public async Task SendMessageAsync_RejectsOversizedAttachment()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        await using var stream = CreateContentStream("small stream");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.SendMessageAsync(
                conversation.Id,
                TestDbContextFactory.AdopterId,
                "See attached.",
                [new MessageAttachmentUpload(
                    "visit-photo.png",
                    "image/png",
                    LocalMessageAttachmentStorageService.DefaultMaxFileSizeBytes + 1,
                    stream)]));
    }

    [Fact]
    public async Task SendMessageAsync_BlocksAttachmentUploadWithoutConversationAccess()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        await using var stream = CreateContentStream("image bytes");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.SendMessageAsync(
                conversation.Id,
                TestDbContextFactory.SecondAdopterId,
                "See attached.",
                [new MessageAttachmentUpload("visit-photo.webp", "image/webp", stream.Length, stream)]));

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await verificationContext.Messages.ToListAsync());
        Assert.Empty(await verificationContext.MessageAttachments.ToListAsync());
    }

    [Fact]
    public async Task GetAttachmentFileAsync_AllowsOnlyConversationParticipants()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        await using var stream = CreateContentStream("pdf bytes");
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Please review this.",
            [new MessageAttachmentUpload("visit-note.pdf", "application/pdf", stream.Length, stream)]);
        var attachmentId = message.Attachments.Single().Id;

        var allowed = await test.MessageService.GetAttachmentFileAsync(attachmentId, TestDbContextFactory.ShelterUserId);
        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.GetAttachmentFileAsync(attachmentId, TestDbContextFactory.SecondAdopterId));

        Assert.NotNull(allowed);
        Assert.Equal("visit-note.pdf", allowed.OriginalFileName);
        Assert.True(blocked.Message.Contains("cannot access", StringComparison.OrdinalIgnoreCase));
        await allowed.Content.DisposeAsync();
    }

    [Fact]
    public async Task AddReactionAsync_AllowsConversationParticipantAndSavesReaction()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Does this time work?");

        var update = await test.MessageService.AddReactionAsync(
            message.Id,
            MessageReactionType.Like.ToString(),
            TestDbContextFactory.ShelterUserId);

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        var reaction = await verificationContext.MessageReactions.SingleAsync();

        Assert.Equal(message.Id, reaction.MessageId);
        Assert.Equal(TestDbContextFactory.ShelterUserId, reaction.UserId);
        Assert.Equal(MessageReactionType.Like, reaction.ReactionType);
        Assert.Single(update.Reactions);
        Assert.True(update.Reactions.Single().ReactedByCurrentUser);
    }

    [Fact]
    public async Task AddReactionAsync_BlocksUsersWithoutConversationAccess()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Hello shelter.");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.MessageService.AddReactionAsync(message.Id, "Heart", TestDbContextFactory.SecondAdopterId));

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await verificationContext.MessageReactions.ToListAsync());
    }

    [Fact]
    public async Task AddReactionAsync_DoesNotCreateDuplicateReaction()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Thank you.");

        await test.MessageService.AddReactionAsync(message.Id, "Thanks", TestDbContextFactory.ShelterUserId);
        await test.MessageService.AddReactionAsync(message.Id, "Thanks", TestDbContextFactory.ShelterUserId);

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await verificationContext.MessageReactions.CountAsync());
    }

    [Fact]
    public async Task ToggleReactionAsync_RemovesExistingReaction()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Please mark this as important.");

        var added = await test.MessageService.ToggleReactionAsync(message.Id, "Important", TestDbContextFactory.ShelterUserId);
        var removed = await test.MessageService.ToggleReactionAsync(message.Id, "Important", TestDbContextFactory.ShelterUserId);

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        Assert.False(added.Removed);
        Assert.True(removed.Removed);
        Assert.Empty(await verificationContext.MessageReactions.ToListAsync());
    }

    [Fact]
    public async Task GetReactionsForMessageAsync_ReturnsCountsAndCurrentUserState()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Visit confirmed.");

        await test.MessageService.AddReactionAsync(message.Id, "Seen", TestDbContextFactory.AdopterId);
        await test.MessageService.AddReactionAsync(message.Id, "Seen", TestDbContextFactory.ShelterUserId);

        var adopterSummary = await test.MessageService.GetReactionsForMessageAsync(
            message.Id,
            TestDbContextFactory.AdopterId);
        var shelterSummary = await test.MessageService.GetReactionsForMessageAsync(
            message.Id,
            TestDbContextFactory.ShelterUserId);

        Assert.Equal(2, adopterSummary.Single().Count);
        Assert.True(adopterSummary.Single().ReactedByCurrentUser);
        Assert.True(shelterSummary.Single().ReactedByCurrentUser);
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_CreatesReadReceiptForRecipient()
    {
        var test = await CreateMessagingTestContextAsync();
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        await test.MessageService.SendMessageAsync(conversation.Id, TestDbContextFactory.AdopterId, "Hello shelter.");

        await test.MessageService.MarkConversationAsReadAsync(conversation.Id, TestDbContextFactory.ShelterUserId);

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        var receipt = await verificationContext.MessageReadReceipts.SingleAsync();
        Assert.Equal(TestDbContextFactory.ShelterUserId, receipt.UserId);
    }

    [Fact]
    public async Task ReportMessageAsync_AllowsParticipantToReportOtherParticipantMessage()
    {
        var test = await CreateMessagingTestContextAsync();
        var reportService = new MessageReportService(test.ContextFactory);
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.ShelterUserId,
            "Please continue the discussion through this suspicious link.");

        var report = await reportService.ReportMessageAsync(
            message.Id,
            "Spam or scam",
            "Asked to use an external link.",
            TestDbContextFactory.AdopterId);

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        var savedReport = await verificationContext.MessageReports.SingleAsync();

        Assert.Equal(message.Id, report.MessageId);
        Assert.Equal(TestDbContextFactory.AdopterId, savedReport.ReporterUserId);
        Assert.Equal(MessageReportStatus.Pending, savedReport.Status);
        Assert.Equal("Spam or scam", savedReport.Reason);
        Assert.True(savedReport.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task ReportMessageAsync_BlocksUserWithoutConversationAccess()
    {
        var test = await CreateMessagingTestContextAsync();
        var reportService = new MessageReportService(test.ContextFactory);
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.ShelterUserId,
            "Conversation-only message.");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reportService.ReportMessageAsync(message.Id, "Other", "Not part of this conversation.", TestDbContextFactory.SecondAdopterId));

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await verificationContext.MessageReports.ToListAsync());
    }

    [Fact]
    public async Task ReportMessageAsync_BlocksSenderFromReportingOwnMessage()
    {
        var test = await CreateMessagingTestContextAsync();
        var reportService = new MessageReportService(test.ContextFactory);
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.AdopterId,
            "Own message.");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reportService.ReportMessageAsync(message.Id, "Other", "Trying to report myself.", TestDbContextFactory.AdopterId));

        Assert.Equal("You cannot report your own message.", exception.Message);
    }

    [Fact]
    public async Task ReportMessageAsync_BlocksDuplicateReportBySameUser()
    {
        var test = await CreateMessagingTestContextAsync();
        var reportService = new MessageReportService(test.ContextFactory);
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.ShelterUserId,
            "Message to report once.");

        await reportService.ReportMessageAsync(message.Id, "Privacy concern", null, TestDbContextFactory.AdopterId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reportService.ReportMessageAsync(message.Id, "Privacy concern", null, TestDbContextFactory.AdopterId));

        Assert.Equal("You have already reported this message.", exception.Message);
    }

    [Fact]
    public async Task GetAdminReportsAsync_AllowsAdminToListPendingReports()
    {
        var test = await CreateMessagingTestContextAsync();
        var reportService = new MessageReportService(test.ContextFactory);
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.ShelterUserId,
            "Reported message.");

        await reportService.ReportMessageAsync(message.Id, "Harassment or pressure", "Too pushy.", TestDbContextFactory.AdopterId);

        var reports = await reportService.GetAdminReportsAsync(
            new MessageReportFilter(MessageReportStatus.Pending),
            TestDbContextFactory.AdminId);

        var report = Assert.Single(reports);
        Assert.Equal("Harassment or pressure", report.Reason);
        Assert.Equal("Messaging Dog", report.DogName);
        Assert.Equal(TestDbContextFactory.AdopterId, await GetSavedReporterIdAsync(test.ContextFactory));
    }

    [Fact]
    public async Task GetAdminReportsAsync_BlocksNonAdminUsers()
    {
        var test = await CreateMessagingTestContextAsync();
        var reportService = new MessageReportService(test.ContextFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reportService.GetAdminReportsAsync(new MessageReportFilter(), TestDbContextFactory.ShelterUserId));
    }

    [Fact]
    public async Task ReviewReportAsync_AllowsAdminToUpdateStatusAndNote()
    {
        var test = await CreateMessagingTestContextAsync();
        var reportService = new MessageReportService(test.ContextFactory);
        var conversation = await test.ConversationService.GetOrCreateForAdoptionRequestAsync(
            test.RequestId,
            TestDbContextFactory.AdopterId);
        var message = await test.MessageService.SendMessageAsync(
            conversation.Id,
            TestDbContextFactory.ShelterUserId,
            "Reported message.");
        var report = await reportService.ReportMessageAsync(message.Id, "Other", "Needs review.", TestDbContextFactory.AdopterId);

        var updated = await reportService.ReviewReportAsync(
            report.Id,
            MessageReportStatus.ActionTaken,
            "Moderator contacted the shelter.",
            TestDbContextFactory.AdminId);

        await using var verificationContext = await test.ContextFactory.CreateDbContextAsync();
        var savedReport = await verificationContext.MessageReports.SingleAsync();

        Assert.Equal(MessageReportStatus.ActionTaken, updated.Status);
        Assert.Equal("Moderator contacted the shelter.", savedReport.AdminNote);
        Assert.Equal(TestDbContextFactory.AdminId, savedReport.ReviewedByAdminId);
        Assert.NotNull(savedReport.ReviewedAt);
    }

    private static async Task<MessagingTestContext> CreateMessagingTestContextAsync()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var dog = TestDbContextFactory.CreateDog("Messaging Dog", DogStatus.Available);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var request = new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            ReasonForAdoption = "I would like to adopt this dog.",
            Status = AdoptionRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.AdoptionRequests.Add(request);
        await context.SaveChangesAsync();

        var contextFactory = TestDbContextFactory.CreateContextFactory(databaseName);
        var notificationService = new NotificationService(contextFactory, NullLogger<NotificationService>.Instance);
        var realtimeNotifier = new ConversationRealtimeNotifier();
        var attachmentStorageService = CreateAttachmentStorage(databaseName);

        return new MessagingTestContext(
            databaseName,
            request.Id,
            contextFactory,
            new ConversationService(contextFactory),
            new MessageService(contextFactory, notificationService, realtimeNotifier, attachmentStorageService));
    }

    private static LocalMessageAttachmentStorageService CreateAttachmentStorage(string databaseName)
    {
        var root = Path.Combine(Path.GetTempPath(), "PawConnectTests", databaseName);
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        return new LocalMessageAttachmentStorageService(new TestWebHostEnvironment(root));
    }

    private static MemoryStream CreateContentStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private static async Task<string?> GetSavedReporterIdAsync(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.MessageReports.Select(report => report.ReporterUserId).SingleAsync();
    }

    private sealed record MessagingTestContext(
        string DatabaseName,
        int RequestId,
        IDbContextFactory<ApplicationDbContext> ContextFactory,
        IConversationService ConversationService,
        IMessageService MessageService);

    private sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PawConnect.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = root;

        public string EnvironmentName { get; set; } = "Development";

        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
