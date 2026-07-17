using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeedDormitoryCorridor.Configuration;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string path;

    public SettingsStore(string path)
    {
        this.path = Path.GetFullPath(path);
    }

    public AppSettings Load(out string? recoveryMessage)
    {
        recoveryMessage = null;
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            string backup = path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            try
            {
                File.Move(path, backup, false);
                recoveryMessage = $"配置已损坏并备份为 {Path.GetFileName(backup)}。";
            }
            catch (IOException)
            {
                recoveryMessage = "配置已损坏，且无法创建备份。";
            }

            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            throw new InvalidOperationException("Settings path has no directory.");
        }

        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, settings, Options);
                stream.Flush(true);
            }

            if (File.Exists(path))
            {
                File.Replace(temporary, path, null, true);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
