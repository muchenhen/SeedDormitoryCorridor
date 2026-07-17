using SeedDormitoryCorridor.Runtime;

namespace SeedDormitoryCorridor.Assets.Tests;

public sealed class CodexPetV2ProfileTests
{
    [Fact]
    public void Version1DefinesNineRowAtlasAndNineAnimations()
    {
        AtlasDefinition atlas = new CodexPetV2Profile().CreateAtlasDefinition(new PetManifest());

        Assert.Equal((1536, 1872, 8, 9, 192, 208),
            (atlas.Width, atlas.Height, atlas.Columns, atlas.Rows, atlas.FrameWidth, atlas.FrameHeight));
        Assert.Equal(9, atlas.Animations.All.Count);
        Assert.Equal([280, 110, 110, 140, 140, 320], atlas.Animations["idle"].FrameDurationsMs);
        Assert.Equal(8, atlas.Animations["running-right"].FrameCount);
        Assert.Equal(220, atlas.Animations["running-left"].FrameDurationsMs[^1]);
        Assert.Equal(AnimationPlaybackMode.Once, atlas.Animations["waving"].DefaultMode);
        Assert.Equal(5, atlas.Animations["jumping"].FrameCount);
        Assert.Equal(8, atlas.Animations["failed"].FrameCount);
        Assert.Equal(6, atlas.Animations["waiting"].FrameCount);
        Assert.Equal(6, atlas.Animations["running"].FrameCount);
        Assert.Equal(6, atlas.Animations["review"].FrameCount);
        Assert.Equal(15, atlas.UnusedCells.Count());
    }

    [Fact]
    public void Version2DefinesElevenRowAtlasIncludingLookFrames()
    {
        AtlasDefinition atlas = new CodexPetV2Profile().CreateAtlasDefinition(new PetManifest { SpriteVersionNumber = 2 });

        Assert.Equal((1536, 2288, 8, 11, 192, 208),
            (atlas.Width, atlas.Height, atlas.Columns, atlas.Rows, atlas.FrameWidth, atlas.FrameHeight));
        Assert.Equal(9, atlas.Animations.All.Count);
        Assert.Equal(74, atlas.RequiredCells.Count());
        Assert.Equal(14, atlas.UnusedCells.Count());
        Assert.Contains((0, 6), atlas.RequiredCells);
        Assert.All(
            Enumerable.Range(9, 2).SelectMany(row => Enumerable.Range(0, 8).Select(column => (row, column))),
            cell => Assert.Contains(cell, atlas.RequiredCells));
    }
}
