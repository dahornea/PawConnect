using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ShelterService(IDbContextFactory<ApplicationDbContext> contextFactory, IAuditLogService? auditLogService = null) : IShelterService
{
    public Task<List<Shelter>> GetAllAsync()
    {
        return GetAllSheltersAsync();
    }

    public async Task<Shelter?> GetByIdAsync(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Shelters
            .Include(s => s.Dogs)
            .ThenInclude(d => d.Images)
            .Include(s => s.ResourceStocks)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Shelter?> GetPublicShelterDetailsAsync(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Shelters
            .Include(s => s.Dogs)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task CreateAsync(Shelter shelter)
    {
        ValidateShelterProfile(shelter);

        await using var context = await contextFactory.CreateDbContextAsync();
        await EnsureShelterEmailIsAvailableAsync(context, shelter.Email, shelter.Id);
        NormalizeShelterProfile(shelter);

        context.Shelters.Add(shelter);
        await context.SaveChangesAsync();
        await LogAsync(AuditActions.ShelterCreated, "Shelter", shelter.Id.ToString(), $"Shelter {shelter.Name} was created.");
    }

    public async Task UpdateAsync(Shelter shelter)
    {
        ValidateShelterProfile(shelter);

        await using var context = await contextFactory.CreateDbContextAsync();
        await EnsureShelterEmailIsAvailableAsync(context, shelter.Email, shelter.Id);
        NormalizeShelterProfile(shelter);

        context.Shelters.Update(shelter);
        await context.SaveChangesAsync();
        await LogAsync(AuditActions.ShelterUpdated, "Shelter", shelter.Id.ToString(), $"Shelter {shelter.Name} was updated.");
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var shelter = await context.Shelters.FindAsync(id);
        if (shelter is null)
        {
            return;
        }

        context.Shelters.Remove(shelter);
        await context.SaveChangesAsync();
        await LogAsync(AuditActions.ShelterUpdated, "Shelter", shelter.Id.ToString(), $"Shelter {shelter.Name} was deleted.");
    }

    public async Task<List<Shelter>> GetAllSheltersAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Shelters
            .Include(s => s.Dogs)
            .Include(s => s.ResourceStocks)
            .AsSplitQuery()
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Shelter?> GetShelterForUserAsync(string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Shelters
            .Include(s => s.Dogs)
            .Include(s => s.ResourceStocks)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ApplicationUserId == userId);
    }

    public async Task UpdateShelterProfileAsync(Shelter shelter)
    {
        ValidateShelterProfile(shelter);

        await using var context = await contextFactory.CreateDbContextAsync();

        var existingShelter = await context.Shelters.FirstOrDefaultAsync(s => s.Id == shelter.Id);
        if (existingShelter is null)
        {
            throw new InvalidOperationException("Shelter was not found.");
        }

        await EnsureShelterEmailIsAvailableAsync(context, shelter.Email, shelter.Id);
        NormalizeShelterProfile(shelter);

        existingShelter.Name = shelter.Name;
        existingShelter.Description = shelter.Description;
        existingShelter.Address = shelter.Address;
        existingShelter.City = shelter.City;
        existingShelter.PhoneNumber = shelter.PhoneNumber;
        existingShelter.Email = shelter.Email;
        existingShelter.Latitude = shelter.Latitude;
        existingShelter.Longitude = shelter.Longitude;
        existingShelter.VisitStartTime = shelter.VisitStartTime;
        existingShelter.VisitEndTime = shelter.VisitEndTime;
        existingShelter.VisitsAllowedMonday = shelter.VisitsAllowedMonday;
        existingShelter.VisitsAllowedTuesday = shelter.VisitsAllowedTuesday;
        existingShelter.VisitsAllowedWednesday = shelter.VisitsAllowedWednesday;
        existingShelter.VisitsAllowedThursday = shelter.VisitsAllowedThursday;
        existingShelter.VisitsAllowedFriday = shelter.VisitsAllowedFriday;
        existingShelter.VisitsAllowedSaturday = shelter.VisitsAllowedSaturday;
        existingShelter.VisitsAllowedSunday = shelter.VisitsAllowedSunday;

        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ShelterUpdated,
            "Shelter",
            existingShelter.Id.ToString(),
            $"Shelter {existingShelter.Name} profile was updated.");
    }

    private static void ValidateShelterProfile(Shelter shelter)
    {
        if (string.IsNullOrWhiteSpace(shelter.Name))
        {
            throw new InvalidOperationException("Shelter name is required.");
        }

        if (string.IsNullOrWhiteSpace(shelter.Address))
        {
            throw new InvalidOperationException("Shelter address is required.");
        }

        if (string.IsNullOrWhiteSpace(shelter.City))
        {
            throw new InvalidOperationException("Shelter city is required.");
        }

        if (!string.IsNullOrWhiteSpace(shelter.Email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(shelter.Email))
        {
            throw new InvalidOperationException("Shelter email must be a valid email address.");
        }

        if (!string.IsNullOrWhiteSpace(shelter.PhoneNumber) && !new System.ComponentModel.DataAnnotations.PhoneAttribute().IsValid(shelter.PhoneNumber))
        {
            throw new InvalidOperationException("Shelter phone number must be valid.");
        }

        if (shelter.Latitude is < -90 or > 90)
        {
            throw new InvalidOperationException("Latitude must be between -90 and 90.");
        }

        if (shelter.Longitude is < -180 or > 180)
        {
            throw new InvalidOperationException("Longitude must be between -180 and 180.");
        }

        var visitStart = shelter.VisitStartTime ?? VisitSchedulingHelper.DefaultVisitStartTime;
        var visitEnd = shelter.VisitEndTime ?? VisitSchedulingHelper.DefaultVisitEndTime;
        if (visitStart >= visitEnd)
        {
            throw new InvalidOperationException("Visit start time must be before visit end time.");
        }
    }

    private static async Task EnsureShelterEmailIsAvailableAsync(ApplicationDbContext context, string? email, int shelterId)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var duplicateEmailExists = await context.Shelters.AnyAsync(s =>
            s.Id != shelterId &&
            s.Email != null &&
            s.Email.Trim().ToUpper() == normalizedEmail);

        if (duplicateEmailExists)
        {
            throw new InvalidOperationException("Another shelter already uses this email address.");
        }
    }

    private static void NormalizeShelterProfile(Shelter shelter)
    {
        shelter.Name = shelter.Name.Trim();
        shelter.City = shelter.City.Trim();
        shelter.Address = NormalizeAddressWithoutCity(shelter.Address, shelter.City);
        shelter.Description = string.IsNullOrWhiteSpace(shelter.Description) ? null : shelter.Description.Trim();
        shelter.PhoneNumber = string.IsNullOrWhiteSpace(shelter.PhoneNumber) ? null : shelter.PhoneNumber.Trim();
        shelter.Email = string.IsNullOrWhiteSpace(shelter.Email) ? null : shelter.Email.Trim();
        VisitSchedulingHelper.ApplyDefaultVisitingHours(shelter);
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

    private Task LogAsync(string action, string entityName, string? entityId, string description)
    {
        return auditLogService?.LogAsync(action, entityName, entityId, description) ?? Task.CompletedTask;
    }
}
