namespace PawConnect.Services;

public interface IBrowserFileDownloadService
{
    Task DownloadAsync(ExportFile file);
}
