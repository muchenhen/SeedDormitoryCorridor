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
        Assert.Null(scheduler.TrySchedule(15_000, true));
    }

    [Theory]
    [InlineData(IdleFrequency.Low, 60_000)]
    [InlineData(IdleFrequency.Normal, 30_000)]
    [InlineData(IdleFrequency.High, 15_000)]
    public void SchedulesAtConfiguredInterval(IdleFrequency frequency, int intervalMs)
    {
        var scheduler = new IdleScheduler([new IdleCandidate("waving", 1, 0)], new FixedRandom())
        {
            Frequency = frequency,
        };

        Assert.Null(scheduler.TrySchedule(intervalMs - 1, false));
        Assert.Equal("waving", scheduler.TrySchedule(intervalMs, false));
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

        Assert.Equal("waving", scheduler.TrySchedule(15_000, false));
        Assert.Equal("review", scheduler.TrySchedule(30_000, false));
        Assert.Null(scheduler.TrySchedule(45_000, false));
    }

    private sealed class FixedRandom : IRandomSource
    {
        public int NextInt(int exclusiveMaximum) => 0;
    }
}
