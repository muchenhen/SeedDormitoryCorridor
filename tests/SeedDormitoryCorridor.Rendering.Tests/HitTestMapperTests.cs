using SeedDormitoryCorridor.Rendering;

namespace SeedDormitoryCorridor.Rendering.Tests;

public sealed class HitTestMapperTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(199, 99, 99, 49)]
    [InlineData(100, 50, 50, 25)]
    public void MapsScaledCoordinates(int x, int y, int expectedX, int expectedY)
    {
        Assert.True(HitTestMapper.TryMapToSource(x, y, 200, 100, 100, 50, false, out int sourceX, out int sourceY));
        Assert.Equal(expectedX, sourceX);
        Assert.Equal(expectedY, sourceY);
    }

    [Fact]
    public void MapsHorizontallyFlippedCoordinates()
    {
        Assert.True(HitTestMapper.TryMapToSource(0, 0, 200, 100, 100, 50, true, out int sourceX, out _));
        Assert.Equal(99, sourceX);
        Assert.True(HitTestMapper.TryMapToSource(199, 0, 200, 100, 100, 50, true, out sourceX, out _));
        Assert.Equal(0, sourceX);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(200, 0)]
    [InlineData(0, 100)]
    public void RejectsOutOfBoundsCoordinates(int x, int y)
    {
        Assert.False(HitTestMapper.TryMapToSource(x, y, 200, 100, 100, 50, false, out _, out _));
    }
}
