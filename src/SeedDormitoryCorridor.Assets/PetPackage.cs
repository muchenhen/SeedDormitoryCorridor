namespace SeedDormitoryCorridor.Assets;

public sealed class PetPackage : IDisposable
{
    public PetPackage(string rootPath, PetManifest manifest, AtlasDefinition atlas, DecodedSpriteSheet spriteSheet)
    {
        RootPath = rootPath;
        Manifest = manifest;
        Atlas = atlas;
        SpriteSheet = spriteSheet;
    }

    public string RootPath { get; }
    public PetManifest Manifest { get; }
    public AtlasDefinition Atlas { get; }
    public DecodedSpriteSheet SpriteSheet { get; }

    public void Dispose() => SpriteSheet.Dispose();
}
