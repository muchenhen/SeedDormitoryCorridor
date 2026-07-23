namespace SeedDormitoryCorridor.Assets;

public enum PetDeleteStatus
{
    Deleted,
    NotInstalled,
    BuiltInProtected,
    SwitchFailed,
}

public sealed record PetDeleteResult(string PetId, PetDeleteStatus Status);

public sealed class PetCollectionManager
{
    private readonly PetInstaller installer;
    private readonly HashSet<string> builtInPetIds;
    private readonly string defaultPetId;

    public PetCollectionManager(PetInstaller installer, IEnumerable<string> builtInPetIds, string defaultPetId)
    {
        this.installer = installer ?? throw new ArgumentNullException(nameof(installer));
        this.builtInPetIds = new HashSet<string>(builtInPetIds ?? throw new ArgumentNullException(nameof(builtInPetIds)), StringComparer.OrdinalIgnoreCase);
        this.defaultPetId = string.IsNullOrWhiteSpace(defaultPetId)
            ? throw new ArgumentException("Default pet id is required.", nameof(defaultPetId))
            : defaultPetId;
        if (!this.builtInPetIds.Contains(defaultPetId))
        {
            throw new ArgumentException("Default pet id must be protected as built in.", nameof(defaultPetId));
        }
    }

    public PetDeleteResult Delete(string petId, string? currentPetId, Func<string, bool> switchPet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(petId);
        ArgumentNullException.ThrowIfNull(switchPet);
        if (builtInPetIds.Contains(petId))
        {
            return new PetDeleteResult(petId, PetDeleteStatus.BuiltInProtected);
        }

        bool installed = installer.ListInstalled().Any(item => string.Equals(item.Id, petId, StringComparison.OrdinalIgnoreCase));
        if (!installed)
        {
            return new PetDeleteResult(petId, PetDeleteStatus.NotInstalled);
        }

        if (string.Equals(petId, currentPetId, StringComparison.OrdinalIgnoreCase) && !switchPet(defaultPetId))
        {
            return new PetDeleteResult(petId, PetDeleteStatus.SwitchFailed);
        }

        installer.Delete(petId);
        return new PetDeleteResult(petId, PetDeleteStatus.Deleted);
    }
}
