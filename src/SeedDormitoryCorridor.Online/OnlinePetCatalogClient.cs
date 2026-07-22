using System.Buffers.Binary;
using System.Net;
using System.Text.Json;

namespace SeedDormitoryCorridor.Online;

public sealed class OnlinePetCatalogClient
{
    public const long MaximumCatalogBytes = 2L * 1024 * 1024;
    public const long MaximumPreviewBytes = 4L * 1024 * 1024;
    public const int MaximumCatalogItems = 500;
    public const int MaximumPreviewDimension = 2048;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient httpClient;

    public OnlinePetCatalogClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<IReadOnlyList<OnlinePetCatalogItem>> GetCatalogAsync(Uri catalogUri, CancellationToken cancellationToken = default)
    {
        OnlinePetContract.EnsureHttps(catalogUri, "catalog.url");
        using HttpResponseMessage response = await SendAsync(catalogUri, "catalog.network", cancellationToken).ConfigureAwait(false);
        EnsureSuccessfulResponse(response, "catalog.http");
        OnlinePetContract.EnsureHttps(response.RequestMessage?.RequestUri ?? catalogUri, "catalog.redirect");
        byte[] jsonBytes = await ReadLimitedAsync(
            response.Content,
            MaximumCatalogBytes,
            "catalog.size",
            "catalog.network",
            cancellationToken).ConfigureAwait(false);

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonBytes);
            JsonElement root = document.RootElement;
            JsonElement petsElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                petsElement = root;
            }
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("pets", out JsonElement pets) && pets.ValueKind == JsonValueKind.Array)
            {
                petsElement = pets;
            }
            else
            {
                throw new OnlinePetLibraryException("catalog.shape", "在线宠物目录必须是数组或包含 pets 数组的对象。");
            }

            if (petsElement.GetArrayLength() > MaximumCatalogItems)
            {
                throw new OnlinePetLibraryException("catalog.items.limit", $"在线宠物目录不能超过 {MaximumCatalogItems} 项。");
            }

            List<OnlinePetCatalogItem> items = JsonSerializer.Deserialize<List<OnlinePetCatalogItem>>(petsElement.GetRawText(), JsonOptions) ?? [];
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (OnlinePetCatalogItem item in items)
            {
                OnlinePetContract.ValidateItem(item);
                if (!ids.Add(item.Id))
                {
                    throw new OnlinePetLibraryException("catalog.id.duplicate", $"在线宠物目录包含重复 id '{item.Id}'。");
                }
            }

            return items;
        }
        catch (JsonException exception)
        {
            throw new OnlinePetLibraryException("catalog.json", $"在线宠物目录不是有效 JSON：{exception.Message}", exception);
        }
    }

    public async Task<OnlinePetPreview> GetPreviewAsync(OnlinePetCatalogItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        OnlinePetContract.ValidateItem(item);
        var previewUri = new Uri(item.PreviewUrl, UriKind.Absolute);
        using HttpResponseMessage response = await SendAsync(previewUri, "preview.network", cancellationToken).ConfigureAwait(false);
        EnsureSuccessfulResponse(response, "preview.http");
        OnlinePetContract.EnsureHttps(response.RequestMessage?.RequestUri ?? previewUri, "preview.redirect");
        byte[] bytes = await ReadLimitedAsync(
            response.Content,
            MaximumPreviewBytes,
            "preview.size",
            "preview.network",
            cancellationToken).ConfigureAwait(false);
        (int width, int height) = ReadPngDimensions(bytes);
        return new OnlinePetPreview(bytes, width, height);
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, string code, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new OnlinePetLibraryException(code, exception.Message, exception);
        }
    }

    private static void EnsureSuccessfulResponse(HttpResponseMessage response, string code)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new OnlinePetLibraryException(code, $"服务器返回 HTTP {(int)response.StatusCode} ({response.StatusCode})。");
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(
        HttpContent content,
        long maximumBytes,
        string sizeCode,
        string networkCode,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long contentLength && contentLength > maximumBytes)
        {
            throw new OnlinePetLibraryException(sizeCode, $"响应大小超过 {maximumBytes} 字节限制。");
        }

        try
        {
            await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            byte[] buffer = new byte[64 * 1024];
            long total = 0;
            while (true)
            {
                int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > maximumBytes)
                {
                    throw new OnlinePetLibraryException(sizeCode, $"响应大小超过 {maximumBytes} 字节限制。");
                }

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }
        catch (OnlinePetLibraryException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new OnlinePetLibraryException(networkCode, exception.Message, exception);
        }
    }

    private static (int Width, int Height) ReadPngDimensions(byte[] bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 24 || !bytes.AsSpan(0, signature.Length).SequenceEqual(signature) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new OnlinePetLibraryException("preview.png", "预览图必须是有效 PNG。");
        }

        uint width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        uint height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (width == 0 || height == 0 || width > MaximumPreviewDimension || height > MaximumPreviewDimension ||
            (long)width * height > MaximumPreviewDimension * MaximumPreviewDimension)
        {
            throw new OnlinePetLibraryException(
                "preview.dimensions",
                $"预览图尺寸必须在 1x1 到 {MaximumPreviewDimension}x{MaximumPreviewDimension} 之间。");
        }

        return ((int)width, (int)height);
    }
}

internal static class OnlinePetContract
{
    internal const long MaximumPackageBytes = 128L * 1024 * 1024;

    internal static void ValidateItem(OnlinePetCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 80 ||
            item.Id.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new OnlinePetLibraryException("catalog.item.id", "在线宠物 id 无效。");
        }

        if (string.IsNullOrWhiteSpace(item.DisplayName) || item.DisplayName.Length > 120 ||
            string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 2_000 ||
            item.Author?.Length > 120 || string.IsNullOrWhiteSpace(item.Version) || item.Version.Length > 64)
        {
            throw new OnlinePetLibraryException("catalog.item.metadata", $"在线宠物 '{item.Id}' 的名称、描述、作者或版本无效。");
        }

        if (string.IsNullOrWhiteSpace(item.PreviewUrl) || item.PreviewUrl.Length > 2_048 ||
            string.IsNullOrWhiteSpace(item.PackageUrl) || item.PackageUrl.Length > 2_048 ||
            string.IsNullOrWhiteSpace(item.MinimumClientVersion) || item.MinimumClientVersion.Length > 64)
        {
            throw new OnlinePetLibraryException("catalog.item.metadata", $"在线宠物 '{item.Id}' 的 URL 或最低客户端版本过长。");
        }

        EnsureHttps(ParseUri(item.PreviewUrl, item.Id, "previewUrl"), "catalog.item.preview-url");
        EnsureHttps(ParseUri(item.PackageUrl, item.Id, "packageUrl"), "catalog.item.package-url");
        if (string.IsNullOrWhiteSpace(item.Sha256) || item.Sha256.Length != 64 ||
            item.Sha256.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new OnlinePetLibraryException("catalog.item.sha256", $"在线宠物 '{item.Id}' 的 SHA-256 无效。");
        }

        if (item.PackageSize <= 0 || item.PackageSize > MaximumPackageBytes)
        {
            throw new OnlinePetLibraryException("catalog.item.package-size", $"在线宠物 '{item.Id}' 的包大小无效。");
        }

        if (item.SpriteVersionNumber is not (1 or 2))
        {
            throw new OnlinePetLibraryException("catalog.item.sprite-version", $"在线宠物 '{item.Id}' 的 spriteVersionNumber 无效。");
        }

        if (!OnlinePetCompatibility.TryParseVersion(item.MinimumClientVersion, out _))
        {
            throw new OnlinePetLibraryException("catalog.item.minimum-version", $"在线宠物 '{item.Id}' 的 minimumClientVersion 无效。");
        }

        if (item.UpdatedAt == default)
        {
            throw new OnlinePetLibraryException("catalog.item.updated-at", $"在线宠物 '{item.Id}' 的 updatedAt 无效。");
        }
    }

    internal static void EnsureHttps(Uri uri, string code)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new OnlinePetLibraryException(code, "在线宠物地址必须是无凭据的绝对 HTTPS URL。");
        }
    }

    private static Uri ParseUri(string value, string id, string field)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            throw new OnlinePetLibraryException("catalog.item.url", $"在线宠物 '{id}' 的 {field} 无效。");
        }

        return uri;
    }
}
