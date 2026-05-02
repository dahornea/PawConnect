using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class AdoptionRequestService(ApplicationDbContext context) : IAdoptionRequestService
{
    public Task<List<AdoptionRequest>> GetAllAsync()
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .Include(r => r.Adopter)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<AdoptionRequest?> GetByIdAsync(int id)
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .Include(r => r.Adopter)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task CreateAsync(AdoptionRequest adoptionRequest)
    {
        context.AdoptionRequests.Add(adoptionRequest);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AdoptionRequest adoptionRequest)
    {
        adoptionRequest.UpdatedAt = DateTime.UtcNow;
        context.AdoptionRequests.Update(adoptionRequest);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var adoptionRequest = await context.AdoptionRequests.FindAsync(id);
        if (adoptionRequest is null)
        {
            return;
        }

        context.AdoptionRequests.Remove(adoptionRequest);
        await context.SaveChangesAsync();
    }

    public Task<List<AdoptionRequest>> GetForAdopterAsync(string userId)
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .Where(r => r.AdopterId == userId)
            .AsNoTracking()
            .ToListAsync();
    }
}
