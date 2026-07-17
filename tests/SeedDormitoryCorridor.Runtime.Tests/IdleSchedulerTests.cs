using SeedDormitoryCorridor.Runtime;

namespace SeedDormitoryCorridor.Runtime.Tests;

public sealed class IdleSchedulerTests
{
    [Fact]
    public void OffAndInteractionBlockScheduling()
    {
        var scheduler = new IdleScheduler([new IdleCandidate("waving", 1, 0)], new FixedRandom());
        scheduler.Frequency = IdleFrequency.Off;
        Assert.Null(scheduler.TrySchedule(1_000_000, false));

        scheduler.Frequency = IdleFrequency.High;
        Assert.Null(scheduler.TrySchedule(30_000, true));
    }

    [Fact]
    public void AvoidsImmediateRepeatAndHonorsCooldown()
    {
        var scheduler = new IdleScheduler(
        [
            new IdleCandidate("waving", 1, 100_000),
            new IdleCandidate("review", 1, 0),
        ], new FixedRandom());
        scheduler.Frequency = IdleFrequency.High;

        Assert.Equal("waving", scheduler.TrySchedule(30_000, false));
        Assert.Equal("review", scheduler.TrySchedule(60_000, false));
        Assert.Null(scheduler.TrySchedule(90_000, false));
    }

    private sealed class FixedRandom : IRandomSource
    {
        public int NextInt(int exclusiveMaximum) => 0;
    }
}
