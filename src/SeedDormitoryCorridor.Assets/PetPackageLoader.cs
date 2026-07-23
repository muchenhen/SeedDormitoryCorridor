using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SeedDormitoryCorridor.Assets;

public sealed class PetPackageLoader
{
    public const long MaximumPngBytes = 64L * 1024 * 1024;
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private readonly AssetProfileRegistry profiles;

    public PetPackageLoader(AssetProfileRegistry? profiles = null)
    {
        this.profiles = profiles ?? new AssetProfileRegistry();
    }

    public PetPackage Load(string packageRoot)
    {
        ValidationResult validation = ValidateAndLoad(packageRoot, out PetPackage? package);
        if (!validation.IsValid || package is null)
        {
            package?.Dispose();
            throw new PetValidationException(validation);
        }

        return package;
    }

    public ValidationResult ValidateAndLoad(string packageRoot, out PetPackage? package)
    {
        package = null;
        var result = new ValidationResult();
        string root = Path.GetFullPath(packageRoot);
        string manifestPath = Path.Combine(root, "pet.json");
        if (!File.Exists(manifestPath))
        {
            result.AddError("manifest.missing", "找不到 pet.json。", filePath: manifestPath);
            return result;
        }

        PetManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PetManifest>(File.ReadAllText(manifestPath), ManifestJsonOptions);
        }
        catch (JsonException exception)
        {
            result.AddError("manifest.invalid-json", $"pet.json 不是有效 JSON：{exception.Message}", exception.Path, manifestPath);
            return result;
        }
        catch (IOException exception)
        {
            result.AddError("manifest.read", $"无法读取 pet.json：{exception.Message}", filePath: manifestPath);
            return result;
        }

        manifest ??= new PetManifest();
        ValidateManifest(manifest, result);
        if (!result.IsValid)
        {
            return result;
        }

        string spritePath;
        try
        {
            spritePath = PathSecurity.ResolveWithinRoot(root, manifest.SpritesheetPath!);
        }
        catch (InvalidDataException exception)
        {
            result.AddError("spritesheet.path", exception.Message, "$.spritesheetPath");
            return result;
        }

        if (!string.Equals(Path.GetExtension(spritePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            result.AddError("spritesheet.format", "当前版本只支持 PNG。", "$.spritesheetPath", spritePath);
            return result;
        }

        var file = new FileInfo(spritePath);
        if (!file.Exists)
        {
            result.AddError("spritesheet.missing", "spritesheet 文件不存在。", "$.spritesheetPath", spritePath);
            return result;
        }

        if (file.Length <= 0 || file.Length > MaximumPngBytes)
        {
            result.AddError("spritesheet.size", "PNG 文件大小必须在 1 字节到 64 MiB 之间。", "$.spritesheetPath", spritePath);
            return result;
        }

        DecodedSpriteSheet? decoded = null;
        try
        {
            decoded = DecodedSpriteSheet.DecodePng(spritePath);
            string profileId = manifest.DesktopPet?.Profile ??
                (decoded.Width == 1536 && decoded.Height is CodexPetV2Profile.Version1Height or CodexPetV2Profile.Version2Height
                    ? CodexPetV2Profile.Id
                    : string.Empty);
            if (!profiles.TryGet(profileId, out IAssetProfile? profile) || profile is null)
            {
                result.AddError("profile.unsupported", string.IsNullOrEmpty(profileId)
                    ? "未声明 profile，且图片尺寸不匹配受支持的 codex-pet-v2 Atlas。"
                    : $"不支持资产 profile '{profileId}'。", "$.desktopPet.profile");
                return result;
            }

            AtlasDefinition atlas = profile.CreateAtlasDefinition(manifest);
            if (decoded.Width != atlas.Width || decoded.Height != atlas.Height)
            {
                result.AddError("atlas.dimensions", $"图片尺寸必须为 {atlas.Width}×{atlas.Height}，实际为 {decoded.Width}×{decoded.Height}。", filePath: spritePath);
                return result;
            }

            ValidateAlpha(decoded, atlas, result, spritePath);
            if (result.IsValid)
            {
                package = new PetPackage(root, manifest, profile.ProfileId, atlas, decoded);
                decoded = null;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or OutOfMemoryException or InvalidDataException)
        {
            result.AddError("spritesheet.decode", $"PNG 解码失败：{exception.Message}", filePath: spritePath);
        }
        finally
        {
            decoded?.Dispose();
        }

        return result;
    }

    private static void ValidateManifest(PetManifest manifest, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            result.AddError("manifest.id.required", "id 不能为空。", "$.id");
        }
        else if (manifest.Id.Length > 80 ||
            manifest.Id.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')) ||
            !PathSecurity.IsSafeRelativePath(manifest.Id))
        {
            result.AddError(
                "manifest.id.invalid",
                "id 必须是 Windows 安全的单个目录名，只能包含 ASCII 字母、数字、点、短横线和下划线，且最长 80 字符。",
                "$.id");
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
        {
            result.AddError("manifest.displayName.required", "displayName 不能为空。", "$.displayName");
        }

        if (!PathSecurity.IsSafeRelativePath(manifest.SpritesheetPath))
        {
            result.AddError("manifest.spritesheetPath.invalid", "spritesheetPath 必须是包内相对路径且不能包含 '..'。", "$.spritesheetPath");
        }

        if (manifest.SpriteVersionNumber is int spriteVersion && spriteVersion is not (1 or 2))
        {
            result.AddError("manifest.spriteVersionNumber.invalid", "spriteVersionNumber 只能是 1 或 2。", "$.spriteVersionNumber");
        }

        if (manifest.DesktopPet?.DefaultScale is float scale && (scale < 0.25f || scale > 4f))
        {
            result.AddError("manifest.scale.invalid", "defaultScale 必须在 0.25 到 4.0 之间。", "$.desktopPet.defaultScale");
        }

        if (manifest.DesktopPet?.RenderMode is string renderMode &&
            !string.Equals(renderMode, "smooth", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(renderMode, "pixelated", StringComparison.OrdinalIgnoreCase))
        {
            result.AddError("manifest.renderMode.invalid", "renderMode 只能是 smooth 或 pixelated。", "$.desktopPet.renderMode");
        }

        if (manifest.DesktopPet?.AlphaThreshold is int threshold && (threshold < 0 || threshold > 255))
        {
            result.AddError("manifest.alphaThreshold.invalid", "alphaThreshold 必须在 0 到 255 之间。", "$.desktopPet.alphaThreshold");
        }
    }

    private static void ValidateAlpha(DecodedSpriteSheet decoded, AtlasDefinition atlas, ValidationResult result, string path)
    {
        foreach ((int row, int column) in atlas.RequiredCells)
        {
            if (!decoded.CellHasAlpha(row, column, atlas.FrameWidth, atlas.FrameHeight))
            {
                result.AddError("atlas.required-cell-transparent", $"必要动画格 row={row}, column={column} 完全透明。", filePath: path);
            }
        }

        foreach ((int row, int column) in atlas.UnusedCells)
        {
            if (decoded.CellHasAlpha(row, column, atlas.FrameWidth, atlas.FrameHeight))
            {
                result.AddError("atlas.unused-cell-visible", $"未使用动画格 row={row}, column={column} 必须完全透明。", filePath: path);
            }
        }
    }
}
