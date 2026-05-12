namespace PawConnect.Services;

public interface ICsvImportService
{
    Task<CsvImportResult> PreviewShelterResourcesImportAsync(Stream csvStream, int shelterId);

    Task<CsvImportResult> ImportShelterResourcesAsync(Stream csvStream, int shelterId);

    Task<CsvImportResult> PreviewShelterDogsImportAsync(Stream csvStream, int shelterId);

    Task<CsvImportResult> ImportShelterDogsAsync(Stream csvStream, int shelterId);

    Task<CsvImportResult> PreviewAdminShelterRequestsImportAsync(Stream csvStream);

    Task<CsvImportResult> ImportAdminShelterRequestsAsync(Stream csvStream);

    ExportFile GenerateShelterResourcesTemplate();

    ExportFile GenerateShelterDogsTemplate();

    ExportFile GenerateAdminShelterRequestsTemplate();
}
