namespace PawConnect.Services;

public class OpenAiSettings
{
    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-5.4-mini";

    public string ChatModel { get; set; } = "gpt-5.4-mini";

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    public string GetSafeModel()
    {
        return GetSafeChatModel();
    }

    public string GetSafeChatModel()
    {
        if (!string.IsNullOrWhiteSpace(ChatModel))
        {
            return ChatModel.Trim();
        }

        return string.IsNullOrWhiteSpace(Model) ? "gpt-5.4-mini" : Model.Trim();
    }

    public string GetSafeEmbeddingModel()
    {
        return string.IsNullOrWhiteSpace(EmbeddingModel) ? "text-embedding-3-small" : EmbeddingModel.Trim();
    }
}
