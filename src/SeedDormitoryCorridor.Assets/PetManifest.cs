using System.Text.Json.Serialization;

namespace SeedDormitoryCorridor.Assets;

public sealed class PetManifest
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("spritesheetPath")]
    public string? SpritesheetPath { get; set; }

    [JsonPropertyName("desktopPet")]
    public DesktopPetManifestOptions? DesktopPet { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class DesktopPetManifestOptions
{
    [JsonPropertyName("profile")]
    public string? Profile { get; set; }

    [JsonPropertyName("defaultScale")]
    public float? DefaultScale { get; set; }

    [JsonPropertyName("renderMode")]
    public string? RenderMode { get; set; }

    [JsonPropertyName("alphaThreshold")]
    public int? AlphaThreshold { get; set; }

    [JsonPropertyName("behavior")]
    public PetBehaviorManifest? Behavior { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class PetBehaviorManifest
{
    public string? OnShow { get; set; }
    public string? OnSingleClick { get; set; }
    public string? OnDoubleClick { get; set; }
    public string? OnDragLeft { get; set; }
    public string? OnDragRight { get; set; }
    public string? AfterInteraction { get; set; }
}
