using System.Globalization;
using Mapa.Core.Models;

namespace Mapa.Core.Import;

public sealed record BdedImportResult(IReadOnlyList<PmuEvent> Events, IReadOnlyList<string> Errors);

/// <summary>
/// Reads all VTCD/frequency events from a BDED folder tree.
/// Expected structure: &lt;root&gt;/&lt;pmu_id&gt;/&lt;year&gt;/&lt;month&gt;.csv
/// CSV columns: pmu_id, source_id, event_type, date, start_time, end_time, extreme_value, quality
/// </summary>
public static class CsvBdedReader
{
    public static BdedImportResult ReadFolder(string bdedFolderPath)
    {
        if (!Directory.Exists(bdedFolderPath))
            return new BdedImportResult(Array.Empty<PmuEvent>(), [$"Pasta não encontrada: {bdedFolderPath}"]);

        var events = new List<PmuEvent>();
        var errors = new List<string>();

        foreach (var file in Directory.EnumerateFiles(bdedFolderPath, "*.csv", SearchOption.AllDirectories))
        {
            try
            {
                ReadFile(file, events, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return new BdedImportResult(events, errors);
    }

    private static void ReadFile(string filePath, List<PmuEvent> events, List<string> errors)
    {
        using var reader = new StreamReader(filePath);
        var header = reader.ReadLine();
        if (header is null)
            return;

        var columns = header.Split(',');
        var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Length; i++)
            idx[columns[i].Trim()] = i;

        string? line;
        var lineNum = 1;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var parts = line.Split(',');

                var pmuId = parts[idx["pmu_id"]].Trim();
                var sourceId = int.Parse(parts[idx["source_id"]].Trim(), CultureInfo.InvariantCulture);
                var eventType = parts[idx["event_type"]].Trim();
                var date = DateOnly.Parse(parts[idx["date"]].Trim(), CultureInfo.InvariantCulture);
                var startTime = ParseTime(parts[idx["start_time"]].Trim());
                var endTime = ParseTime(parts[idx["end_time"]].Trim());
                var extremeValue = double.Parse(parts[idx["extreme_value"]].Trim(), CultureInfo.InvariantCulture);
                var quality = parts[idx["quality"]].Trim();

                var eventId = $"{pmuId}_{date:yyyy-MM-dd}_{startTime:HHmmss.fff}";
                events.Add(PmuEvent.Create(eventId, pmuId, sourceId, eventType, date, startTime, endTime, extremeValue, quality));
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(filePath)}:{lineNum}: {ex.Message}");
            }
        }
    }

    private static TimeOnly ParseTime(string raw) =>
        TimeOnly.TryParseExact(raw, "HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t
            : TimeOnly.Parse(raw, CultureInfo.InvariantCulture);
}
