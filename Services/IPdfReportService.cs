namespace PawConnect.Services;

public interface IPdfReportService
{
    Task<byte[]> GenerateAdoptionRequestReportAsync(int adoptionRequestId);

    Task<byte[]> GenerateAdoptionStatusReportAsync(int adoptionRequestId);

    Task<byte[]> GenerateLowStockResourceReportAsync(int resourceStockId);
}
