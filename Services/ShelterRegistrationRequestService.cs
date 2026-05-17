using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ShelterRegistrationRequestService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IPdfReportService pdfReportService,
    ILogger<ShelterRegistrationRequestService> logger,
    IAuditLogService? auditLogService = null,
    INotificationService? notificationService = null,
    IReportHistoryService? reportHistoryService = null) : IShelterRegistrationRequestService
{
    public async Task<ShelterRegistrationRequest> SubmitRequestAsync(ShelterRegistrationRequest request, string? currentUserId = null)
    {
        await EnsureCanSubmitApplicationAsync(currentUserId);
        ValidateRequest(request);

        request.Email = request.Email.Trim();
        var normalizedEmail = NormalizeEmail(request.Email);

        await EnsureNoExistingShelterAccountForEmailAsync(normalizedEmail);

        var duplicatePending = await context.ShelterRegistrationRequests
            .AnyAsync(r => r.Status == ShelterRegistrationRequestStatus.Pending && r.Email.Trim().ToUpper() == normalizedEmail);

        if (duplicatePending)
        {
            throw new InvalidOperationException("A shelter application with this email is already pending review.");
        }

        request.ShelterName = request.ShelterName.Trim();
        request.ContactPersonName = request.ContactPersonName.Trim();
        request.PhoneNumber = request.PhoneNumber.Trim();
        request.City = request.City.Trim();
        request.Neighborhood = NormalizeOptional(request.Neighborhood);
        request.Address = NormalizeAddressWithoutCity(request.Address, request.City);
        request.Description = request.Description.Trim();
        request.Website = NormalizeOptional(request.Website);
        request.OpeningHours = NormalizeOptional(request.OpeningHours);
        request.ReasonForJoining = NormalizeOptional(request.ReasonForJoining);
        request.Status = ShelterRegistrationRequestStatus.Pending;
        request.SubmittedAt = DateTime.UtcNow;

        context.ShelterRegistrationRequests.Add(request);
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ShelterRegistrationRequestSubmitted,
            "ShelterRegistrationRequest",
            request.Id.ToString(),
            $"Shelter application for {request.ShelterName} was submitted.",
            userId: currentUserId,
            additionalData: $"Email={request.Email}");

        await TryNotifyAdminsAsync(request);
        return request;
    }

    public Task<List<ShelterRegistrationRequest>> GetAllAsync()
    {
        return context.ShelterRegistrationRequests
            .Include(r => r.CreatedShelter)
            .Include(r => r.ReviewedByUser)
            .OrderByDescending(r => r.SubmittedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<ShelterRegistrationRequest?> GetByIdAsync(int id)
    {
        return context.ShelterRegistrationRequests
            .Include(r => r.CreatedShelter)
            .Include(r => r.ReviewedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<ShelterRegistrationRequest> AcceptRequestAsync(int requestId, string adminUserId)
    {
        await EnsureAdminAsync(adminUserId);

        var request = await context.ShelterRegistrationRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null)
        {
            throw new InvalidOperationException("Shelter application was not found.");
        }

        if (request.Status != ShelterRegistrationRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending shelter applications can be accepted.");
        }

        request.Email = request.Email.Trim();
        request.City = request.City.Trim();
        request.Neighborhood = NormalizeOptional(request.Neighborhood);
        request.Address = NormalizeAddressWithoutCity(request.Address, request.City);

        var normalizedEmail = NormalizeEmail(request.Email);
        await EnsureNoExistingShelterAccountForEmailAsync(normalizedEmail);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.ContactPersonName,
            PhoneNumber = request.PhoneNumber
        };

        var createResult = await userManager.CreateAsync(user, GenerateTemporaryPassword());
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException("The shelter user account could not be created.");
        }

        var roleResult = await userManager.AddToRoleAsync(user, IdentitySeedData.ShelterRole);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException("The shelter role could not be assigned.");
        }

        var shelter = new Shelter
        {
            Name = request.ShelterName,
            Description = request.Description,
            Address = request.Address,
            City = request.City,
            Neighborhood = request.Neighborhood,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ApplicationUserId = user.Id
        };

        context.Shelters.Add(shelter);
        request.Status = ShelterRegistrationRequestStatus.Accepted;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedByUserId = adminUserId;
        request.CreatedUserId = user.Id;

        await context.SaveChangesAsync();

        request.CreatedShelterId = shelter.Id;
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ShelterRegistrationRequestAccepted,
            "ShelterRegistrationRequest",
            request.Id.ToString(),
            $"Shelter application for {request.ShelterName} was accepted.",
            userId: adminUserId,
            additionalData: $"CreatedShelterId={shelter.Id};CreatedUserId={user.Id}");
        await LogAsync(
            AuditActions.ShelterCreated,
            "Shelter",
            shelter.Id.ToString(),
            $"Shelter {shelter.Name} was created from an approved application.",
            userId: adminUserId,
            additionalData: $"RequestId={request.Id}");

        return request;
    }

    public async Task<ShelterRegistrationRequest> RejectRequestAsync(int requestId, string adminUserId)
    {
        await EnsureAdminAsync(adminUserId);

        var request = await context.ShelterRegistrationRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null)
        {
            throw new InvalidOperationException("Shelter application was not found.");
        }

        if (request.Status != ShelterRegistrationRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending shelter applications can be rejected.");
        }

        request.Status = ShelterRegistrationRequestStatus.Rejected;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedByUserId = adminUserId;

        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ShelterRegistrationRequestRejected,
            "ShelterRegistrationRequest",
            request.Id.ToString(),
            $"Shelter application for {request.ShelterName} was rejected.",
            userId: adminUserId);
        return request;
    }

    private async Task TryNotifyAdminsAsync(ShelterRegistrationRequest request)
    {
        try
        {
            var admins = await userManager.GetUsersInRoleAsync(IdentitySeedData.AdminRole);
            var recipients = admins
                .Select(a => a.Email)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (notificationService is not null)
            {
                foreach (var admin in admins)
                {
                    await notificationService.CreateNotificationAsync(
                        admin.Id,
                        "New shelter application",
                        $"{request.ShelterName} submitted a shelter application.",
                        NotificationCategory.ShelterApplication,
                        NotificationType.Info,
                        "/admin/shelter-requests",
                        "ShelterRegistrationRequest",
                        request.Id.ToString());
                }
            }

            if (recipients.Count == 0)
            {
                logger.LogInformation("No admin email recipients found for shelter registration request {RequestId}.", request.Id);
                return;
            }

            var attachments = await TryCreatePdfAttachmentAsync(request.Id);
            var body = $"""
                A new shelter application was submitted.

                Shelter: {request.ShelterName}
                Contact: {request.ContactPersonName}
                Email: {request.Email}
                Phone: {request.PhoneNumber}
                Address: {request.Address}, {request.City}
                Neighborhood: {(string.IsNullOrWhiteSpace(request.Neighborhood) ? "Not provided" : request.Neighborhood)}
                Coordinates: {(request.Latitude.HasValue && request.Longitude.HasValue ? $"{request.Latitude:0.######}, {request.Longitude:0.######}" : "Not provided")}
                Submitted: {request.SubmittedAt:g}
                """;

            var htmlBody = PawConnectEmailTemplate.BuildHtml(
                "New shelter application submitted",
                "Hello,",
                ["A new shelter application was submitted and is ready for admin review."],
                details:
                [
                    new("Shelter", request.ShelterName),
                    new("Contact person", request.ContactPersonName),
                    new("Email", request.Email),
                    new("Phone", request.PhoneNumber),
                    new("City", request.City),
                    new("Neighborhood", string.IsNullOrWhiteSpace(request.Neighborhood) ? "Not provided" : request.Neighborhood),
                    new("Address", request.Address),
                    new("Map location", request.Latitude.HasValue && request.Longitude.HasValue ? "Map location selected" : "No map location provided"),
                    new("Submitted", request.SubmittedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm"))
                ],
                hasAttachment: attachments.Count > 0);

            foreach (var recipient in recipients)
            {
                const string subject = "New shelter application submitted";
                await emailService.SendEmailAsync(recipient!, subject, body, attachments, htmlBody);
                if (reportHistoryService is not null && attachments.Count > 0)
                {
                    await reportHistoryService.RecordReportSentAsync(new ReportHistoryRecord(
                        ReportHistoryTypes.ShelterRegistrationRequestReport,
                        ReportHistoryTriggers.System,
                        recipient,
                        subject,
                        attachments[0].FileName,
                        GeneratedAt: DateTime.UtcNow,
                        SentAt: DateTime.UtcNow,
                        RelatedEntityName: "ShelterRegistrationRequest",
                        RelatedEntityId: request.Id.ToString()));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin notification failed for shelter registration request {RequestId}.", request.Id);
        }
    }

    private async Task EnsureCanSubmitApplicationAsync(string? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return;
        }

        var user = await userManager.FindByIdAsync(currentUserId);
        if (user is null)
        {
            return;
        }

        if (await userManager.IsInRoleAsync(user, IdentitySeedData.AdminRole))
        {
            throw new InvalidOperationException("Admins manage shelter applications from the Admin Shelter Requests page.");
        }

        if (await userManager.IsInRoleAsync(user, IdentitySeedData.ShelterRole))
        {
            throw new InvalidOperationException("Your shelter account is already active.");
        }
    }

    private async Task EnsureAdminAsync(string adminUserId)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new InvalidOperationException("Only admins can review shelter applications.");
        }

        var admin = await userManager.FindByIdAsync(adminUserId);
        if (admin is null || !await userManager.IsInRoleAsync(admin, IdentitySeedData.AdminRole))
        {
            throw new InvalidOperationException("Only admins can review shelter applications.");
        }
    }

    private async Task<List<EmailAttachment>> TryCreatePdfAttachmentAsync(int requestId)
    {
        try
        {
            return
            [
                new EmailAttachment
                {
                    FileName = "ShelterRegistrationRequest.pdf",
                    ContentType = "application/pdf",
                    Content = await pdfReportService.GenerateShelterRegistrationRequestReportAsync(requestId)
                }
            ];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Shelter registration PDF generation failed for request {RequestId}.", requestId);
            return [];
        }
    }

    private static void ValidateRequest(ShelterRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ShelterName))
        {
            throw new InvalidOperationException("Shelter name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ContactPersonName))
        {
            throw new InvalidOperationException("Contact person name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email))
        {
            throw new InvalidOperationException("A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new InvalidOperationException("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.City))
        {
            throw new InvalidOperationException("City is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            throw new InvalidOperationException("Address is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new InvalidOperationException("Description is required.");
        }

        if (request.Latitude is < -90 or > 90)
        {
            throw new InvalidOperationException("Latitude must be between -90 and 90.");
        }

        if (request.Longitude is < -180 or > 180)
        {
            throw new InvalidOperationException("Longitude must be between -180 and 180.");
        }
    }

    private async Task EnsureNoExistingShelterAccountForEmailAsync(string normalizedEmail)
    {
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            throw new InvalidOperationException("A shelter account with this email already exists.");
        }

        var existingShelter = await context.Shelters.AnyAsync(s =>
            s.Email != null &&
            s.Email.Trim().ToUpper() == normalizedEmail);

        if (existingShelter)
        {
            throw new InvalidOperationException("A shelter account with this email already exists.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToUpperInvariant();
    }

    private static string NormalizeAddressWithoutCity(string address, string city)
    {
        var normalizedAddress = address.Trim();
        var normalizedCity = city.Trim();

        if (string.IsNullOrWhiteSpace(normalizedCity))
        {
            return normalizedAddress;
        }

        var citySuffix = $", {normalizedCity}";
        return normalizedAddress.EndsWith(citySuffix, StringComparison.OrdinalIgnoreCase)
            ? normalizedAddress[..^citySuffix.Length].Trim()
            : normalizedAddress;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GenerateTemporaryPassword()
    {
        return $"Shelter-{Guid.NewGuid():N}aA1!";
    }

    private Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        string? userId = null,
        string? additionalData = null)
    {
        return auditLogService?.LogAsync(action, entityName, entityId, description, userId: userId, additionalData: additionalData) ?? Task.CompletedTask;
    }
}
