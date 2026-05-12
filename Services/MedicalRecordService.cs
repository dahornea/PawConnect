using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class MedicalRecordService(ApplicationDbContext context, IAuditLogService? auditLogService = null) : IMedicalRecordService
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

    public Task<List<MedicalRecord>> GetMedicalRecordsForDogAsync(int dogId)
    {
        return context.MedicalRecords
            .Where(m => m.DogId == dogId)
            .OrderByDescending(m => m.RecordDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddMedicalRecordAsync(int dogId, int shelterId, MedicalRecord record)
    {
        var dog = await EnsureDogCanBeManagedAsync(dogId, shelterId);
        ValidateMedicalRecord(record);

        record.Id = 0;
        record.DogId = dogId;
        record.Dog = null;

        context.MedicalRecords.Add(record);
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.MedicalRecordAdded,
            "MedicalRecord",
            record.Id.ToString(),
            $"Medical record was added for dog {dog.Name}.",
            additionalData: $"DogId={dogId};ShelterId={shelterId}");
    }

    public async Task UpdateMedicalRecordAsync(int shelterId, MedicalRecord record)
    {
        ValidateMedicalRecord(record);

        var existingRecord = await context.MedicalRecords
            .Include(m => m.Dog)
            .FirstOrDefaultAsync(m => m.Id == record.Id);

        if (existingRecord?.Dog is null || existingRecord.Dog.ShelterId != shelterId)
        {
            throw new InvalidOperationException("Medical record was not found for your shelter.");
        }

        EnsureDogIsNotAdopted(existingRecord.Dog);

        existingRecord.VaccineName = string.IsNullOrWhiteSpace(record.VaccineName) ? null : record.VaccineName.Trim();
        existingRecord.TreatmentDescription = string.IsNullOrWhiteSpace(record.TreatmentDescription) ? null : record.TreatmentDescription.Trim();
        existingRecord.RecordDate = record.RecordDate;
        existingRecord.Notes = string.IsNullOrWhiteSpace(record.Notes) ? null : record.Notes.Trim();

        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.MedicalRecordUpdated,
            "MedicalRecord",
            existingRecord.Id.ToString(),
            $"Medical record was updated for dog {existingRecord.Dog.Name}.",
            additionalData: $"DogId={existingRecord.DogId};ShelterId={shelterId}");
    }

    public async Task DeleteMedicalRecordAsync(int recordId, int shelterId)
    {
        var record = await context.MedicalRecords
            .Include(m => m.Dog)
            .FirstOrDefaultAsync(m => m.Id == recordId);

        if (record?.Dog is null || record.Dog.ShelterId != shelterId)
        {
            throw new InvalidOperationException("Medical record was not found for your shelter.");
        }

        EnsureDogIsNotAdopted(record.Dog);

        context.MedicalRecords.Remove(record);
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.MedicalRecordDeleted,
            "MedicalRecord",
            recordId.ToString(),
            $"Medical record was deleted for dog {record.Dog.Name}.",
            additionalData: $"DogId={record.DogId};ShelterId={shelterId}");
    }

    private async Task<Dog> EnsureDogCanBeManagedAsync(int dogId, int shelterId)
    {
        var dog = await context.Dogs.FirstOrDefaultAsync(d => d.Id == dogId && d.ShelterId == shelterId);
        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        EnsureDogIsNotAdopted(dog);
        return dog;
    }

    private static void EnsureDogIsNotAdopted(Dog dog)
    {
        if (dog.Status == DogStatus.Adopted)
        {
            throw new InvalidOperationException("Adopted dogs are read-only for shelter users.");
        }
    }

    private static void ValidateMedicalRecord(MedicalRecord record)
    {
        if (record.RecordDate == default)
        {
            throw new InvalidOperationException("Record date is required.");
        }
    }

    private Task LogAsync(string action, string entityName, string? entityId, string description, string? additionalData = null)
    {
        return auditLogService?.LogAsync(action, entityName, entityId, description, additionalData: additionalData) ?? Task.CompletedTask;
    }
}
