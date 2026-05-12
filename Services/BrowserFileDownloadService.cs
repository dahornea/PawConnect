using Microsoft.JSInterop;

namespace PawConnect.Services;

public class BrowserFileDownloadService(IJSRuntime jsRuntime) : IBrowserFileDownloadService
{
    public async Task DownloadAsync(ExportFile file)
    {
        if (file.Content.Length == 0)
        {
            return;
        }

        await jsRuntime.InvokeVoidAsync(
            "pawConnect.downloadFileFromBase64",
            file.FileName,
            file.ContentType,
            Convert.ToBase64String(file.Content));
    }
}
