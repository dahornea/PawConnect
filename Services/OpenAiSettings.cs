namespace PawConnect.Services;

public class OpenAiSettings
{
    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-5.4-mini";

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    public string GetSafeModel()
    {
        return string.IsNullOrWhiteSpace(Model) ? "gpt-5.4-mini" : Model.Trim();
    }
}
