using SeedDormitoryCorridor.Runtime;

namespace SeedDormitoryCorridor.Assets;

public sealed record AtlasDefinition(
    int Width,
    int Height,
    int Columns,
    int Rows,
    int FrameWidth,
    int FrameHeight,
    AnimationCatalog Animations,
    IReadOnlyCollection<(int Row, int Column)>? AdditionalRequiredCells = null)
{
    public IEnumerable<(int Row, int Column)> RequiredCells =>
        Animations.All
            .SelectMany(animation => Enumerable.Range(0, animation.FrameCount).Select(column => (animation.Row, column)))
            .Concat(AdditionalRequiredCells ?? []);

    public IEnumerable<(int Row, int Column)> UnusedCells
    {
        get
        {
            var required = RequiredCells.ToHashSet();
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    if (!required.Contains((row, column)))
                    {
                        yield return (row, column);
                    }
                }
            }
        }
    }
}

public interface IAssetProfile
{
    string ProfileId { get; }

    AtlasDefinition CreateAtlasDefinition(PetManifest manifest);
}

public sealed class CodexPetV2Profile : IAssetProfile
{
    public const string Id = "codex-pet-v2";
    public const int Version1Height = 1872;
    public const int Version2Height = 2288;

    public string ProfileId => Id;

    public AtlasDefinition CreateAtlasDefinition(PetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        AnimationDefinition[] animations =
        [
            new("idle", 0, [280, 110, 110, 140, 140, 320]),
            new("running-right", 1, [120, 120, 120, 120, 120, 120, 120, 220], Priority: 20),
            new("running-left", 2, [120, 120, 120, 120, 120, 120, 120, 220], Priority: 20),
            new("waving", 3, [140, 140, 140, 280], AnimationPlaybackMode.Once, DefaultNextAnimation: "idle", Priority: 10),
            new("jumping", 4, [140, 140, 140, 140, 280], AnimationPlaybackMode.Once, DefaultNextAnimation: "idle", Priority: 10),
            new("failed", 5, [140, 140, 140, 140, 140, 140, 140, 240], AnimationPlaybackMode.Count, 2, "idle", 10),
            new("waiting", 6, [150, 150, 150, 150, 150, 260], AnimationPlaybackMode.Count, 2, "idle", 5),
            new("running", 7, [120, 120, 120, 120, 120, 220]),
            new("review", 8, [150, 150, 150, 150, 150, 280], AnimationPlaybackMode.Count, 2, "idle", 5),
        ];
        bool isVersion2 = manifest.SpriteVersionNumber == 2;
        int rows = isVersion2 ? 11 : 9;
        IReadOnlyCollection<(int Row, int Column)> lookCells = isVersion2
            ? new[] { (Row: 0, Column: 6) }
                .Concat(Enumerable.Range(9, 2).SelectMany(row => Enumerable.Range(0, 8).Select(column => (Row: row, Column: column))))
                .ToArray()
            : [];
        return new AtlasDefinition(
            1536,
            isVersion2 ? Version2Height : Version1Height,
            8,
            rows,
            192,
            208,
            new AnimationCatalog(animations),
            lookCells);
    }
}

public sealed class AssetProfileRegistry
{
    private readonly Dictionary<string, IAssetProfile> profiles = new(StringComparer.OrdinalIgnoreCase);

    public AssetProfileRegistry(IEnumerable<IAssetProfile>? profiles = null)
    {
        foreach (IAssetProfile profile in profiles ?? [new CodexPetV2Profile()])
        {
            this.profiles.Add(profile.ProfileId, profile);
        }
    }

    public bool TryGet(string id, out IAssetProfile? profile) => profiles.TryGetValue(id, out profile);
}
