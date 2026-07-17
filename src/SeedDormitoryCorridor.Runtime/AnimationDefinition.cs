namespace SeedDormitoryCorridor.Runtime;

public enum AnimationPlaybackMode
{
    Loop,
    Once,
    Count,
}

/// <summary>Immutable animation metadata independent of UI and rendering technology.</summary>
public sealed record AnimationDefinition(
    string Name,
    int Row,
    IReadOnlyList<int> FrameDurationsMs,
    AnimationPlaybackMode DefaultMode = AnimationPlaybackMode.Loop,
    int DefaultLoopCount = 0,
    string? DefaultNextAnimation = null,
    int Priority = 0)
{
    public int FrameCount => FrameDurationsMs.Count;

    public int DurationMs => FrameDurationsMs.Sum();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Animation name cannot be empty.", nameof(Name));
        }

        if (Row < 0 || FrameDurationsMs.Count == 0 || FrameDurationsMs.Any(duration => duration <= 0))
        {
            throw new ArgumentException($"Animation '{Name}' has invalid row or frame durations.");
        }

        if (DefaultMode == AnimationPlaybackMode.Count && DefaultLoopCount < 1)
        {
            throw new ArgumentException($"Animation '{Name}' requires a positive loop count.");
        }
    }
}

public sealed class AnimationCatalog
{
    private readonly IReadOnlyDictionary<string, AnimationDefinition> animations;

    public AnimationCatalog(IEnumerable<AnimationDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var dictionary = new Dictionary<string, AnimationDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (AnimationDefinition definition in definitions)
        {
            definition.Validate();
            if (!dictionary.TryAdd(definition.Name, definition))
            {
                throw new ArgumentException($"Duplicate animation name '{definition.Name}'.", nameof(definitions));
            }
        }

        if (dictionary.Count == 0)
        {
            throw new ArgumentException("At least one animation is required.", nameof(definitions));
        }

        animations = dictionary;
    }

    public IReadOnlyCollection<AnimationDefinition> All => animations.Values.ToArray();

    public AnimationDefinition this[string name] => animations.TryGetValue(name, out AnimationDefinition? value)
        ? value
        : throw new KeyNotFoundException($"Animation '{name}' was not found.");

    public bool TryGet(string name, out AnimationDefinition? definition) => animations.TryGetValue(name, out definition);
}
