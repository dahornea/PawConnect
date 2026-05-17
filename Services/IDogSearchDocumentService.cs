using PawConnect.Entities;

namespace PawConnect.Services;

public interface IDogSearchDocumentService
{
    string BuildDocument(Dog dog);

    string ComputeContentHash(string content);
}
