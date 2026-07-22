using SeedDormitoryCorridor.Online;

namespace SeedDormitoryCorridor.App.Settings;

public enum OnlinePetUiStatus
{
    NotInstalled,
    Downloading,
    Installed,
    Incompatible,
    Failed,
}

public sealed record OnlinePetViewModel(
    OnlinePetCatalogItem Item,
    OnlinePetUiStatus Status,
    string? ErrorMessage = null,
    bool IsInstalled = false);
