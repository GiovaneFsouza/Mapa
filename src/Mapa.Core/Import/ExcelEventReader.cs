using System.Globalization;
using ClosedXML.Excel;
using Mapa.Core.Models;

namespace Mapa.Core.Import;

public sealed record ExcelImportResult(IReadOnlyList<PmuEvent> Events, IReadOnlyList<string> RowErrors);

/// <summary>
/// Reads the events sheet (Tabela 3 shape, idName instead of pmu_id):
/// event_id, idName, source_id, event_type, date, start_time, end_time, extreme_value, quality.
/// Column order in the file does not matter; matched by header name.
/// </summary>
public static class ExcelEventReader
{
    private static readonly string[] RequiredHeaders =
    {
        "event_id", "idName", "source_id", "event_type", "date", "start_time", "end_time", "extreme_value", "quality",
    };

    private const string TimeFormat = "HH:mm:ss.fff";

    public static ExcelImportResult Read(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();

        var headerRow = worksheet.FirstRowUsed()
            ?? throw new InvalidDataException("Planilha vazia: nenhuma linha de cabecalho encontrada.");

        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(header))
                columnIndex[header] = cell.Address.ColumnNumber;
        }

        var missing = RequiredHeaders.Where(h => !columnIndex.ContainsKey(h)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"Colunas obrigatorias ausentes na planilha: {string.Join(", ", missing)}");

        var events = new List<PmuEvent>();
        var errors = new List<string>();

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var rowNumber = row.RowNumber();
            try
            {
                var eventId = row.Cell(columnIndex["event_id"]).GetString().Trim();
                if (string.IsNullOrEmpty(eventId))
                    continue; // blank trailing row

                var idName = row.Cell(columnIndex["idName"]).GetString().Trim();
                var sourceId = (int)row.Cell(columnIndex["source_id"]).GetDouble();
                var eventType = row.Cell(columnIndex["event_type"]).GetString().Trim();
                var date = DateOnly.FromDateTime(row.Cell(columnIndex["date"]).GetDateTime());
                var startTime = ParseTime(row.Cell(columnIndex["start_time"]).GetString());
                var endTime = ParseTime(row.Cell(columnIndex["end_time"]).GetString());
                var extremeValue = row.Cell(columnIndex["extreme_value"]).GetDouble();
                var qualityRaw = row.Cell(columnIndex["quality"]).GetString().Trim();

                events.Add(PmuEvent.Create(eventId, idName, sourceId, eventType, date, startTime, endTime, extremeValue, qualityRaw));
            }
            catch (Exception ex)
            {
                errors.Add($"Linha {rowNumber}: {ex.Message}");
            }
        }

        return new ExcelImportResult(events, errors);
    }

    private static TimeOnly ParseTime(string raw)
    {
        var trimmed = raw.Trim();
        return TimeOnly.TryParseExact(trimmed, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t
            : TimeOnly.Parse(trimmed, CultureInfo.InvariantCulture);
    }
}
