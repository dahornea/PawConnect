namespace PawConnect.Services;

public interface IExportService
{
    Task<ExportFile> GenerateUsersCsvAsync();

    Task<ExportFile> GenerateSheltersCsvAsync();

    Task<ExportFile> GenerateDogsCsvAsync();

    Task<ExportFile> GenerateAdoptionRequestsCsvAsync();

    Task<ExportFile> GenerateShelterRequestsCsvAsync();

    Task<ExportFile> GenerateAdoptionRequestsPdfAsync();

    Task<ExportFile> GenerateShelterRequestsPdfAsync();
}

public sealed record ExportFile(string FileName, string ContentType, byte[] Content);
