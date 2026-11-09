using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapa.App.Services;
using Mapa.Core.Aggregation;
using Mapa.Core.Models;
using Mapa.Core.Scales;

namespace Mapa.App.ViewModels;

public enum OccurrenceSortOption
{
    MostRecent,
    MostPmus,
    MostSevere,
}

/// <summary>One detected event (a disturbance correlated across PMUs), as listed in the UI.</summary>
public sealed class OccurrenceItemViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public OccurrenceItemViewModel(Occurrence occurrence, IReadOnlyList<Pmu> pmus)
    {
        Occurrence = occurrence;
        InvolvedPmus = pmus;

        // Grouping header in the list separates the two grandezas.
        CategoryLabel = occurrence.Category == EventCategory.Voltage ? "VTCD" : "Desvio de Frequência";
        DateLabel = occurrence.AnchorTimestamp.ToString("dd/MM/yyyy", PtBr);
        TimeLabel = occurrence.AnchorTimestamp.ToString("HH:mm:ss.fff", PtBr);
        PmuCountLabel = occurrence.PmuCount == 1 ? "1 PMU" : $"{occurrence.PmuCount} PMUs";
        WorstValueLabel = occurrence.Category == EventCategory.Voltage
            ? $"{occurrence.WorstExtremeValue.ToString("0.0000", PtBr)} pu"
            : $"{occurrence.WorstExtremeValue.ToString("0.0000", PtBr)} Hz";
    }

    public Occurrence Occurrence { get; }

    /// <summary>PMUs resolved against the XML — only these can be plotted.</summary>
    public IReadOnlyList<Pmu> InvolvedPmus { get; }

    public string CategoryLabel { get; }
    public string DateLabel { get; }
    public string TimeLabel { get; }
    public string PmuCountLabel { get; }
    public string WorstValueLabel { get; }

    public double SeverityRank => MetricSeverityRank.For(
        Occurrence.WorstExtremeValue, MetricMode.Severity, Occurrence.Category);
}

/// <summary>
/// One PMU's detection within the selected event. Ranked worst-first, and carrying the extras that
/// matter for electrical studies: deviation from nominal, voltage level, region, and how long after
/// the first detection this PMU saw the disturbance (its temporal propagation).
/// </summary>
public sealed class OccurrenceEventRowViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public OccurrenceEventRowViewModel(int rank, PmuEvent ev, Pmu? pmu, double nominal, DateTime firstDetection)
    {
        Rank = rank;
        IdName = ev.IdName;
        PmuLabel = pmu?.FullName ?? ev.IdName;
        Region = pmu?.Area ?? "—";
        Station = pmu?.Station ?? "—";
        VoltLevelLabel = pmu is null ? "—" : $"{pmu.VoltLevel / 1000.0:0.##} kV";
        EventType = ev.EventType;
        SourceLabel = SourceIdLabel(ev.SourceId);
        StartTime = ev.StartTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        EndTime = ev.EndTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        DurationLabel = $"{ev.Duration.TotalSeconds.ToString("0.###", PtBr)} s";
        ExtremeValue = ev.ExtremeValue.ToString("0.0000", PtBr);
        Deviation = Math.Abs(ev.ExtremeValue - nominal);
        DeviationLabel = Deviation.ToString("0.0000", PtBr);
        RelativeMs = (ev.StartTimestamp - firstDetection).TotalMilliseconds;
        RelativeLabel = $"+{RelativeMs.ToString("0", PtBr)} ms";
        Quality = ev.QualityRaw;
    }

    public int Rank { get; }
    public string IdName { get; }
    /// <summary>config.json label — what the user sees; IdName remains the join key.</summary>
    public string PmuLabel { get; }
    public string Region { get; }
    public string Station { get; }
    public string VoltLevelLabel { get; }
    public string EventType { get; }
    public string SourceLabel { get; }
    public string StartTime { get; }
    public string EndTime { get; }
    public string DurationLabel { get; }
    public string ExtremeValue { get; }
    /// <summary>Distance from nominal. Dropped from the on-screen grid (the measured value already
    /// conveys it) but kept in the CSV export, where it is handy for analysis.</summary>
    public double Deviation { get; }
    public string DeviationLabel { get; }
    public double RelativeMs { get; }
    public string RelativeLabel { get; }
    public string Quality { get; }

    /// <summary>Per the project's data dictionary: 1 = frequência do sistema, 2/3/4 = tensão fases A/B/C.</summary>
    private static string SourceIdLabel(int sourceId) => sourceId switch
    {
        1 => "Frequência",
        2 => "Fase A",
        3 => "Fase B",
        4 => "Fase C",
        _ => $"source_id {sourceId}",
    };
}

/// <summary>
/// Backs the "Eventos Detectados" tab: disturbances correlated across PMUs, and the map/detail view
/// of whichever one is selected. The map shows only the PMUs that observed the selected event,
/// which is what makes a disturbance's spatial footprint visible.
/// </summary>
public sealed partial class OccurrencesViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private IReadOnlyList<Pmu> _pmusInScope = Array.Empty<Pmu>();

    // VTCD threshold colours — kept in sync with the Análise tab's Severity settings.
    public double VtcdSubL1   { get; set; } = 0.70;
    public double VtcdSubL2   { get; set; } = 0.90;
    public double VtcdSobreL1 { get; set; } = 1.10;
    public double VtcdSobreL2 { get; set; } = 1.30;

    public void SetVtcdThresholds(double subL1, double subL2, double sobreL1, double sobreL2)
    {
        VtcdSubL1 = subL1; VtcdSubL2 = subL2;
        VtcdSobreL1 = sobreL1; VtcdSobreL2 = sobreL2;
        OnPropertyChanged(nameof(VtcdSubLegendHigh));
        OnPropertyChanged(nameof(VtcdSubLegendMid));
        OnPropertyChanged(nameof(VtcdSubLegendLow));
        OnPropertyChanged(nameof(VtcdSobreLegendNormal));
        OnPropertyChanged(nameof(VtcdSobreLegendMid));
        OnPropertyChanged(nameof(VtcdSobreLegendHigh));
        if (SelectedOccurrence?.Occurrence.Category == EventCategory.Voltage)
            OnSelectedOccurrenceChanged(SelectedOccurrence);
    }

    // Sub legend (IsInverted): green = ≥ L2, yellow = L1–L2, red = < L1
    public string VtcdSubLegendHigh => $"≥ {VtcdSubL2.ToString("0.##", PtBr)} pu";
    public string VtcdSubLegendMid  => $"{VtcdSubL1.ToString("0.##", PtBr)} a {VtcdSubL2.ToString("0.##", PtBr)} pu";
    public string VtcdSubLegendLow  => $"< {VtcdSubL1.ToString("0.##", PtBr)} pu";

    // Sobre legend (normal): green = < L1, yellow = L1–L2, red = ≥ L2
    public string VtcdSobreLegendNormal => $"< {VtcdSobreL1.ToString("0.##", PtBr)} pu";
    public string VtcdSobreLegendMid    => $"{VtcdSobreL1.ToString("0.##", PtBr)} a {VtcdSobreL2.ToString("0.##", PtBr)} pu";
    public string VtcdSobreLegendHigh   => $"≥ {VtcdSobreL2.ToString("0.##", PtBr)} pu";

    private ThresholdScale VtcdScaleFor(PmuEvent ev) =>
        ev.Direction == EventDirection.High
            ? new ThresholdScale(VtcdSobreL1, VtcdSobreL2, IsInverted: false)
            : new ThresholdScale(VtcdSubL1, VtcdSubL2, IsInverted: true);

    public event Action<IReadOnlyList<MarkerPayload>>? MarkersReady;
    public event Action? FitBoundsRequested;

    public ObservableCollection<OccurrenceItemViewModel> Occurrences { get; } = new();
    public ObservableCollection<OccurrenceEventRowViewModel> SelectedOccurrenceEvents { get; } = new();

    /// <summary>Grouped view of <see cref="Occurrences"/>: VTCD and frequência listed under separate headers.</summary>
    public ICollectionView OccurrencesView { get; }

    public OccurrencesViewModel()
    {
        OccurrencesView = CollectionViewSource.GetDefaultView(Occurrences);
        OccurrencesView.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(OccurrenceItemViewModel.CategoryLabel)));
    }

    [ObservableProperty] private OccurrenceItemViewModel? _selectedOccurrence;
    [ObservableProperty] private string _summary = "Nenhuma planilha carregada.";
    [ObservableProperty] private string _selectedSummary = string.Empty;

    /// <summary>
    /// Legend for the selected event's map, read left-to-right as a ranking: worst case first.
    /// Unlike the Mapa tab (a real pu axis with nominal in the middle), every PMU in one event
    /// sits on the same side of nominal, so ordering by severity is what makes sense here.
    /// </summary>
    [ObservableProperty] private string _scaleLeftLabel = string.Empty;
    [ObservableProperty] private string _scaleRightLabel = string.Empty;
    [ObservableProperty] private string _scaleCaption = string.Empty;
    [ObservableProperty] private bool _hasScale;
    [ObservableProperty] private bool _hasVtcdSubLegend;
    [ObservableProperty] private bool _hasVtcdSobreLegend;
    [ObservableProperty] private bool _hasFreqLegend;
    [ObservableProperty] private bool _hasDetectionRankLegend;

    [ObservableProperty] private OccurrenceSortOption _sortOption = OccurrenceSortOption.MostRecent;

    // VTCD dual-constraint parameters (editable from UI)
    [ObservableProperty] private double _vtcdGapMaxMs   = 500.0;
    [ObservableProperty] private double _vtcdRangeMaxMs = 1000.0;

    // Frequency occurrence grouping — independent settings (single-window, not dual-constraint)
    [ObservableProperty] private double _freqWindowMs = 2000.0;
    [ObservableProperty] private int    _freqMinPmus  = 3;

    private IReadOnlyList<PmuEvent> _lastFiltered = Array.Empty<PmuEvent>();
    private EventCategory? _lastCategoryFilter;

    partial void OnVtcdGapMaxMsChanged(double value)   => Refresh();
    partial void OnVtcdRangeMaxMsChanged(double value) => Refresh();
    partial void OnFreqWindowMsChanged(double value)   => Refresh();
    partial void OnFreqMinPmusChanged(int value)       => Refresh();

    public void Recompute(
        IReadOnlyList<PmuEvent> filtered, IReadOnlyList<Pmu> pmusInScope,
        EventCategory? categoryFilter)
    {
        _lastFiltered = filtered;
        _pmusInScope = pmusInScope;
        _lastCategoryFilter = categoryFilter;

        var pmusByIdName = pmusInScope
            .GroupBy(p => p.IdName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var categories = categoryFilter is null
            ? new[] { EventCategory.Voltage, EventCategory.Frequency }
            : new[] { categoryFilter.Value };

        var previousId = SelectedOccurrence?.Occurrence.Id;

        var vtcdGap   = TimeSpan.FromMilliseconds(Math.Max(1, VtcdGapMaxMs));
        var vtcdRange = TimeSpan.FromMilliseconds(Math.Max(1, VtcdRangeMaxMs));
        var freqWin   = TimeSpan.FromMilliseconds(Math.Max(1, FreqWindowMs));
        var freqMin   = Math.Max(1, FreqMinPmus);

        var items = new List<OccurrenceItemViewModel>();
        foreach (var category in categories)
        {
            var gapMax      = category == EventCategory.Voltage ? vtcdGap   : freqWin;
            var rangeMax    = category == EventCategory.Voltage ? vtcdRange : freqWin;
            var minimumPmus = category == EventCategory.Voltage ? 1         : freqMin;

            foreach (var occurrence in OccurrenceCorrelator.Correlate(filtered, category, gapMax, rangeMax, minimumPmus))
            {
                var involved = occurrence.IdNames
                    .Where(pmusByIdName.ContainsKey)
                    .SelectMany(id => pmusByIdName[id])
                    .ToList();

                // An event whose PMUs are all outside the current region/XML can't be mapped.
                if (involved.Count == 0)
                    continue;

                items.Add(new OccurrenceItemViewModel(occurrence, involved));
            }
        }

        var ordered = Sort(items).ToList();

        Occurrences.Clear();
        foreach (var item in ordered)
            Occurrences.Add(item);

        Summary = BuildSummary(ordered);

        SelectedOccurrence = ordered.FirstOrDefault(i => i.Occurrence.Id == previousId) ?? ordered.FirstOrDefault();
    }

    private IEnumerable<OccurrenceItemViewModel> Sort(IEnumerable<OccurrenceItemViewModel> items) => SortOption switch
    {
        OccurrenceSortOption.MostPmus => items
            .OrderByDescending(i => i.Occurrence.PmuCount)
            .ThenByDescending(i => i.Occurrence.AnchorTimestamp),
        OccurrenceSortOption.MostSevere => items
            .OrderByDescending(i => i.SeverityRank)
            .ThenByDescending(i => i.Occurrence.AnchorTimestamp),
        _ => items.OrderByDescending(i => i.Occurrence.AnchorTimestamp),
    };

    private string BuildSummary(IReadOnlyList<OccurrenceItemViewModel> items)
    {
        if (_lastFiltered.Count == 0)
            return "Nenhum evento no período/filtro selecionado.";

        var vtcd = items.Count(i => i.Occurrence.Category == EventCategory.Voltage);
        var freq = items.Count - vtcd;
        return $"{items.Count} evento(s): {vtcd} VTCD, {freq} de frequência";
    }

    partial void OnSelectedOccurrenceChanged(OccurrenceItemViewModel? value)
    {
        SelectedOccurrenceEvents.Clear();

        if (value is null)
        {
            SelectedSummary = string.Empty;
            HasScale = false;
            HasVtcdSubLegend = false;
            HasVtcdSobreLegend = false;
            HasFreqLegend = false;
            HasDetectionRankLegend = false;
            MarkersReady?.Invoke(Array.Empty<MarkerPayload>());
            return;
        }

        var occurrence = value.Occurrence;
        var nominal = DomainConstants.NominalFor(occurrence.Category);
        var firstDetection = occurrence.Events.Min(e => e.StartTimestamp);
        var pmuByIdName = value.InvolvedPmus
            .GroupBy(p => p.IdName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Chronological: the order in which the PMUs saw the disturbance, so "Início" and the
        // Δt column (delay from the first detection) both read top-down as the event propagates.
        // The grid's headers stay sortable, so ranking by "Valor" is a click away.
        var rankedEvents = occurrence.Events
            .OrderBy(e => e.StartTimestamp)
            .ThenBy(e => e.IdName, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < rankedEvents.Count; i++)
        {
            var ev = rankedEvents[i];
            pmuByIdName.TryGetValue(ev.IdName, out var pmu);
            SelectedOccurrenceEvents.Add(new OccurrenceEventRowViewModel(i + 1, ev, pmu, nominal, firstDetection));
        }

        SelectedSummary = BuildSelectedSummary(value, firstDetection);

        if (value.Occurrence.Category == EventCategory.Frequency)
            PublishFrequencyMarkers(value);
        else
            PublishVoltageMarkers(value);
        FitBoundsRequested?.Invoke();
    }

    private string BuildSelectedSummary(OccurrenceItemViewModel item, DateTime firstDetection)
    {
        var occurrence = item.Occurrence;
        var regions = item.InvolvedPmus
            .Select(p => p.Area)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();

        var spreadMs = (occurrence.Events.Max(e => e.StartTimestamp) - firstDetection).TotalMilliseconds;

        var unit = occurrence.Category == EventCategory.Voltage ? "pu" : "Hz";
        return
            $"{item.PmuCountLabel}  •  pior valor {occurrence.WorstExtremeValue.ToString("0.0000", PtBr)} {unit}  •  " +
            $"regiões: {(regions.Count == 0 ? "—" : string.Join(", ", regions))}  •  " +
            $"propagação {spreadMs.ToString("0", PtBr)} ms";
    }

    private static Dictionary<string, (int Rank, double DelayMs)> BuildDetectionRanks(Occurrence occurrence)
    {
        var first = occurrence.Events.Min(e => e.StartTimestamp);
        return occurrence.Events
            .GroupBy(e => e.IdName, StringComparer.Ordinal)
            .Select(g => (IdName: g.Key, FirstTime: g.Min(e => e.StartTimestamp)))
            .OrderBy(x => x.FirstTime)
            .Select((x, i) => (x.IdName, Rank: i + 1, DelayMs: (x.FirstTime - first).TotalMilliseconds))
            .ToDictionary(x => x.IdName, x => (x.Rank, x.DelayMs), StringComparer.Ordinal);
    }

    private void PublishVoltageMarkers(OccurrenceItemViewModel item)
    {
        const EventCategory category = EventCategory.Voltage;
        var nominal = DomainConstants.NominalFor(category);

        // Each PMU is represented by its own worst reading within this event.
        var worstByIdName = item.Occurrence.Events
            .GroupBy(e => e.IdName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.MaxBy(e => Math.Abs(e.ExtremeValue - nominal))!,
                StringComparer.Ordinal);

        // The scale auto-fits this event and runs over *deviation from nominal*, not raw value.
        var deviations = worstByIdName.Values
            .Select(e => Math.Abs(e.ExtremeValue - nominal))
            .ToList();

        var minDeviation = deviations.Count == 0 ? 0 : deviations.Min();
        var maxDeviation = deviations.Count == 0 ? 0 : deviations.Max();
        var hasGradient = maxDeviation > minDeviation;
        var deviationRange = hasGradient
            ? new ScaleRange(minDeviation, maxDeviation)
            : new ScaleRange(minDeviation, minDeviation);

        var ranks = BuildDetectionRanks(item.Occurrence);
        var markers = new List<MarkerPayload>();
        foreach (var pmu in item.InvolvedPmus)
        {
            if (!worstByIdName.TryGetValue(pmu.IdName, out var worst))
                continue;

            var events = BuildMarkerEvents(item.Occurrence, pmu.IdName);
            var deviation = Math.Abs(worst.ExtremeValue - nominal);
            var (rank, delayMs) = ranks.TryGetValue(pmu.IdName, out var rd) ? rd : (0, 0.0);

            markers.Add(new MarkerPayload(
                pmu.IdName, pmu.Lat, pmu.Lon,
                VtcdScaleFor(worst).ColorFor(worst.ExtremeValue),
                MarkerVisualBuilder.DefaultRadiusPx,
                deviation,
                deviationRange.Normalize(deviation),
                events, rank, delayMs, pmu.FullName));
        }

        var worstOverall = worstByIdName.Values.MaxBy(e => Math.Abs(e.ExtremeValue - nominal))!;
        var isSobre = worstOverall.Direction == EventDirection.High;
        HasScale = false;
        HasVtcdSubLegend        = !isSobre;
        HasVtcdSobreLegend      = isSobre;
        HasFreqLegend           = false;
        HasDetectionRankLegend  = markers.Count > 1;
        MarkersReady?.Invoke(markers);
    }

    private void PublishFrequencyMarkers(OccurrenceItemViewModel item)
    {
        const string SubColor  = "#1565C0";
        const string SobreColor = "#C62828";
        const double Nominal   = 60.0;

        var ranks = BuildDetectionRanks(item.Occurrence);
        var markers = new List<MarkerPayload>();

        foreach (var pmu in item.InvolvedPmus)
        {
            var pmuEvents = item.Occurrence.Events
                .Where(e => string.Equals(e.IdName, pmu.IdName, StringComparison.Ordinal))
                .ToList();

            if (pmuEvents.Count == 0) continue;

            var worst = pmuEvents.MaxBy(e => Math.Abs(e.ExtremeValue - Nominal))!;
            var color = worst.ExtremeValue < Nominal ? SubColor : SobreColor;
            var (rank, delayMs) = ranks.TryGetValue(pmu.IdName, out var rd) ? rd : (0, 0.0);

            markers.Add(new MarkerPayload(
                pmu.IdName, pmu.Lat, pmu.Lon,
                color,
                MarkerVisualBuilder.DefaultRadiusPx,
                Math.Abs(worst.ExtremeValue - Nominal),
                0.5,
                BuildMarkerEvents(item.Occurrence, pmu.IdName), rank, delayMs, pmu.FullName));
        }

        HasScale               = false;
        HasVtcdSubLegend       = false;
        HasVtcdSobreLegend     = false;
        HasFreqLegend          = true;
        HasDetectionRankLegend = markers.Count > 1;
        MarkersReady?.Invoke(markers);
    }

    private static IReadOnlyList<MarkerEventPayload> BuildMarkerEvents(Occurrence occurrence, string idName) =>
        occurrence.Events
            .Where(e => string.Equals(e.IdName, idName, StringComparison.Ordinal))
            .OrderBy(e => e.StartTimestamp)
            .Select(e => new MarkerEventPayload(
                e.EventId, e.EventType, e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                e.StartTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                e.EndTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                e.ExtremeValue, e.QualityRaw))
            .ToList();

    private void UpdateScaleLegend(
        OccurrenceItemViewModel item, EventCategory category,
        IEnumerable<PmuEvent> worstPerPmu, double nominal, bool hasGradient)
    {
        var unit = category == EventCategory.Voltage ? "pu" : "Hz";
        var ordered = worstPerPmu.OrderBy(e => Math.Abs(e.ExtremeValue - nominal)).ToList();

        if (ordered.Count == 0)
        {
            HasScale = false;
            return;
        }

        // Read as a ranking, worst first: left end is the PMU furthest from nominal.
        ScaleLeftLabel = ordered.Last().ExtremeValue.ToString("0.0###", PtBr);
        ScaleRightLabel = ordered.First().ExtremeValue.ToString("0.0###", PtBr);

        ScaleCaption = hasGradient
            ? $"Severidade do evento ({unit})"
            : $"Severidade do evento ({unit}) — PMU única, sem gradiente";
        HasScale = true;
    }

    /// <summary>Exports the selected event's PMU-by-PMU detail, for use in reports/studies.</summary>
    [RelayCommand]
    private void ExportSelected()
    {
        if (SelectedOccurrence is null || SelectedOccurrenceEvents.Count == 0)
            return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar evento detectado",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"{SelectedOccurrence.Occurrence.Id}.csv",
        };

        if (dialog.ShowDialog() != true)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("ordem;idName;regiao;subestacao;tensao_nominal;event_type;grandeza;inicio;fim;duracao_s;valor_extremo;desvio;delta_t_ms;quality");
        foreach (var row in SelectedOccurrenceEvents)
        {
            sb.AppendLine(string.Join(';',
                row.Rank, row.IdName, row.Region, row.Station, row.VoltLevelLabel, row.EventType,
                row.SourceLabel, row.StartTime, row.EndTime, row.DurationLabel, row.ExtremeValue,
                row.DeviationLabel, row.RelativeMs.ToString("0", PtBr), row.Quality));
        }

        try
        {
            // UTF-8 with BOM so Excel opens the accented station names correctly.
            File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível salvar o arquivo:\n\n{ex.Message}",
                "Erro ao exportar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    partial void OnSortOptionChanged(OccurrenceSortOption value) => Refresh();

    private void Refresh() => Recompute(_lastFiltered, _pmusInScope, _lastCategoryFilter);
}
