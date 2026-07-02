using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class LocalDogImageStorageServiceTests
{
    [Fact]
    public async Task SaveDogImageAsync_StoresValidImageUnderPublicRelativePath()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var service = CreateService(tempRoot);
            var file = new TestBrowserFile("friendly-dog.jpg", "image/jpeg", [1, 2, 3]);

            var result = await service.SaveDogImageAsync(42, file);

            Assert.StartsWith("/uploads/dogs/42/", result.ImagePath);
            Assert.EndsWith(".jpg", result.ImagePath);
            Assert.DoesNotContain("friendly-dog", result.ImagePath, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(ToPhysicalPath(tempRoot, result.ImagePath)));
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task SaveDogImageAsync_RejectsUnsupportedExtension()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var service = CreateService(tempRoot);
            var file = new TestBrowserFile("dog.exe", "image/jpeg", [1, 2, 3]);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveDogImageAsync(42, file));

            Assert.Contains("Only", exception.Message);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task SaveDogImageAsync_RejectsOversizedFile()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var service = CreateService(tempRoot, maxFileSizeBytes: 2);
            var file = new TestBrowserFile("dog.png", "image/png", [1, 2, 3]);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveDogImageAsync(42, file));

            Assert.Contains("or smaller", exception.Message);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task DeleteDogImageAsync_RemovesLocalUploadedFile()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var service = CreateService(tempRoot);
            var file = new TestBrowserFile("dog.webp", "image/webp", [1, 2, 3]);
            var result = await service.SaveDogImageAsync(42, file);
            var physicalPath = ToPhysicalPath(tempRoot, result.ImagePath);

            await service.DeleteDogImageAsync(result.ImagePath);

            Assert.False(File.Exists(physicalPath));
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static LocalDogImageStorageService CreateService(string contentRoot, long maxFileSizeBytes = 1024)
    {
        var environment = new TestWebHostEnvironment(contentRoot);
        var options = Options.Create(new DogImageStorageOptions
        {
            LocalRoot = "uploads/dogs",
            MaxFileSizeBytes = maxFileSizeBytes,
            AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"]
        });

        return new LocalDogImageStorageService(environment, options);
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pawconnect-upload-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "wwwroot"));
        return tempRoot;
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string ToPhysicalPath(string tempRoot, string publicPath)
    {
        return Path.Combine(tempRoot, "wwwroot", publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class TestBrowserFile(string name, string contentType, byte[] content) : IBrowserFile
    {
        public string Name { get; } = name;

        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;

        public long Size => content.Length;

        public string ContentType { get; } = contentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            return new MemoryStream(content);
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PawConnect.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = "Development";

        public string WebRootPath { get; set; } = Path.Combine(contentRootPath, "wwwroot");

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
