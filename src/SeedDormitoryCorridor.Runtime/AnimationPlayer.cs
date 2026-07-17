namespace SeedDormitoryCorridor.Runtime;

public readonly record struct AnimationFrameState(string AnimationName, int Row, int Column, int RemainingMs);

public sealed class AnimationCompletedEventArgs(string animationName) : EventArgs
{
    public string AnimationName { get; } = animationName;
}

/// <summary>Advances animations from caller-provided monotonic timestamps.</summary>
public sealed class AnimationPlayer
{
    private readonly AnimationCatalog catalog;
    private AnimationDefinition current;
    private long frameStartedAtMs;
    private long pausedAtMs;
    private int frameIndex;
    private int completedLoops;
    private AnimationPlaybackMode mode;
    private int targetLoops;
    private int activePriority;
    private string? nextAnimation;

    public AnimationPlayer(AnimationCatalog catalog, string defaultAnimation, long initialTimestampMs = 0)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        DefaultAnimation = defaultAnimation;
        current = catalog[defaultAnimation];
        frameStartedAtMs = initialTimestampMs;
        ApplyDefaults(current);
    }

    public event EventHandler<AnimationCompletedEventArgs>? AnimationCompleted;

    public string DefaultAnimation { get; }

    public bool IsPaused { get; private set; }

    public AnimationFrameState State => new(current.Name, current.Row, frameIndex,
        Math.Max(1, current.FrameDurationsMs[frameIndex]));

    public int ActivePriority => activePriority;

    public bool Play(
        string animationName,
        long timestampMs,
        int? priority = null,
        bool force = false,
        bool restart = true,
        AnimationPlaybackMode? playbackMode = null,
        int? loopCount = null,
        string? afterAnimation = null)
    {
        AnimationDefinition requested = catalog[animationName];
        int requestedPriority = priority ?? requested.Priority;
        if (!force && requestedPriority < activePriority)
        {
            return false;
        }

        if (!restart && string.Equals(current.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        current = requested;
        frameIndex = 0;
        completedLoops = 0;
        frameStartedAtMs = timestampMs;
        activePriority = requestedPriority;
        mode = playbackMode ?? requested.DefaultMode;
        targetLoops = loopCount ?? requested.DefaultLoopCount;
        nextAnimation = afterAnimation ?? requested.DefaultNextAnimation;
        if (mode == AnimationPlaybackMode.Count && targetLoops < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(loopCount), "Count playback requires at least one loop.");
        }

        return true;
    }

    public bool Update(long timestampMs)
    {
        if (IsPaused || timestampMs < frameStartedAtMs)
        {
            return false;
        }

        bool changed = false;
        int guard = 0;
        while (timestampMs - frameStartedAtMs >= current.FrameDurationsMs[frameIndex])
        {
            frameStartedAtMs += current.FrameDurationsMs[frameIndex];
            frameIndex++;
            changed = true;
            if (frameIndex < current.FrameCount)
            {
                continue;
            }

            frameIndex = 0;
            completedLoops++;
            if (mode == AnimationPlaybackMode.Loop ||
                (mode == AnimationPlaybackMode.Count && completedLoops < targetLoops))
            {
                continue;
            }

            CompleteCurrent(frameStartedAtMs);
            if (++guard > 32)
            {
                throw new InvalidOperationException("Animation completion chain exceeded its safety limit.");
            }
        }

        return changed;
    }

    public int GetRemainingFrameTimeMs(long timestampMs)
    {
        if (IsPaused)
        {
            return current.FrameDurationsMs[frameIndex];
        }

        long elapsed = Math.Max(0, timestampMs - frameStartedAtMs);
        return Math.Max(1, current.FrameDurationsMs[frameIndex] - (int)Math.Min(int.MaxValue, elapsed));
    }

    public void Pause(long timestampMs)
    {
        if (!IsPaused)
        {
            pausedAtMs = timestampMs;
            IsPaused = true;
        }
    }

    public void Resume(long timestampMs)
    {
        if (IsPaused)
        {
            frameStartedAtMs += Math.Max(0, timestampMs - pausedAtMs);
            IsPaused = false;
        }
    }

    public void RestoreDefault(long timestampMs, bool force = true) =>
        Play(DefaultAnimation, timestampMs, priority: 0, force: force, restart: true);

    private void CompleteCurrent(long timestampMs)
    {
        string completed = current.Name;
        string destination = nextAnimation ?? DefaultAnimation;
        AnimationCompleted?.Invoke(this, new AnimationCompletedEventArgs(completed));
        current = catalog[destination];
        frameIndex = 0;
        completedLoops = 0;
        frameStartedAtMs = timestampMs;
        ApplyDefaults(current);
    }

    private void ApplyDefaults(AnimationDefinition definition)
    {
        mode = definition.DefaultMode;
        targetLoops = definition.DefaultLoopCount;
        activePriority = definition.Priority;
        nextAnimation = definition.DefaultNextAnimation;
    }
}
