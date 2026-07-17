namespace SeedDormitoryCorridor.Platform.Windows;

public static class MonitorHelper
{
    public static Point EnsurePartiallyVisible(Point desired, Size windowSize, string? preferredDeviceName = null)
    {
        const int visiblePixels = 32;
        Screen? preferred = Screen.AllScreens.FirstOrDefault(screen =>
            string.Equals(screen.DeviceName, preferredDeviceName, StringComparison.OrdinalIgnoreCase));
        Rectangle bounds = preferred?.WorkingArea ?? Screen.FromPoint(desired).WorkingArea;
        var window = new Rectangle(desired, windowSize);
        bool intersects = Screen.AllScreens.Any(screen => Rectangle.Intersect(screen.WorkingArea, window).Width >= visiblePixels &&
            Rectangle.Intersect(screen.WorkingArea, window).Height >= visiblePixels);
        if (intersects)
        {
            return desired;
        }

        int x = Math.Clamp(desired.X, bounds.Left - windowSize.Width + visiblePixels, bounds.Right - visiblePixels);
        int y = Math.Clamp(desired.Y, bounds.Top - windowSize.Height + visiblePixels, bounds.Bottom - visiblePixels);
        return new Point(x, y);
    }

    public static Point DefaultPosition(Size windowSize)
    {
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.VirtualScreen;
        return new Point(area.Right - windowSize.Width - 32, area.Bottom - windowSize.Height - 32);
    }
}
