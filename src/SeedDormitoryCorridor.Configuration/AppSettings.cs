namespace SeedDormitoryCorridor.Configuration;

public enum RenderMode
{
    Smooth,
    Pixelated,
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    public string? CurrentPetId { get; set; }

    public bool PetVisible { get; set; } = true;

    public bool AnimationPaused { get; set; }

    public bool TopMost { get; set; } = true;

    public bool ClickThrough { get; set; }

    public bool StartWithWindows { get; set; }

    public bool ShowOnStartup { get; set; } = true;

    public float Scale { get; set; } = 1.0f;

    public RenderMode RenderMode { get; set; } = RenderMode.Smooth;

    public byte AlphaThreshold { get; set; } = 16;

    public string IdleFrequency { get; set; } = "normal";

    public int X { get; set; } = int.MinValue;

    public int Y { get; set; } = int.MinValue;

    public string? MonitorDeviceName { get; set; }

    public Dictionary<string, PetOverrides> PetOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PetOverrides
{
    public float? Scale { get; set; }

    public RenderMode? RenderMode { get; set; }

    public byte? AlphaThreshold { get; set; }

    public Dictionary<string, string>? Behavior { get; set; }
}
