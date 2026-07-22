using System.Text.Json.Serialization;

namespace SeedDormitoryCorridor.Online;

public sealed class OnlinePetCatalogItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("previewUrl")]
    public string PreviewUrl { get; init; } = string.Empty;

    [JsonPropertyName("packageUrl")]
    public string PackageUrl { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("packageSize")]
    public long PackageSize { get; init; }

    [JsonPropertyName("spriteVersionNumber")]
    public int SpriteVersionNumber { get; init; }

    [JsonPropertyName("minimumClientVersion")]
    public string MinimumClientVersion { get; init; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record OnlinePetPreview(byte[] PngBytes, int Width, int Height);

public sealed class OnlinePetLibraryException : Exception
{
    public OnlinePetLibraryException(
        string code,
        string message,
        Exception? innerException = null,
        IReadOnlyList<Assets.ValidationIssue>? validationIssues = null)
        : base(message, innerException)
    {
        Code = code;
        ValidationIssues = validationIssues ?? [];
    }

    public string Code { get; }

    public IReadOnlyList<Assets.ValidationIssue> ValidationIssues { get; }
}

public static class OnlinePetCompatibility
{
    public static bool IsCompatible(OnlinePetCatalogItem item, Version clientVersion)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(clientVersion);
        return TryParseVersion(item.MinimumClientVersion, out Version? minimum) && clientVersion >= minimum;
    }

    internal static bool TryParseVersion(string value, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        int suffix = value.IndexOfAny(['-', '+']);
        string numeric = suffix >= 0 ? value[..suffix] : value;
        return Version.TryParse(numeric, out version);
    }
}
