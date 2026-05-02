using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class MedicalRecordService(ApplicationDbContext context) : IMedicalRecordService
{
    public Task<List<MedicalRecord>> GetAllAsync()
    {
        return context.MedicalRecords
            .Include(m => m.Dog)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<MedicalRecord?> GetByIdAsync(int id)
    {
        return context.MedicalRecords
            .Include(m => m.Dog)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task CreateAsync(MedicalRecord medicalRecord)
    {
        context.MedicalRecords.Add(medicalRecord);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(MedicalRecord medicalRecord)
    {
        context.MedicalRecords.Update(medicalRecord);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var medicalRecord = await context.MedicalRecords.FindAsync(id);
        if (medicalRecord is null)
        {
            return;
        }

        context.MedicalRecords.Remove(medicalRecord);
        await context.SaveChangesAsync();
    }
}
