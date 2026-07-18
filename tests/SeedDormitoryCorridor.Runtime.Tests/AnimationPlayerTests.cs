using SeedDormitoryCorridor.Runtime;

namespace SeedDormitoryCorridor.Runtime.Tests;

public sealed class AnimationPlayerTests
{
    private static AnimationCatalog CreateCatalog() => new(
    [
        new AnimationDefinition("idle", 0, [100, 200]),
        new AnimationDefinition("once", 1, [50, 50], AnimationPlaybackMode.Once, DefaultNextAnimation: "idle", Priority: 10),
        new AnimationDefinition("count", 2, [30, 30], AnimationPlaybackMode.Count, 2, "idle", 5),
        new AnimationDefinition("high", 3, [100], Priority: 20),
    ]);

    [Fact]
    public void AdvancesAtExactFrameBoundaryAndAcrossFrames()
    {
        var player = new AnimationPlayer(CreateCatalog(), "idle");

        Assert.False(player.Update(99));
        Assert.Equal(0, player.State.Column);
        Assert.True(player.Update(100));
        Assert.Equal(1, player.State.Column);
        Assert.True(player.Update(300));
        Assert.Equal(0, player.State.Column);
        Assert.True(player.Update(700));
        Assert.Equal(1, player.State.Column);
    }

    [Fact]
    public void OnceCompletesAndJumpsToIdle()
    {
        var player = new AnimationPlayer(CreateCatalog(), "idle");
        string? completed = null;
        player.AnimationCompleted += (_, args) => completed = args.AnimationName;

        player.Play("once", 0, force: true);
        player.Update(100);

        Assert.Equal("once", completed);
        Assert.Equal("idle", player.State.AnimationName);
        Assert.Equal(0, player.State.Column);
    }

    [Fact]
    public void CountPlaysRequestedNumberOfLoops()
    {
        var player = new AnimationPlayer(CreateCatalog(), "idle");
        player.Play("count", 0, force: true);

        player.Update(119);
        Assert.Equal("count", player.State.AnimationName);
        Assert.Equal(1, player.State.Column);
        player.Update(120);
        Assert.Equal("idle", player.State.AnimationName);
    }

    [Fact]
    public void LowerPriorityCannotInterruptButForceCan()
    {
        var player = new AnimationPlayer(CreateCatalog(), "idle");
        Assert.True(player.Play("high", 0));
        Assert.False(player.Play("once", 1));
        Assert.Equal("high", player.State.AnimationName);
        Assert.True(player.Play("once", 2, force: true));
    }

    [Fact]
    public void ForcedManualSelectionRestartsAfterHighPriorityLoop()
    {
        var player = new AnimationPlayer(CreateCatalog(), "idle");
        Assert.True(player.Play("high", 0));
        Assert.True(player.Update(250));

        Assert.True(player.Play("count", 250, force: true, restart: true));

        Assert.Equal("count", player.State.AnimationName);
        Assert.Equal(0, player.State.Column);
        Assert.Equal(5, player.ActivePriority);
    }

    [Fact]
    public void SameAnimationCanRestartOrContinue()
    {
        var player = new AnimationPlayer(CreateCatalog(), "idle");
        player.Update(150);
        Assert.Equal(1, player.State.Column);

        player.Play("idle", 150, restart: false);
        Assert.Equal(1, player.State.Column);
        player.Play("idle", 150, restart: true);
        Assert.Equal(0, player.State.Column);
    }

    [Fact]
    public void PauseAndResumeDoNotSkipElapsedWallTime()
    {
        var player = new AnimationPlayer(CreateCatalog(), "idle");
        player.Pause(50);
        Assert.False(player.Update(10_000));
        player.Resume(10_000);

        Assert.False(player.Update(10_049));
        Assert.True(player.Update(10_050));
        Assert.Equal(1, player.State.Column);
    }
}
