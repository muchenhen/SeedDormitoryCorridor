using Microsoft.Win32;

namespace SeedDormitoryCorridor.Platform.Windows;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SeedDormitoryCorridor";

    public static bool IsEnabled
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled, string executablePath)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Path.GetFullPath(executablePath)}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}
