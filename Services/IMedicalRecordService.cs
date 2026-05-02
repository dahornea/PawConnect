using PawConnect.Entities;

namespace PawConnect.Services;

public interface IMedicalRecordService
{
    Task<List<MedicalRecord>> GetAllAsync();

    Task<MedicalRecord?> GetByIdAsync(int id);

    Task CreateAsync(MedicalRecord medicalRecord);

    Task UpdateAsync(MedicalRecord medicalRecord);

    Task DeleteAsync(int id);

    Task<List<MedicalRecord>> GetMedicalRecordsForDogAsync(int dogId);

    Task AddMedicalRecordAsync(int dogId, int shelterId, MedicalRecord record);

    Task UpdateMedicalRecordAsync(int shelterId, MedicalRecord record);

    Task DeleteMedicalRecordAsync(int recordId, int shelterId);
}
