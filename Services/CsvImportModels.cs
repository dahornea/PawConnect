namespace PawConnect.Services;

public class CsvImportResult
{
    public int TotalRows { get; set; }

    public int ValidRows => RowResults.Count(row => row.IsValid);

    public int InvalidRows => RowResults.Count(row => !row.IsValid);

    public int ImportedRows { get; set; }

    public bool HasErrors => RowResults.Any(row => !row.IsValid);

    public List<CsvImportRowResult> RowResults { get; set; } = [];
}

public class CsvImportRowResult
{
    public int RowNumber { get; set; }

    public bool IsValid => Errors.Count == 0;

    public List<CsvImportValidationError> Errors { get; set; } = [];

    public Dictionary<string, string?> PreviewData { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Action { get; set; } = CsvImportActions.Create;
}

public class CsvImportValidationError
{
    public string Field { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public static class CsvImportActions
{
    public const string Create = nameof(Create);
    public const string Update = nameof(Update);
    public const string Skip = nameof(Skip);
    public const string CreatePendingRequest = "Create Pending Request";
    public const string Duplicate = nameof(Duplicate);
}
