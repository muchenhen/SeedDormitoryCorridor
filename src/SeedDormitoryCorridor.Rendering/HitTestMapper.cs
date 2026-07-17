namespace SeedDormitoryCorridor.Rendering;

public static class HitTestMapper
{
    public static bool TryMapToSource(
        int clientX,
        int clientY,
        int targetWidth,
        int targetHeight,
        int sourceWidth,
        int sourceHeight,
        bool flipHorizontally,
        out int sourceX,
        out int sourceY)
    {
        sourceX = 0;
        sourceY = 0;
        if (clientX < 0 || clientY < 0 || targetWidth <= 0 || targetHeight <= 0 ||
            sourceWidth <= 0 || sourceHeight <= 0 || clientX >= targetWidth || clientY >= targetHeight)
        {
            return false;
        }

        sourceX = Math.Min(sourceWidth - 1, (int)((long)clientX * sourceWidth / targetWidth));
        sourceY = Math.Min(sourceHeight - 1, (int)((long)clientY * sourceHeight / targetHeight));
        if (flipHorizontally)
        {
            sourceX = sourceWidth - 1 - sourceX;
        }

        return true;
    }
}
