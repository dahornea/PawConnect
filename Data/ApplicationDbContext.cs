using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PawConnect.Entities;

namespace PawConnect.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Shelter> Shelters => Set<Shelter>();

    public DbSet<Dog> Dogs => Set<Dog>();

    public DbSet<DogBreed> DogBreeds => Set<DogBreed>();

    public DbSet<DogImage> DogImages => Set<DogImage>();

    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();

    public DbSet<AdoptionRequest> AdoptionRequests => Set<AdoptionRequest>();

    public DbSet<FavoriteDog> FavoriteDogs => Set<FavoriteDog>();

    public DbSet<ResourceStock> ResourceStocks => Set<ResourceStock>();

    public DbSet<ResourceCategory> ResourceCategories => Set<ResourceCategory>();

    public DbSet<FoodType> FoodTypes => Set<FoodType>();

    public DbSet<AdopterProfile> AdopterProfiles => Set<AdopterProfile>();

    public DbSet<DogStatusHistory> DogStatusHistories => Set<DogStatusHistory>();

    public DbSet<RecentlyViewedDog> RecentlyViewedDogs => Set<RecentlyViewedDog>();

    public DbSet<SavedDogSearch> SavedDogSearches => Set<SavedDogSearch>();

    public DbSet<SavedSearchMatch> SavedSearchMatches => Set<SavedSearchMatch>();

    public DbSet<ShelterRegistrationRequest> ShelterRegistrationRequests => Set<ShelterRegistrationRequest>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<NotificationDeliveryLog> NotificationDeliveryLogs => Set<NotificationDeliveryLog>();

    public DbSet<NotificationOutboxMessage> NotificationOutboxMessages => Set<NotificationOutboxMessage>();

    public DbSet<ReportHistory> ReportHistories => Set<ReportHistory>();

    public DbSet<DogSearchEmbedding> DogSearchEmbeddings => Set<DogSearchEmbedding>();

    public DbSet<CopilotSession> CopilotSessions => Set<CopilotSession>();

    public DbSet<CopilotResultFeedback> CopilotResultFeedbacks => Set<CopilotResultFeedback>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();

    public DbSet<MessageReport> MessageReports => Set<MessageReport>();

    public DbSet<MessageReadReceipt> MessageReadReceipts => Set<MessageReadReceipt>();

    public DbSet<ShelterAvailabilitySlot> ShelterAvailabilitySlots => Set<ShelterAvailabilitySlot>();

    public DbSet<LostFoundPost> LostFoundPosts => Set<LostFoundPost>();

    public DbSet<LostFoundPostImage> LostFoundPostImages => Set<LostFoundPostImage>();

    public DbSet<DogTransferRequest> DogTransferRequests => Set<DogTransferRequest>();

    public DbSet<VolunteerProfile> VolunteerProfiles => Set<VolunteerProfile>();

    public DbSet<VolunteerTask> VolunteerTasks => Set<VolunteerTask>();

    public DbSet<VolunteerTaskActivity> VolunteerTaskActivities => Set<VolunteerTaskActivity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Shelter>()
            .HasOne(s => s.ApplicationUser)
            .WithOne(u => u.Shelter)
            .HasForeignKey<Shelter>(s => s.ApplicationUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ShelterAvailabilitySlot>()
            .HasOne(slot => slot.Shelter)
            .WithMany(shelter => shelter.AvailabilitySlots)
            .HasForeignKey(slot => slot.ShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ShelterAvailabilitySlot>()
            .HasOne(slot => slot.BookedAdoptionRequest)
            .WithMany()
            .HasForeignKey(slot => slot.BookedAdoptionRequestId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ShelterAvailabilitySlot>()
            .HasOne(slot => slot.CreatedByUser)
            .WithMany()
            .HasForeignKey(slot => slot.CreatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ShelterAvailabilitySlot>()
            .HasOne(slot => slot.CancelledByUser)
            .WithMany()
            .HasForeignKey(slot => slot.CancelledByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ShelterAvailabilitySlot>()
            .Property(slot => slot.Notes)
            .HasMaxLength(500);

        builder.Entity<ShelterAvailabilitySlot>()
            .Property(slot => slot.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<ShelterAvailabilitySlot>()
            .HasIndex(slot => new { slot.ShelterId, slot.StartTime });

        builder.Entity<ShelterAvailabilitySlot>()
            .HasIndex(slot => new { slot.ShelterId, slot.IsCancelled, slot.IsBooked, slot.StartTime });

        builder.Entity<ShelterAvailabilitySlot>()
            .HasIndex(slot => slot.BookedAdoptionRequestId)
            .IsUnique()
            .HasFilter("[BookedAdoptionRequestId] IS NOT NULL AND [IsCancelled] = 0");

        builder.Entity<LostFoundPost>()
            .HasOne(post => post.CreatedByUser)
            .WithMany()
            .HasForeignKey(post => post.CreatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LostFoundPost>()
            .HasOne(post => post.ApprovedByUser)
            .WithMany()
            .HasForeignKey(post => post.ApprovedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LostFoundPost>()
            .HasOne(post => post.ClosedByUser)
            .WithMany()
            .HasForeignKey(post => post.ClosedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LostFoundPost>()
            .Property(post => post.Title)
            .HasMaxLength(120);

        builder.Entity<LostFoundPost>()
            .Property(post => post.Description)
            .HasMaxLength(2000);

        builder.Entity<LostFoundPost>()
            .Property(post => post.DogName)
            .HasMaxLength(80);

        builder.Entity<LostFoundPost>()
            .Property(post => post.BreedText)
            .HasMaxLength(120);

        builder.Entity<LostFoundPost>()
            .Property(post => post.CoatColor)
            .HasMaxLength(80);

        builder.Entity<LostFoundPost>()
            .Property(post => post.DistinctiveMarks)
            .HasMaxLength(500);

        builder.Entity<LostFoundPost>()
            .Property(post => post.City)
            .HasMaxLength(80);

        builder.Entity<LostFoundPost>()
            .Property(post => post.Neighborhood)
            .HasMaxLength(80);

        builder.Entity<LostFoundPost>()
            .Property(post => post.AddressOrAreaDescription)
            .HasMaxLength(250);

        builder.Entity<LostFoundPost>()
            .Property(post => post.ContactName)
            .HasMaxLength(120);

        builder.Entity<LostFoundPost>()
            .Property(post => post.ContactEmail)
            .HasMaxLength(256);

        builder.Entity<LostFoundPost>()
            .Property(post => post.ContactPhone)
            .HasMaxLength(40);

        builder.Entity<LostFoundPost>()
            .Property(post => post.RejectionReason)
            .HasMaxLength(500);

        builder.Entity<LostFoundPost>()
            .Property(post => post.ResolutionNotes)
            .HasMaxLength(1000);

        builder.Entity<LostFoundPost>()
            .Property(post => post.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<LostFoundPost>()
            .HasIndex(post => post.Status);

        builder.Entity<LostFoundPost>()
            .HasIndex(post => post.PostType);

        builder.Entity<LostFoundPost>()
            .HasIndex(post => post.City);

        builder.Entity<LostFoundPost>()
            .HasIndex(post => post.Neighborhood);

        builder.Entity<LostFoundPost>()
            .HasIndex(post => post.LastSeenOrFoundDate);

        builder.Entity<LostFoundPost>()
            .HasIndex(post => post.CreatedByUserId);

        builder.Entity<LostFoundPostImage>()
            .HasOne(image => image.LostFoundPost)
            .WithMany(post => post.Images)
            .HasForeignKey(image => image.LostFoundPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LostFoundPostImage>()
            .HasOne(image => image.UploadedByUser)
            .WithMany()
            .HasForeignKey(image => image.UploadedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LostFoundPostImage>()
            .Property(image => image.ImageUrlOrPath)
            .HasMaxLength(500);

        builder.Entity<LostFoundPostImage>()
            .Property(image => image.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<LostFoundPostImage>()
            .HasIndex(image => image.LostFoundPostId);

        builder.Entity<AdopterProfile>()
            .HasOne(p => p.ApplicationUser)
            .WithOne(u => u.AdopterProfile)
            .HasForeignKey<AdopterProfile>(p => p.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AdopterProfile>()
            .HasIndex(p => p.ApplicationUserId)
            .IsUnique();

        builder.Entity<Dog>()
            .HasOne(d => d.Shelter)
            .WithMany(s => s.Dogs)
            .HasForeignKey(d => d.ShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Dog>()
            .Property(d => d.CoatColor)
            .HasMaxLength(80);

        builder.Entity<Dog>()
            .Property(d => d.CompatibilityNotes)
            .HasMaxLength(1000);

        builder.Entity<DogBreed>()
            .HasIndex(breed => breed.Name)
            .IsUnique();

        builder.Entity<Dog>()
            .HasOne(d => d.DogBreed)
            .WithMany(breed => breed.Dogs)
            .HasForeignKey(d => d.DogBreedId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Dog>()
            .HasOne(d => d.SecondaryBreed)
            .WithMany()
            .HasForeignKey(d => d.SecondaryBreedId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Dog>()
            .HasOne(d => d.PreferredFoodType)
            .WithMany(f => f.Dogs)
            .HasForeignKey(d => d.PreferredFoodTypeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DogSearchEmbedding>()
            .HasOne(embedding => embedding.Dog)
            .WithOne()
            .HasForeignKey<DogSearchEmbedding>(embedding => embedding.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DogSearchEmbedding>()
            .HasIndex(embedding => embedding.DogId)
            .IsUnique();

        builder.Entity<DogSearchEmbedding>()
            .HasIndex(embedding => embedding.UpdatedAt);

        builder.Entity<CopilotSession>()
            .HasOne(session => session.AdopterUser)
            .WithMany()
            .HasForeignKey(session => session.AdopterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CopilotSession>()
            .Property(session => session.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<CopilotSession>()
            .Property(session => session.QueryText)
            .HasMaxLength(1000);

        builder.Entity<CopilotSession>()
            .Property(session => session.SanitizedQuerySummary)
            .HasMaxLength(500);

        builder.Entity<CopilotSession>()
            .Property(session => session.PrimaryIntent)
            .HasMaxLength(80);

        builder.Entity<CopilotSession>()
            .Property(session => session.CompatibilityTarget)
            .HasMaxLength(80);

        builder.Entity<CopilotSession>()
            .Property(session => session.HomeType)
            .HasMaxLength(80);

        builder.Entity<CopilotSession>()
            .Property(session => session.ActivityLevel)
            .HasMaxLength(80);

        builder.Entity<CopilotSession>()
            .Property(session => session.City)
            .HasMaxLength(120);

        builder.Entity<CopilotSession>()
            .Property(session => session.Neighborhood)
            .HasMaxLength(120);

        builder.Entity<CopilotSession>()
            .Property(session => session.FallbackReason)
            .HasMaxLength(500);

        builder.Entity<CopilotSession>()
            .HasIndex(session => new { session.AdopterUserId, session.CreatedAt });

        builder.Entity<CopilotResultFeedback>()
            .HasOne(feedback => feedback.CopilotSession)
            .WithMany(session => session.Feedback)
            .HasForeignKey(feedback => feedback.CopilotSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CopilotResultFeedback>()
            .HasOne(feedback => feedback.Dog)
            .WithMany()
            .HasForeignKey(feedback => feedback.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CopilotResultFeedback>()
            .HasOne(feedback => feedback.AdopterUser)
            .WithMany()
            .HasForeignKey(feedback => feedback.AdopterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CopilotResultFeedback>()
            .Property(feedback => feedback.OptionalComment)
            .HasMaxLength(500);

        builder.Entity<CopilotResultFeedback>()
            .Property(feedback => feedback.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<CopilotResultFeedback>()
            .HasIndex(feedback => new { feedback.CopilotSessionId, feedback.DogId, feedback.AdopterUserId })
            .IsUnique();

        builder.Entity<CopilotResultFeedback>()
            .HasIndex(feedback => feedback.AdopterUserId);

        builder.Entity<DogImage>()
            .HasOne(i => i.Dog)
            .WithMany(d => d.Images)
            .HasForeignKey(i => i.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MedicalRecord>()
            .HasOne(m => m.Dog)
            .WithMany(d => d.MedicalRecords)
            .HasForeignKey(m => m.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DogStatusHistory>()
            .HasOne(h => h.Dog)
            .WithMany(d => d.StatusHistories)
            .HasForeignKey(h => h.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DogStatusHistory>()
            .HasOne(h => h.ChangedByUser)
            .WithMany(u => u.DogStatusHistories)
            .HasForeignKey(h => h.ChangedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<DogStatusHistory>()
            .Property(h => h.ChangedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<AdoptionRequest>()
            .HasOne(a => a.Dog)
            .WithMany(d => d.AdoptionRequests)
            .HasForeignKey(a => a.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AdoptionRequest>()
            .HasOne(a => a.Adopter)
            .WithMany(u => u.AdoptionRequests)
            .HasForeignKey(a => a.AdopterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AdoptionRequest>()
            .HasOne(a => a.VisitConfirmedByUser)
            .WithMany()
            .HasForeignKey(a => a.VisitConfirmedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<AdoptionRequest>()
            .HasIndex(a => new { a.AdopterId, a.DogId })
            .HasFilter("[Status] = 0")
            .IsUnique();

        builder.Entity<AdoptionRequest>()
            .Property(a => a.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<AdoptionRequest>()
            .Property(a => a.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<Conversation>()
            .HasOne(conversation => conversation.AdoptionRequest)
            .WithOne()
            .HasForeignKey<Conversation>(conversation => conversation.AdoptionRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Conversation>()
            .HasIndex(conversation => conversation.AdoptionRequestId)
            .IsUnique();

        builder.Entity<Conversation>()
            .Property(conversation => conversation.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<Message>()
            .HasOne(message => message.Conversation)
            .WithMany(conversation => conversation.Messages)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Message>()
            .HasOne(message => message.SenderUser)
            .WithMany()
            .HasForeignKey(message => message.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .Property(message => message.Body)
            .HasMaxLength(2000);

        builder.Entity<Message>()
            .Property(message => message.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<Message>()
            .HasIndex(message => new { message.ConversationId, message.CreatedAt });

        builder.Entity<MessageAttachment>()
            .HasOne(attachment => attachment.Message)
            .WithMany(message => message.Attachments)
            .HasForeignKey(attachment => attachment.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MessageAttachment>()
            .HasOne(attachment => attachment.UploadedByUser)
            .WithMany()
            .HasForeignKey(attachment => attachment.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MessageAttachment>()
            .Property(attachment => attachment.OriginalFileName)
            .HasMaxLength(255);

        builder.Entity<MessageAttachment>()
            .Property(attachment => attachment.StoredFileName)
            .HasMaxLength(255);

        builder.Entity<MessageAttachment>()
            .Property(attachment => attachment.FilePathOrKey)
            .HasMaxLength(500);

        builder.Entity<MessageAttachment>()
            .Property(attachment => attachment.ContentType)
            .HasMaxLength(120);

        builder.Entity<MessageAttachment>()
            .Property(attachment => attachment.UploadedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<MessageAttachment>()
            .HasIndex(attachment => attachment.MessageId);

        builder.Entity<MessageAttachment>()
            .HasIndex(attachment => attachment.UploadedByUserId);

        builder.Entity<MessageReaction>()
            .HasOne(reaction => reaction.Message)
            .WithMany(message => message.Reactions)
            .HasForeignKey(reaction => reaction.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MessageReaction>()
            .HasOne(reaction => reaction.User)
            .WithMany()
            .HasForeignKey(reaction => reaction.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MessageReaction>()
            .Property(reaction => reaction.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<MessageReaction>()
            .HasIndex(reaction => reaction.UserId);

        builder.Entity<MessageReaction>()
            .HasIndex(reaction => new { reaction.MessageId, reaction.UserId, reaction.ReactionType })
            .IsUnique();

        builder.Entity<MessageReport>()
            .HasOne(report => report.Message)
            .WithMany(message => message.Reports)
            .HasForeignKey(report => report.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MessageReport>()
            .HasOne(report => report.ReporterUser)
            .WithMany()
            .HasForeignKey(report => report.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MessageReport>()
            .HasOne(report => report.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(report => report.ReviewedByAdminId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<MessageReport>()
            .Property(report => report.Reason)
            .HasMaxLength(80);

        builder.Entity<MessageReport>()
            .Property(report => report.Details)
            .HasMaxLength(1000);

        builder.Entity<MessageReport>()
            .Property(report => report.AdminNote)
            .HasMaxLength(1000);

        builder.Entity<MessageReport>()
            .Property(report => report.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<MessageReport>()
            .HasIndex(report => report.Status);

        builder.Entity<MessageReport>()
            .HasIndex(report => report.CreatedAt);

        builder.Entity<MessageReport>()
            .HasIndex(report => new { report.MessageId, report.ReporterUserId })
            .IsUnique();

        builder.Entity<MessageReadReceipt>()
            .HasOne(receipt => receipt.Message)
            .WithMany(message => message.ReadReceipts)
            .HasForeignKey(receipt => receipt.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MessageReadReceipt>()
            .HasOne(receipt => receipt.User)
            .WithMany()
            .HasForeignKey(receipt => receipt.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MessageReadReceipt>()
            .Property(receipt => receipt.ReadAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<MessageReadReceipt>()
            .HasIndex(receipt => new { receipt.MessageId, receipt.UserId })
            .IsUnique();

        builder.Entity<Shelter>()
            .Property(s => s.VisitStartTime)
            .HasDefaultValue(new TimeSpan(10, 0, 0));

        builder.Entity<Shelter>()
            .Property(s => s.VisitEndTime)
            .HasDefaultValue(new TimeSpan(17, 0, 0));

        builder.Entity<Shelter>()
            .Property(s => s.VisitsAllowedMonday)
            .HasDefaultValue(true);

        builder.Entity<Shelter>()
            .Property(s => s.VisitsAllowedTuesday)
            .HasDefaultValue(true);

        builder.Entity<Shelter>()
            .Property(s => s.VisitsAllowedWednesday)
            .HasDefaultValue(true);

        builder.Entity<Shelter>()
            .Property(s => s.VisitsAllowedThursday)
            .HasDefaultValue(true);

        builder.Entity<Shelter>()
            .Property(s => s.VisitsAllowedFriday)
            .HasDefaultValue(true);

        builder.Entity<SavedDogSearch>()
            .HasOne(search => search.AdopterUser)
            .WithMany()
            .HasForeignKey(search => search.AdopterUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedDogSearch>()
            .HasOne(search => search.Shelter)
            .WithMany()
            .HasForeignKey(search => search.ShelterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<SavedDogSearch>()
            .Property(search => search.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedDogSearch>()
            .Property(search => search.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => search.AdopterUserId);

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => new { search.AdopterUserId, search.Name })
            .IsUnique();

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => new { search.AlertsEnabled, search.LastEvaluatedAtUtc });

        builder.Entity<SavedSearchMatch>()
            .HasOne(match => match.SavedDogSearch)
            .WithMany(search => search.Matches)
            .HasForeignKey(match => match.SavedDogSearchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedSearchMatch>()
            .HasOne(match => match.Dog)
            .WithMany()
            .HasForeignKey(match => match.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SavedSearchMatch>()
            .Property(match => match.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedSearchMatch>()
            .Property(match => match.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => new { match.SavedDogSearchId, match.DogId })
            .IsUnique();

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => match.DogId);

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => match.Status);
        builder.Entity<FavoriteDog>()
            .HasIndex(f => new { f.AdopterId, f.DogId })
            .IsUnique();

        builder.Entity<SavedDogSearch>()
            .HasOne(search => search.AdopterUser)
            .WithMany()
            .HasForeignKey(search => search.AdopterUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedDogSearch>()
            .HasOne(search => search.Shelter)
            .WithMany()
            .HasForeignKey(search => search.ShelterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<SavedDogSearch>()
            .Property(search => search.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedDogSearch>()
            .Property(search => search.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => search.AdopterUserId);

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => new { search.AdopterUserId, search.Name })
            .IsUnique();

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => new { search.AlertsEnabled, search.LastEvaluatedAtUtc });

        builder.Entity<SavedSearchMatch>()
            .HasOne(match => match.SavedDogSearch)
            .WithMany(search => search.Matches)
            .HasForeignKey(match => match.SavedDogSearchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedSearchMatch>()
            .HasOne(match => match.Dog)
            .WithMany()
            .HasForeignKey(match => match.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SavedSearchMatch>()
            .Property(match => match.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedSearchMatch>()
            .Property(match => match.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => new { match.SavedDogSearchId, match.DogId })
            .IsUnique();

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => match.DogId);

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => match.Status);
        builder.Entity<FavoriteDog>()
            .HasOne(f => f.Adopter)
            .WithMany(u => u.FavoriteDogs)
            .HasForeignKey(f => f.AdopterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SavedDogSearch>()
            .HasOne(search => search.AdopterUser)
            .WithMany()
            .HasForeignKey(search => search.AdopterUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedDogSearch>()
            .HasOne(search => search.Shelter)
            .WithMany()
            .HasForeignKey(search => search.ShelterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<SavedDogSearch>()
            .Property(search => search.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedDogSearch>()
            .Property(search => search.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => search.AdopterUserId);

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => new { search.AdopterUserId, search.Name })
            .IsUnique();

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => new { search.AlertsEnabled, search.LastEvaluatedAtUtc });

        builder.Entity<SavedSearchMatch>()
            .HasOne(match => match.SavedDogSearch)
            .WithMany(search => search.Matches)
            .HasForeignKey(match => match.SavedDogSearchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedSearchMatch>()
            .HasOne(match => match.Dog)
            .WithMany()
            .HasForeignKey(match => match.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SavedSearchMatch>()
            .Property(match => match.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedSearchMatch>()
            .Property(match => match.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => new { match.SavedDogSearchId, match.DogId })
            .IsUnique();

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => match.DogId);

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => match.Status);
        builder.Entity<FavoriteDog>()
            .HasOne(f => f.Dog)
            .WithMany(d => d.FavoriteDogs)
            .HasForeignKey(f => f.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SavedDogSearch>()
            .HasOne(search => search.AdopterUser)
            .WithMany()
            .HasForeignKey(search => search.AdopterUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedDogSearch>()
            .HasOne(search => search.Shelter)
            .WithMany()
            .HasForeignKey(search => search.ShelterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<SavedDogSearch>()
            .Property(search => search.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedDogSearch>()
            .Property(search => search.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => search.AdopterUserId);

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => new { search.AdopterUserId, search.Name })
            .IsUnique();

        builder.Entity<SavedDogSearch>()
            .HasIndex(search => new { search.AlertsEnabled, search.LastEvaluatedAtUtc });

        builder.Entity<SavedSearchMatch>()
            .HasOne(match => match.SavedDogSearch)
            .WithMany(search => search.Matches)
            .HasForeignKey(match => match.SavedDogSearchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedSearchMatch>()
            .HasOne(match => match.Dog)
            .WithMany()
            .HasForeignKey(match => match.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SavedSearchMatch>()
            .Property(match => match.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedSearchMatch>()
            .Property(match => match.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => new { match.SavedDogSearchId, match.DogId })
            .IsUnique();

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => match.DogId);

        builder.Entity<SavedSearchMatch>()
            .HasIndex(match => match.Status);
        builder.Entity<FavoriteDog>()
            .Property(f => f.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<RecentlyViewedDog>()
            .HasIndex(v => new { v.AdopterId, v.DogId })
            .IsUnique();

        builder.Entity<RecentlyViewedDog>()
            .HasOne(v => v.Adopter)
            .WithMany(u => u.RecentlyViewedDogs)
            .HasForeignKey(v => v.AdopterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RecentlyViewedDog>()
            .HasOne(v => v.Dog)
            .WithMany(d => d.RecentlyViewedDogs)
            .HasForeignKey(v => v.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RecentlyViewedDog>()
            .Property(v => v.ViewedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<ResourceStock>()
            .HasOne(r => r.Shelter)
            .WithMany(s => s.ResourceStocks)
            .HasForeignKey(r => r.ShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ResourceStock>()
            .HasOne(r => r.ResourceCategory)
            .WithMany(c => c.ResourceStocks)
            .HasForeignKey(r => r.ResourceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ResourceStock>()
            .HasOne(r => r.FoodType)
            .WithMany(f => f.ResourceStocks)
            .HasForeignKey(r => r.FoodTypeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ResourceStock>()
            .Property(r => r.LastUpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<ShelterRegistrationRequest>()
            .HasIndex(r => r.Email)
            .HasFilter("[Status] = 0")
            .IsUnique();

        builder.Entity<ShelterRegistrationRequest>()
            .HasOne(r => r.ReviewedByUser)
            .WithMany(u => u.ReviewedShelterRegistrationRequests)
            .HasForeignKey(r => r.ReviewedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ShelterRegistrationRequest>()
            .HasOne(r => r.CreatedShelter)
            .WithMany()
            .HasForeignKey(r => r.CreatedShelterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ShelterRegistrationRequest>()
            .Property(r => r.SubmittedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<AuditLog>()
            .Property(log => log.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<AuditLog>()
            .Property(log => log.UserAgent)
            .HasMaxLength(512);

        builder.Entity<AuditLog>()
            .Property(log => log.CorrelationId)
            .HasMaxLength(100);

        builder.Entity<AuditLog>()
            .Property(log => log.Severity)
            .HasMaxLength(40)
            .HasDefaultValue("Information");

        builder.Entity<AuditLog>()
            .Property(log => log.EventType)
            .HasMaxLength(80)
            .HasDefaultValue("Business");

        builder.Entity<AuditLog>()
            .Property(log => log.DetailsJson)
            .HasMaxLength(4000);

        builder.Entity<AuditLog>()
            .HasIndex(log => log.CreatedAt);

        builder.Entity<AuditLog>()
            .HasIndex(log => log.Action);

        builder.Entity<AuditLog>()
            .HasIndex(log => log.EntityName);

        builder.Entity<AuditLog>()
            .HasIndex(log => log.UserId);

        builder.Entity<AuditLog>()
            .HasIndex(log => log.CorrelationId);

        builder.Entity<Notification>()
            .HasOne(notification => notification.User)
            .WithMany(user => user.Notifications)
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Notification>()
            .Property(notification => notification.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<Notification>()
            .HasIndex(notification => new { notification.UserId, notification.IsRead, notification.CreatedAt });

        builder.Entity<NotificationPreference>()
            .HasOne(preference => preference.User)
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<NotificationPreference>()
            .Property(preference => preference.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<NotificationPreference>()
            .Property(preference => preference.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<NotificationPreference>()
            .HasIndex(preference => new { preference.UserId, preference.NotificationType })
            .IsUnique();

        builder.Entity<NotificationDeliveryLog>()
            .HasOne(log => log.Notification)
            .WithMany()
            .HasForeignKey(log => log.NotificationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<NotificationDeliveryLog>()
            .HasOne(log => log.User)
            .WithMany()
            .HasForeignKey(log => log.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<NotificationDeliveryLog>()
            .Property(log => log.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<NotificationDeliveryLog>()
            .HasIndex(log => log.UserId);

        builder.Entity<NotificationDeliveryLog>()
            .HasIndex(log => log.NotificationType);

        builder.Entity<NotificationDeliveryLog>()
            .HasIndex(log => log.Channel);

        builder.Entity<NotificationDeliveryLog>()
            .HasIndex(log => log.Status);

        builder.Entity<NotificationDeliveryLog>()
            .HasIndex(log => log.CreatedAt);

        builder.Entity<NotificationDeliveryLog>()
            .HasIndex(log => new { log.RelatedEntityType, log.RelatedEntityId });

        builder.Entity<NotificationOutboxMessage>()
            .HasOne(message => message.RecipientUser)
            .WithMany()
            .HasForeignKey(message => message.RecipientUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<NotificationOutboxMessage>()
            .Property(message => message.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<NotificationOutboxMessage>()
            .Property(message => message.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<NotificationOutboxMessage>()
            .HasIndex(message => new { message.Status, message.NextAttemptAt });

        builder.Entity<NotificationOutboxMessage>()
            .HasIndex(message => message.CreatedAt);

        builder.Entity<NotificationOutboxMessage>()
            .HasIndex(message => message.RecipientUserId);

        builder.Entity<NotificationOutboxMessage>()
            .HasIndex(message => message.Channel);

        builder.Entity<NotificationOutboxMessage>()
            .HasIndex(message => message.NotificationType);

        builder.Entity<NotificationOutboxMessage>()
            .HasIndex(message => message.CorrelationId);

        builder.Entity<NotificationOutboxMessage>()
            .HasIndex(message => message.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.Entity<ReportHistory>()
            .HasOne(history => history.Shelter)
            .WithMany()
            .HasForeignKey(history => history.ShelterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ReportHistory>()
            .Property(history => history.GeneratedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<ReportHistory>()
            .HasIndex(history => history.GeneratedAt);

        builder.Entity<ReportHistory>()
            .HasIndex(history => history.ReportType);

        builder.Entity<ReportHistory>()
            .HasIndex(history => new { history.ShelterId, history.GeneratedAt });

        builder.Entity<DogTransferRequest>()
            .HasOne(transfer => transfer.Dog)
            .WithMany()
            .HasForeignKey(transfer => transfer.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DogTransferRequest>()
            .HasOne(transfer => transfer.SourceShelter)
            .WithMany()
            .HasForeignKey(transfer => transfer.SourceShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DogTransferRequest>()
            .HasOne(transfer => transfer.DestinationShelter)
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DogTransferRequest>()
            .HasOne(transfer => transfer.RequestedByUser)
            .WithMany()
            .HasForeignKey(transfer => transfer.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DogTransferRequest>()
            .HasOne(transfer => transfer.RespondedByUser)
            .WithMany()
            .HasForeignKey(transfer => transfer.RespondedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DogTransferRequest>()
            .HasOne(transfer => transfer.CompletedByUser)
            .WithMany()
            .HasForeignKey(transfer => transfer.CompletedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DogTransferRequest>()
            .Property(transfer => transfer.Reason)
            .HasMaxLength(1000);

        builder.Entity<DogTransferRequest>()
            .Property(transfer => transfer.SourceShelterNotes)
            .HasMaxLength(1000);

        builder.Entity<DogTransferRequest>()
            .Property(transfer => transfer.DestinationShelterResponseNotes)
            .HasMaxLength(1000);

        builder.Entity<DogTransferRequest>()
            .Property(transfer => transfer.AdminNotes)
            .HasMaxLength(1000);

        builder.Entity<DogTransferRequest>()
            .Property(transfer => transfer.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<DogTransferRequest>()
            .Property(transfer => transfer.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<DogTransferRequest>()
            .HasIndex(transfer => transfer.DogId);

        builder.Entity<DogTransferRequest>()
            .HasIndex(transfer => transfer.SourceShelterId);

        builder.Entity<DogTransferRequest>()
            .HasIndex(transfer => transfer.DestinationShelterId);

        builder.Entity<DogTransferRequest>()
            .HasIndex(transfer => transfer.Status);

        builder.Entity<DogTransferRequest>()
            .HasIndex(transfer => transfer.RequestedAtUtc);
        builder.Entity<VolunteerProfile>()
            .HasOne(profile => profile.User)
            .WithMany()
            .HasForeignKey(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VolunteerProfile>()
            .HasOne(profile => profile.PreferredShelter)
            .WithMany()
            .HasForeignKey(profile => profile.PreferredShelterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<VolunteerProfile>()
            .Property(profile => profile.DisplayName)
            .HasMaxLength(120);

        builder.Entity<VolunteerProfile>()
            .Property(profile => profile.Email)
            .HasMaxLength(256);

        builder.Entity<VolunteerProfile>()
            .Property(profile => profile.PhoneNumber)
            .HasMaxLength(40);

        builder.Entity<VolunteerProfile>()
            .Property(profile => profile.Skills)
            .HasMaxLength(1000);

        builder.Entity<VolunteerProfile>()
            .Property(profile => profile.AvailabilityNotes)
            .HasMaxLength(1000);

        builder.Entity<VolunteerProfile>()
            .Property(profile => profile.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<VolunteerProfile>()
            .Property(profile => profile.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<VolunteerProfile>()
            .HasIndex(profile => profile.UserId)
            .IsUnique();

        builder.Entity<VolunteerProfile>()
            .HasIndex(profile => profile.PreferredShelterId);

        builder.Entity<VolunteerProfile>()
            .HasIndex(profile => profile.IsActive);

        builder.Entity<VolunteerTask>()
            .HasOne(task => task.Shelter)
            .WithMany()
            .HasForeignKey(task => task.ShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VolunteerTask>()
            .HasOne(task => task.Dog)
            .WithMany()
            .HasForeignKey(task => task.DogId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<VolunteerTask>()
            .HasOne(task => task.CreatedByUser)
            .WithMany()
            .HasForeignKey(task => task.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VolunteerTask>()
            .HasOne(task => task.AssignedVolunteerProfile)
            .WithMany(profile => profile.AssignedTasks)
            .HasForeignKey(task => task.AssignedVolunteerProfileId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<VolunteerTask>()
            .Property(task => task.Title)
            .HasMaxLength(160);

        builder.Entity<VolunteerTask>()
            .Property(task => task.Description)
            .HasMaxLength(1000);

        builder.Entity<VolunteerTask>()
            .Property(task => task.Location)
            .HasMaxLength(250);

        builder.Entity<VolunteerTask>()
            .Property(task => task.RequiredSkills)
            .HasMaxLength(500);

        builder.Entity<VolunteerTask>()
            .Property(task => task.ShelterNotes)
            .HasMaxLength(1000);

        builder.Entity<VolunteerTask>()
            .Property(task => task.VolunteerNotes)
            .HasMaxLength(1000);

        builder.Entity<VolunteerTask>()
            .Property(task => task.CompletionNotes)
            .HasMaxLength(1000);

        builder.Entity<VolunteerTask>()
            .Property(task => task.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<VolunteerTask>()
            .Property(task => task.UpdatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<VolunteerTask>()
            .HasIndex(task => task.ShelterId);

        builder.Entity<VolunteerTask>()
            .HasIndex(task => task.AssignedVolunteerProfileId);

        builder.Entity<VolunteerTask>()
            .HasIndex(task => task.Status);

        builder.Entity<VolunteerTask>()
            .HasIndex(task => task.Category);

        builder.Entity<VolunteerTask>()
            .HasIndex(task => task.ScheduledStartUtc);

        builder.Entity<VolunteerTask>()
            .HasIndex(task => task.DueAtUtc);

        builder.Entity<VolunteerTaskActivity>()
            .HasOne(activity => activity.VolunteerTask)
            .WithMany(task => task.Activities)
            .HasForeignKey(activity => activity.VolunteerTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<VolunteerTaskActivity>()
            .HasOne(activity => activity.ActorUser)
            .WithMany()
            .HasForeignKey(activity => activity.ActorUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VolunteerTaskActivity>()
            .Property(activity => activity.Message)
            .HasMaxLength(1000);

        builder.Entity<VolunteerTaskActivity>()
            .Property(activity => activity.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<VolunteerTaskActivity>()
            .HasIndex(activity => new { activity.VolunteerTaskId, activity.CreatedAtUtc });

        builder.Entity<VolunteerTaskActivity>()
            .HasIndex(activity => activity.ActorUserId);
        SeedLookupData(builder);
    }

    private static void SeedLookupData(ModelBuilder builder)
    {
        builder.Entity<DogBreed>().HasData(DogBreedSeedData.CreateSeedEntities());

        builder.Entity<ResourceCategory>().HasData(
            new ResourceCategory { Id = 1, Name = "Food", Description = "Food supplies for dogs." },
            new ResourceCategory { Id = 2, Name = "Medicine", Description = "Medication and medical supplies." },
            new ResourceCategory { Id = 3, Name = "Blankets", Description = "Blankets and bedding materials." },
            new ResourceCategory { Id = 4, Name = "Cleaning Supplies", Description = "Cleaning and sanitation products." },
            new ResourceCategory { Id = 5, Name = "Accessories", Description = "Leashes, collars, bowls, and similar items." },
            new ResourceCategory { Id = 6, Name = "Other", Description = "General shelter resources." });

        builder.Entity<FoodType>().HasData(
            new FoodType { Id = 1, Name = "Adult dry food", Description = "Standard dry food for adult dogs." },
            new FoodType { Id = 2, Name = "Puppy food", Description = "Food suitable for puppies." },
            new FoodType { Id = 3, Name = "Senior food", Description = "Food suitable for older dogs." },
            new FoodType { Id = 4, Name = "Wet food", Description = "Canned or wet dog food." },
            new FoodType { Id = 5, Name = "Medical diet food", Description = "Special diet food recommended by a veterinarian." });
    }
}



