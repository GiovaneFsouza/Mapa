namespace Mapa.Core.Settings;

public sealed record XmlDatabaseEntry(string FileName, string FullPath);

/// <summary>Scans the fixed "XML" folder (next to the executable) for .xml files the user has dropped there.</summary>
public static class XmlFolderScanner
{
    public static IReadOnlyList<XmlDatabaseEntry> Scan(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return Array.Empty<XmlDatabaseEntry>();

        return Directory.EnumerateFiles(folderPath, "*.xml", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new XmlDatabaseEntry(Path.GetFileName(path), path))
            .ToList();
    }
}
