using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using SeedDormitoryCorridor.Online;

namespace SeedDormitoryCorridor.Online.Tests;

internal static class TestSupport
{
    internal static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new StubHttpMessageHandler(responseFactory));

    internal static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    internal static HttpResponseMessage BinaryResponse(byte[] bytes) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(bytes),
    };

    internal static HttpResponseMessage StreamResponse(Stream stream, long contentLength)
    {
        var content = new StreamContent(stream);
        content.Headers.ContentLength = contentLength;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    internal static OnlinePetCatalogItem CreateCatalogItem(byte[] package, string? sha256 = null) => new()
    {
        Id = "builtin-seed",
        DisplayName = "走廊种子",
        Description = "测试在线宠物",
        Author = "Test",
        Version = "1.0.0",
        PreviewUrl = "https://catalog.example/preview.png",
        PackageUrl = "https://catalog.example/package.zip",
        Sha256 = sha256 ?? Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(),
        PackageSize = package.Length,
        SpriteVersionNumber = 1,
        MinimumClientVersion = "0.1.0",
        UpdatedAt = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero),
    };

    internal static OnlinePetCatalogItem CopyCatalogItem(OnlinePetCatalogItem item, string? minimumClientVersion = null) => new()
    {
        Id = item.Id,
        DisplayName = item.DisplayName,
        Description = item.Description,
        Author = item.Author,
        Version = item.Version,
        PreviewUrl = item.PreviewUrl,
        PackageUrl = item.PackageUrl,
        Sha256 = item.Sha256,
        PackageSize = item.PackageSize,
        SpriteVersionNumber = item.SpriteVersionNumber,
        MinimumClientVersion = minimumClientVersion ?? item.MinimumClientVersion,
        UpdatedAt = item.UpdatedAt,
    };

    internal static byte[] CreateValidPackage()
    {
        string source = Path.Combine(FindRepositoryRoot(), "assets", "builtin-seed");
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                ZipArchiveEntry entry = archive.CreateEntry(relative, CompressionLevel.NoCompression);
                using Stream destination = entry.Open();
                using FileStream input = File.OpenRead(file);
                input.CopyTo(destination);
            }
        }

        return output.ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "assets", "builtin-seed")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}

internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = responseFactory(request);
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }
}

internal sealed class TestDirectory : IDisposable
{
    internal TestDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "SeedDormitoryCorridor.Online.Tests", Guid.NewGuid().ToString("N"));
        Pets = Path.Combine(Root, "Pets");
        Staging = Path.Combine(Root, "Staging");
        Directory.CreateDirectory(Pets);
        Directory.CreateDirectory(Staging);
    }

    internal string Root { get; }
    internal string Pets { get; }
    internal string Staging { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
