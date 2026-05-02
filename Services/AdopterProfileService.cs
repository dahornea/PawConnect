using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class AdopterProfileService(ApplicationDbContext context) : IAdopterProfileService
{
    public Task<AdopterProfile?> GetProfileForUserAsync(string userId)
    {
        return context.AdopterProfiles
            .Include(p => p.ApplicationUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ApplicationUserId == userId);
    }

    public async Task CreateOrUpdateProfileAsync(string userId, AdopterProfile profile)
    {
        ValidateProfile(profile);

        var existingProfile = await context.AdopterProfiles.FirstOrDefaultAsync(p => p.ApplicationUserId == userId);
        if (existingProfile is null)
        {
            profile.Id = 0;
            profile.ApplicationUserId = userId;
            profile.ApplicationUser = null;
            NormalizeProfile(profile);

            context.AdopterProfiles.Add(profile);
        }
        else
        {
            existingProfile.FullName = profile.FullName.Trim();
            existingProfile.ProfileImageUrl = string.IsNullOrWhiteSpace(profile.ProfileImageUrl) ? null : profile.ProfileImageUrl.Trim();
            existingProfile.Address = string.IsNullOrWhiteSpace(profile.Address) ? null : profile.Address.Trim();
            existingProfile.City = profile.City.Trim();
            existingProfile.PhoneNumber = string.IsNullOrWhiteSpace(profile.PhoneNumber) ? null : profile.PhoneNumber.Trim();
            existingProfile.HousingType = profile.HousingType;
            existingProfile.HasYard = profile.HasYard;
            existingProfile.HasOtherPets = profile.HasOtherPets;
            existingProfile.HasChildren = profile.HasChildren;
            existingProfile.ExperienceWithDogs = string.IsNullOrWhiteSpace(profile.ExperienceWithDogs) ? null : profile.ExperienceWithDogs.Trim();
            existingProfile.AdditionalNotes = string.IsNullOrWhiteSpace(profile.AdditionalNotes) ? null : profile.AdditionalNotes.Trim();
        }

        await context.SaveChangesAsync();
    }

    private static void ValidateProfile(AdopterProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FullName))
        {
            throw new InvalidOperationException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.City))
        {
            throw new InvalidOperationException("City is required.");
        }

        if (!string.IsNullOrWhiteSpace(profile.PhoneNumber) &&
            !new System.ComponentModel.DataAnnotations.PhoneAttribute().IsValid(profile.PhoneNumber))
        {
            throw new InvalidOperationException("Phone number must be valid.");
        }

        if (!string.IsNullOrWhiteSpace(profile.ProfileImageUrl) &&
            !new System.ComponentModel.DataAnnotations.UrlAttribute().IsValid(profile.ProfileImageUrl))
        {
            throw new InvalidOperationException("Profile image URL must be valid.");
        }
    }

    private static void NormalizeProfile(AdopterProfile profile)
    {
        profile.FullName = profile.FullName.Trim();
        profile.ProfileImageUrl = string.IsNullOrWhiteSpace(profile.ProfileImageUrl) ? null : profile.ProfileImageUrl.Trim();
        profile.Address = string.IsNullOrWhiteSpace(profile.Address) ? null : profile.Address.Trim();
        profile.City = profile.City.Trim();
        profile.PhoneNumber = string.IsNullOrWhiteSpace(profile.PhoneNumber) ? null : profile.PhoneNumber.Trim();
        profile.ExperienceWithDogs = string.IsNullOrWhiteSpace(profile.ExperienceWithDogs) ? null : profile.ExperienceWithDogs.Trim();
        profile.AdditionalNotes = string.IsNullOrWhiteSpace(profile.AdditionalNotes) ? null : profile.AdditionalNotes.Trim();
    }
}
