namespace SeedDormitoryCorridor.Assets;

public static class PathSecurity
{
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static bool IsSafeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
        {
            return false;
        }

        string[] segments = value.Replace('\\', '/').Split('/');
        return segments.All(IsSafePathSegment);
    }

    public static string ResolveWithinRoot(string root, string relativePath)
    {
        if (!IsSafeRelativePath(relativePath))
        {
            throw new InvalidDataException("路径必须是包内相对路径，且不能包含 '..'。 ");
        }

        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("路径逃逸出宠物包目录。");
        }

        return candidate;
    }

    public static bool HasReparsePoint(string path)
    {
        var info = new FileInfo(path);
        return info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0;
    }

    private static bool IsSafePathSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) || segment is "." or ".." ||
            segment.EndsWith(' ') || segment.EndsWith('.') ||
            segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        string deviceName = segment.Split('.')[0];
        return !ReservedWindowsNames.Contains(deviceName);
    }
}
