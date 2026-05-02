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
            .Include(r => r.AdopterUser)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<AdoptionRequest>> GetForAdopterAsync(string userId)
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .Where(r => r.AdopterUserId == userId)
            .AsNoTracking()
            .ToListAsync();
    }
}
