namespace SeedDormitoryCorridor.Runtime;

public enum IdleFrequency
{
    Off,
    Low,
    Normal,
    High,
}

public sealed record IdleCandidate(string AnimationName, int Weight, int CooldownMs);

public interface IRandomSource
{
    int NextInt(int exclusiveMaximum);
}

public sealed class SystemRandomSource : IRandomSource
{
    public int NextInt(int exclusiveMaximum) => Random.Shared.Next(exclusiveMaximum);
}

/// <summary>Schedules bounded, weighted special idle animations without direct UI dependencies.</summary>
public sealed class IdleScheduler
{
    private readonly IReadOnlyList<IdleCandidate> candidates;
    private readonly IRandomSource random;
    private readonly Dictionary<string, long> lastPlayed = new(StringComparer.OrdinalIgnoreCase);
    private long lastInteractionMs;
    private string? previous;

    public IdleScheduler(IEnumerable<IdleCandidate> candidates, IRandomSource? random = null)
    {
        this.candidates = candidates.Where(item => item.Weight > 0 && item.CooldownMs >= 0).ToArray();
        this.random = random ?? new SystemRandomSource();
    }

    public IdleFrequency Frequency { get; set; } = IdleFrequency.Normal;

    public void Reset(long timestampMs) => lastInteractionMs = timestampMs;

    public string? TrySchedule(long timestampMs, bool interactionBlocked)
    {
        int interval = Frequency switch
        {
            IdleFrequency.Off => int.MaxValue,
            IdleFrequency.Low => 60_000,
            IdleFrequency.Normal => 30_000,
            IdleFrequency.High => 15_000,
            _ => 30_000,
        };

        if (interactionBlocked || timestampMs - lastInteractionMs < interval)
        {
            return null;
        }

        IdleCandidate[] eligible = candidates.Where(candidate =>
            !string.Equals(candidate.AnimationName, previous, StringComparison.OrdinalIgnoreCase) &&
            (!lastPlayed.TryGetValue(candidate.AnimationName, out long last) || timestampMs - last >= candidate.CooldownMs)).ToArray();

        if (eligible.Length == 0)
        {
            lastInteractionMs = timestampMs;
            return null;
        }

        int totalWeight = eligible.Sum(item => item.Weight);
        int pick = random.NextInt(totalWeight);
        IdleCandidate selected = eligible[0];
        foreach (IdleCandidate candidate in eligible)
        {
            if (pick < candidate.Weight)
            {
                selected = candidate;
                break;
            }

            pick -= candidate.Weight;
        }

        previous = selected.AnimationName;
        lastPlayed[selected.AnimationName] = timestampMs;
        lastInteractionMs = timestampMs;
        return selected.AnimationName;
    }
}
