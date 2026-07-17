namespace SeedDormitoryCorridor.Platform.Windows;

public static class DpiHelper
{
    public static Rendering.DpiScale GetScale(Control control) => new(control.DeviceDpi / 96f, control.DeviceDpi / 96f);
}
