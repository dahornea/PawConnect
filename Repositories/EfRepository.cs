using Microsoft.EntityFrameworkCore;
using PawConnect.Data;

namespace PawConnect.Repositories;

public class EfRepository<T>(ApplicationDbContext context) : IGenericRepository<T> where T : class
{
    public Task<List<T>> GetAllAsync()
    {
        return context.Set<T>().AsNoTracking().ToListAsync();
    }

    public Task<T?> GetByIdAsync(int id)
    {
        return context.Set<T>().FindAsync(id).AsTask();
    }

    public async Task AddAsync(T entity)
    {
        context.Set<T>().Add(entity);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        context.Set<T>().Update(entity);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        context.Set<T>().Remove(entity);
        await context.SaveChangesAsync();
    }
}
