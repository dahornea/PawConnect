using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ShelterService(ApplicationDbContext context) : IShelterService
{
    public Task<List<Shelter>> GetAllAsync()
    {
        return GetAllSheltersAsync();
    }

    public Task<Shelter?> GetByIdAsync(int id)
    {
        return context.Shelters
            .Include(s => s.Dogs)
            .Include(s => s.ResourceStocks)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task CreateAsync(Shelter shelter)
    {
        context.Shelters.Add(shelter);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Shelter shelter)
    {
        context.Shelters.Update(shelter);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var shelter = await context.Shelters.FindAsync(id);
        if (shelter is null)
        {
            return;
        }

        context.Shelters.Remove(shelter);
        await context.SaveChangesAsync();
    }

    public Task<List<Shelter>> GetAllSheltersAsync()
    {
        return context.Shelters
            .Include(s => s.Dogs)
            .Include(s => s.ResourceStocks)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<Shelter?> GetShelterForUserAsync(string userId)
    {
        return context.Shelters
            .Include(s => s.Dogs)
            .Include(s => s.ResourceStocks)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ApplicationUserId == userId);
    }

    public async Task UpdateShelterProfileAsync(Shelter shelter)
    {
        ValidateShelterProfile(shelter);

        var existingShelter = await context.Shelters.FirstOrDefaultAsync(s => s.Id == shelter.Id);
        if (existingShelter is null)
        {
            throw new InvalidOperationException("Shelter was not found.");
        }

        var duplicateEmailExists = !string.IsNullOrWhiteSpace(shelter.Email) &&
            await context.Shelters.AnyAsync(s => s.Id != shelter.Id && s.Email == shelter.Email);

        if (duplicateEmailExists)
        {
            throw new InvalidOperationException("Another shelter already uses this email address.");
        }

        existingShelter.Name = shelter.Name.Trim();
        existingShelter.Description = string.IsNullOrWhiteSpace(shelter.Description) ? null : shelter.Description.Trim();
        existingShelter.Address = shelter.Address.Trim();
        existingShelter.City = shelter.City.Trim();
        existingShelter.PhoneNumber = string.IsNullOrWhiteSpace(shelter.PhoneNumber) ? null : shelter.PhoneNumber.Trim();
        existingShelter.Email = string.IsNullOrWhiteSpace(shelter.Email) ? null : shelter.Email.Trim();

        await context.SaveChangesAsync();
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
    }
}
