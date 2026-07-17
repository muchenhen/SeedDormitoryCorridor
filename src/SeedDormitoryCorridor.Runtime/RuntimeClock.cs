using System.Diagnostics;

namespace SeedDormitoryCorridor.Runtime;

public interface IRuntimeClock
{
    long ElapsedMilliseconds { get; }
}

public sealed class RuntimeClock : IRuntimeClock
{
    private readonly long origin = Stopwatch.GetTimestamp();

    public long ElapsedMilliseconds => (Stopwatch.GetTimestamp() - origin) * 1000 / Stopwatch.Frequency;
}
