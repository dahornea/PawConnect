namespace PawConnect.Services;

public static class ReportHistoryTypes
{
    public const string ShelterSummaryReport = nameof(ShelterSummaryReport);
    public const string AdminPlatformSummaryReport = nameof(AdminPlatformSummaryReport);
    public const string AdoptionRequestReport = nameof(AdoptionRequestReport);
    public const string AdoptionStatusReport = nameof(AdoptionStatusReport);
    public const string LowStockResourceReport = nameof(LowStockResourceReport);
    public const string ShelterRegistrationRequestReport = nameof(ShelterRegistrationRequestReport);
    public const string CsvExport = nameof(CsvExport);
    public const string PdfExport = nameof(PdfExport);
}

public static class ReportHistoryTriggers
{
    public const string Manual = nameof(Manual);
    public const string Quartz = nameof(Quartz);
    public const string System = nameof(System);
    public const string Admin = nameof(Admin);
    public const string Shelter = nameof(Shelter);
}
