namespace SeedDormitoryCorridor.Configuration;

public sealed class AppPaths
{
    public AppPaths(string? roamingRoot = null, string? localRoot = null)
    {
        RoamingDirectory = roamingRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SeedDormitoryCorridor");
        LocalDirectory = localRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeedDormitoryCorridor");
        PetsDirectory = Path.Combine(LocalDirectory, "Pets");
        LogsDirectory = Path.Combine(LocalDirectory, "Logs");
        StagingDirectory = Path.Combine(LocalDirectory, "Staging");
        SettingsFile = Path.Combine(RoamingDirectory, "settings.json");
    }

    public string RoamingDirectory { get; }

    public string LocalDirectory { get; }

    public string PetsDirectory { get; }

    public string LogsDirectory { get; }

    public string StagingDirectory { get; }

    public string SettingsFile { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RoamingDirectory);
        Directory.CreateDirectory(PetsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(StagingDirectory);
    }
}
