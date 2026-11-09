using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapa.Core.Aggregation;
using Mapa.Core.Correlation;
using Mapa.Core.Filtering;
using Mapa.Core.Import;
using Mapa.Core.Models;
using Mapa.Core.Scales;
using Mapa.Core.Settings;
using Mapa.App.Services;
using Microsoft.Win32;

namespace Mapa.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly AggregationEngine _engine = new();
    private readonly RegionalAggregationEngine _regionalEngine = new();
    private readonly FrequencyCorrelationEngine _freqEngine = new();
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private List<PmuEvent> _allEvents = new();
    private List<Pmu> _allPmus = new();
    private bool _isUpdatingThresholds;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _reloadCts;

    public event Action<EventCategory, IReadOnlyList<MarkerPayload>>? MarkersReady;
    public event Action<EventCategory>? FitBoundsRequested;

    public FilterViewModel Filter { get; } = new();
    public GrandezaPanelViewModel VoltagePanel { get; } = new(EventCategory.Voltage);
    public FrequencyCorrelationViewModel FrequencyCorrelation { get; } = new();
    public OccurrencesViewModel Occurrences { get; } = new();

    // ── Type selector (Tensão / Frequência) ───────────────────────────
    [ObservableProperty] private bool _isShowingVoltage = true;

    public bool IsShowingFrequency
    {
        get => !IsShowingVoltage;
        set { if (value) IsShowingVoltage = false; }
    }


    [ObservableProperty] private string _bdedFolderPath = string.Empty;
    [ObservableProperty] private string _configJsonPath = string.Empty;
    [ObservableProperty] private string _statusMessage = "Nenhum dado carregado.";
    [ObservableProperty] private bool _showVoltagePanel = true;
    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private int _selectedTabIndex;

    public bool IsMapTabActive => SelectedTabIndex == 0;
    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsMapTabActive));
        OnPropertyChanged(nameof(IsVoltageMapTabActive));
    }

    /// <summary>Controls "Modo" selector visibility: only shown when Tensão tab is active.</summary>
    public bool IsVoltageMapTabActive => IsMapTabActive && IsShowingVoltage;
    partial void OnIsShowingVoltageChanged(bool value)
    {
        OnPropertyChanged(nameof(IsShowingFrequency));
        OnPropertyChanged(nameof(IsVoltageMapTabActive));
        Recompute();
    }

    public bool ArePathsConfigured =>
        !string.IsNullOrWhiteSpace(BdedFolderPath) && !string.IsNullOrWhiteSpace(ConfigJsonPath);

    public MainViewModel()
    {
        _settingsService = SettingsService.CreateDefault();
        _settings = _settingsService.Load();

        BdedFolderPath = _settings.BdedFolderPath ?? string.Empty;
        ConfigJsonPath = _settings.ConfigJsonPath ?? string.Empty;

        MigrateSeverityThresholds();
        LoadThresholdsIntoPanel(VoltagePanel, Filter.ActiveMode, EventCategory.Voltage);
        Occurrences.VtcdGapMaxMs   = _settings.VtcdGapMaxMs;
        Occurrences.VtcdRangeMaxMs = _settings.VtcdRangeMaxMs;
        Occurrences.FreqWindowMs   = _settings.OccurrencesFreqWindowMs;
        Occurrences.FreqMinPmus    = _settings.OccurrencesFreqMinPmus;
        Occurrences.SetVtcdThresholds(
            _settings.SeverityVoltageSubL1, _settings.SeverityVoltageSubL2,
            _settings.SeverityVoltageSobreL1, _settings.SeverityVoltageSobreL2);

        FrequencyCorrelation.CorrelationWindowSeconds = _settings.FrequencyCorrelationWindowSeconds;
        FrequencyCorrelation.MinimumPmus              = _settings.FrequencyMinimumPmus;

        Occurrences.PropertyChanged += OnOccurrencesPropertyChanged;
        Filter.PropertyChanged += OnFilterPropertyChanged;
        VoltagePanel.PropertyChanged += (_, e) => OnPanelPropertyChanged(e, EventCategory.Voltage);
        FrequencyCorrelation.PropertyChanged += OnFrequencyCorrelationSettingsChanged;

        if (ArePathsConfigured)
            _ = LoadDataAsync();
        else
            StatusMessage = "Configure a pasta do histórico de eventos e o arquivo config.json para começar.";
    }

    public void RepublishCurrentState() => Recompute();

    [RelayCommand]
    private void BrowseBdedFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Selecionar pasta do histórico de eventos" };
        if (dialog.ShowDialog() != true) return;
        BdedFolderPath = dialog.FolderName;
        _settings.BdedFolderPath = BdedFolderPath;
        _settingsService.Save(_settings);
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private void BrowseConfigJson()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json|Todos os arquivos (*.*)|*.*",
            Title = "Selecionar arquivo config.json",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true) return;
        ConfigJsonPath = dialog.FileName;
        _settings.ConfigJsonPath = ConfigJsonPath;
        _settingsService.Save(_settings);
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (!ArePathsConfigured)
        {
            StatusMessage = "Configure a pasta do histórico de eventos e o arquivo config.json para começar.";
            return;
        }

        IsLoading = true;
        try
        {
            try
            {
                _allPmus = ConfigJsonPmuReader.Read(ConfigJsonPath).ToList();
            }
            catch (Exception ex)
            {
                _allPmus = new List<Pmu>();
                StatusMessage = $"Falha ao ler config.json: {ex.Message}";
                MessageBox.Show(
                    $"Não foi possível ler o arquivo config.json:\n\n{ex.Message}",
                    "Erro ao carregar configuração", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            BdedImportResult result;
            try
            {
                result = await Task.Run(() => CsvBdedReader.ReadFolder(BdedFolderPath));
            }
            catch (Exception ex)
            {
                _allEvents = new List<PmuEvent>();
                StatusMessage = $"Falha ao ler pasta do histórico de eventos: {ex.Message}";
                return;
            }

            _allEvents = result.Events.ToList();
            Filter.SetAvailableRegions(_allPmus.Select(p => p.Area));

            if (_allEvents.Count > 0)
            {
                var minDate = _allEvents.Min(e => e.StartTimestamp);
                var maxDate = _allEvents.Max(e => e.StartTimestamp);
                Filter.SetDateRangeToData(minDate, maxDate);
            }

            SetupWatcher(BdedFolderPath);
        }
        finally
        {
            IsLoading = false;
        }

        Recompute();
    }

    private void SetupWatcher(string folder)
    {
        _watcher?.Dispose();
        _watcher = null;
        if (!Directory.Exists(folder)) return;
        _watcher = new FileSystemWatcher(folder, "*.csv")
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };
        _watcher.Created += OnBdedFileChanged;
        _watcher.Changed += OnBdedFileChanged;
        _watcher.Deleted += OnBdedFileChanged;
        _watcher.Renamed += (s, e) => OnBdedFileChanged(s, e);
    }

    private void OnBdedFileChanged(object sender, FileSystemEventArgs e)
    {
        _reloadCts?.Cancel();
        _reloadCts = new CancellationTokenSource();
        var token = _reloadCts.Token;
        Task.Delay(1500, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            Application.Current.Dispatcher.InvokeAsync(LoadDataAsync);
        }, TaskScheduler.Default);
    }

    private void OnOccurrencesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OccurrencesViewModel.VtcdGapMaxMs):
                _settings.VtcdGapMaxMs = Occurrences.VtcdGapMaxMs;
                break;
            case nameof(OccurrencesViewModel.VtcdRangeMaxMs):
                _settings.VtcdRangeMaxMs = Occurrences.VtcdRangeMaxMs;
                break;
            case nameof(OccurrencesViewModel.FreqWindowMs):
                _settings.OccurrencesFreqWindowMs = Occurrences.FreqWindowMs;
                break;
            case nameof(OccurrencesViewModel.FreqMinPmus):
                _settings.OccurrencesFreqMinPmus = Occurrences.FreqMinPmus;
                break;
            default:
                return;
        }
        _settingsService.Save(_settings);
    }

    private void OnFrequencyCorrelationSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(FrequencyCorrelationViewModel.CorrelationWindowSeconds)
                               or nameof(FrequencyCorrelationViewModel.MinimumPmus))) return;
        _settings.FrequencyCorrelationWindowSeconds = FrequencyCorrelation.CorrelationWindowSeconds;
        _settings.FrequencyMinimumPmus              = FrequencyCorrelation.MinimumPmus;
        _settingsService.Save(_settings);
        Recompute();
    }

    private void OnFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilterViewModel.ActiveMode))
            LoadThresholdsIntoPanel(VoltagePanel, Filter.ActiveMode, EventCategory.Voltage);
        Recompute();
    }

    private void OnPanelPropertyChanged(PropertyChangedEventArgs e, EventCategory category)
    {
        if (_isUpdatingThresholds) return;

        var panel = VoltagePanel; // only voltage uses threshold panels now

        if (e.PropertyName == nameof(GrandezaPanelViewModel.SelectedDirection))
        {
            // Direction switched — load the stored thresholds for the new direction, then recompute
            LoadThresholdsIntoPanel(panel, Filter.ActiveMode, category);
            Recompute();
            return;
        }

        var isThreshold = e.PropertyName is
            nameof(GrandezaPanelViewModel.ThresholdL1) or
            nameof(GrandezaPanelViewModel.ThresholdL2);
        if (!isThreshold) return;

        SaveThresholdsFromPanel(panel, Filter.ActiveMode, category);
        _settingsService.Save(_settings);

        if (category == EventCategory.Voltage && Filter.ActiveMode == MetricMode.Severity)
            Occurrences.SetVtcdThresholds(
                _settings.SeverityVoltageSubL1, _settings.SeverityVoltageSubL2,
                _settings.SeverityVoltageSobreL1, _settings.SeverityVoltageSobreL2);

        Recompute();
    }

    // ── Threshold helpers ──────────────────────────────────────────────────

    private static bool IsDirectionalMode(MetricMode mode) =>
        mode == MetricMode.Severity || mode == MetricMode.Duration;

    private void LoadThresholdsIntoPanel(GrandezaPanelViewModel panel, MetricMode mode, EventCategory category)
    {
        _isUpdatingThresholds = true;
        try
        {
            var directional = IsDirectionalMode(mode);
            panel.HasDirectionalThresholds = directional;
            panel.ScaleUnit = BuildScaleUnit(mode, category);

            double l1, l2;
            if (directional)
            {
                var isSub = panel.SelectedDirection == EventDirection.Low;
                var (sl1, sl2, ol1, ol2) = GetSavedDirectionalThresholds(mode, category);
                l1 = isSub ? sl1 : ol1;
                l2 = isSub ? sl2 : ol2;
            }
            else
            {
                (l1, l2) = GetSavedThresholds(mode, category);
            }

            panel.ThresholdL1 = FormatThreshold(l1);
            panel.ThresholdL2 = FormatThreshold(l2);
        }
        finally
        {
            _isUpdatingThresholds = false;
        }
    }

    private (double L1, double L2) GetSavedThresholds(MetricMode mode, EventCategory category) =>
        (mode, category) switch
        {
            (MetricMode.Quantity,  EventCategory.Voltage)   => (_settings.QuantityVoltageL1,  _settings.QuantityVoltageL2),
            (MetricMode.Quantity,  EventCategory.Frequency) => (_settings.QuantityFreqL1,     _settings.QuantityFreqL2),
            (MetricMode.Undefined, EventCategory.Voltage)   => (_settings.UndefinedVoltageL1, _settings.UndefinedVoltageL2),
            (MetricMode.Undefined, EventCategory.Frequency) => (_settings.UndefinedFreqL1,    _settings.UndefinedFreqL2),
            _ => (1, 10),
        };

    private (double SubL1, double SubL2, double SobreL1, double SobreL2) GetSavedDirectionalThresholds(
        MetricMode mode, EventCategory category) =>
        (mode, category) switch
        {
            (MetricMode.Severity, EventCategory.Voltage)   => (_settings.SeverityVoltageSubL1,   _settings.SeverityVoltageSubL2,
                                                               _settings.SeverityVoltageSobreL1, _settings.SeverityVoltageSobreL2),
            (MetricMode.Severity, EventCategory.Frequency) => (_settings.SeverityFreqSubL1,      _settings.SeverityFreqSubL2,
                                                               _settings.SeverityFreqSobreL1,    _settings.SeverityFreqSobreL2),
            (MetricMode.Duration, EventCategory.Voltage)   => (_settings.DurationVoltageSubL1,   _settings.DurationVoltageSubL2,
                                                               _settings.DurationVoltageSobreL1, _settings.DurationVoltageSobreL2),
            (MetricMode.Duration, EventCategory.Frequency) => (_settings.DurationFreqSubL1,      _settings.DurationFreqSubL2,
                                                               _settings.DurationFreqSobreL1,    _settings.DurationFreqSobreL2),
            _ => (1, 5, 1, 5),
        };

    private void SaveThresholdsFromPanel(GrandezaPanelViewModel panel, MetricMode mode, EventCategory category)
    {
        var l1 = ParseThreshold(panel.ThresholdL1);
        var l2 = ParseThreshold(panel.ThresholdL2);

        if (IsDirectionalMode(mode))
        {
            var isSub = panel.SelectedDirection == EventDirection.Low;
            switch ((mode, category))
            {
                case (MetricMode.Severity, EventCategory.Voltage):
                    if (isSub) { _settings.SeverityVoltageSubL1   = l1; _settings.SeverityVoltageSubL2   = l2; }
                    else        { _settings.SeverityVoltageSobreL1 = l1; _settings.SeverityVoltageSobreL2 = l2; } break;
                case (MetricMode.Severity, EventCategory.Frequency):
                    if (isSub) { _settings.SeverityFreqSubL1      = l1; _settings.SeverityFreqSubL2      = l2; }
                    else        { _settings.SeverityFreqSobreL1    = l1; _settings.SeverityFreqSobreL2    = l2; } break;
                case (MetricMode.Duration, EventCategory.Voltage):
                    if (isSub) { _settings.DurationVoltageSubL1   = l1; _settings.DurationVoltageSubL2   = l2; }
                    else        { _settings.DurationVoltageSobreL1 = l1; _settings.DurationVoltageSobreL2 = l2; } break;
                case (MetricMode.Duration, EventCategory.Frequency):
                    if (isSub) { _settings.DurationFreqSubL1      = l1; _settings.DurationFreqSubL2      = l2; }
                    else        { _settings.DurationFreqSobreL1    = l1; _settings.DurationFreqSobreL2    = l2; } break;
            }
        }
        else
        {
            switch ((mode, category))
            {
                case (MetricMode.Quantity,  EventCategory.Voltage):   _settings.QuantityVoltageL1  = l1; _settings.QuantityVoltageL2  = l2; break;
                case (MetricMode.Quantity,  EventCategory.Frequency): _settings.QuantityFreqL1     = l1; _settings.QuantityFreqL2     = l2; break;
                case (MetricMode.Undefined, EventCategory.Voltage):   _settings.UndefinedVoltageL1 = l1; _settings.UndefinedVoltageL2 = l2; break;
                case (MetricMode.Undefined, EventCategory.Frequency): _settings.UndefinedFreqL1    = l1; _settings.UndefinedFreqL2    = l2; break;
            }
        }
    }

    /// <summary>
    /// Resets Severity thresholds if they're in the old deviation format (values &lt; 1.0 for voltage).
    /// This handles the one-time migration from the deviation-based to actual-value-based system.
    /// </summary>
    private void MigrateSeverityThresholds()
    {
        bool changed = false;
        // Old deviation-based format (L1 < 0.5 for voltage) → reset to actual-value defaults
        if (_settings.SeverityVoltageSubL1 < 0.5 || _settings.SeverityVoltageSobreL1 < 0.5)
        {
            _settings.SeverityVoltageSubL1   = 0.70; _settings.SeverityVoltageSubL2   = 0.90;
            _settings.SeverityVoltageSobreL1 = 1.10; _settings.SeverityVoltageSobreL2 = 1.30;
            changed = true;
        }
        else if (_settings.SeverityVoltageSubL1 > _settings.SeverityVoltageSubL2)
        {
            // Old L1 > L2 inverted format → swap to new convention (L1 = Inferior = lower value)
            (_settings.SeverityVoltageSubL1, _settings.SeverityVoltageSubL2) =
                (_settings.SeverityVoltageSubL2, _settings.SeverityVoltageSubL1);
            changed = true;
        }
        if (_settings.SeverityFreqSubL1 < 1.0)
        {
            _settings.SeverityFreqSubL1      = 58.0; _settings.SeverityFreqSubL2      = 59.5;
            _settings.SeverityFreqSobreL1    = 60.5; _settings.SeverityFreqSobreL2    = 62.0;
            changed = true;
        }
        else if (_settings.SeverityFreqSubL1 > _settings.SeverityFreqSubL2)
        {
            (_settings.SeverityFreqSubL1, _settings.SeverityFreqSubL2) =
                (_settings.SeverityFreqSubL2, _settings.SeverityFreqSubL1);
            changed = true;
        }
        if (changed) _settingsService.Save(_settings);
    }

    private static string FormatThreshold(double value) =>
        value == Math.Floor(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("G", CultureInfo.InvariantCulture);

    private static double ParseThreshold(string s)
    {
        var normalized = s.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    // ── Recompute ──────────────────────────────────────────────────────────

    private const string UndefinedEventType = "UNDEFINED";

    private void Recompute()
    {
        var mismatch = IdNameValidator.Validate(_allEvents, _allPmus);
        ApplyMismatchWarning(VoltagePanel, mismatch);

        var pmusInScope = PmusInScope();

        // Period-filtered events (all categories)
        var criteria = new FilterCriteria(Filter.CombinedStart, Filter.CombinedEnd, null);
        IReadOnlyList<PmuEvent> filtered;

        var isUndefinedMode = Filter.ActiveMode == MetricMode.Undefined;
        var inPeriod = EventFilterService.Filter(_allEvents, criteria);
        filtered = inPeriod
            .Where(e => IsUndefined(e) == isUndefinedMode)
            .Where(e => isUndefinedMode || !Filter.OnlyQualityOk || e.IsQualityOk)
            .ToList();

        // The frequency tab has no Modo selector, so it always gets the characterised events and
        // reports its own Indefinidos separately from the VTCD ones.
        var frequencyInPeriod = inPeriod.Where(e => e.Category == EventCategory.Frequency).ToList();
        var frequencyEvents = frequencyInPeriod
            .Where(e => !IsUndefined(e) && (!Filter.OnlyQualityOk || e.IsQualityOk)).ToList();

        if (!ArePathsConfigured)
            StatusMessage = "Configure a pasta do histórico de eventos e o arquivo config.json para começar.";
        else if (_allPmus.Count == 0)
            StatusMessage = "Nenhum PMU encontrado no config.json (verifique se o arquivo tem entradas com latitude/longitude).";
        else if (_allEvents.Count == 0)
            StatusMessage = $"Nenhum evento encontrado na pasta do histórico de eventos \"{BdedFolderPath}\".";
        else
        {
            // Count only the grandeza on screen, so the line agrees with the panel below it
            var category = IsShowingVoltage ? EventCategory.Voltage : EventCategory.Frequency;
            var shown = IsShowingVoltage
                ? filtered.Count(e => e.Category == EventCategory.Voltage)
                : frequencyEvents.Count;
            var total = _allEvents.Count(e => e.Category == category);
            var label = IsShowingVoltage
                ? (isUndefinedMode ? "evento(s) indefinido(s) de VTCD" : "evento(s) de VTCD")
                : "evento(s) de desvio de frequência";
            var regionSuffix = Filter.HasRegionFilter ? $" — região {Filter.SelectedRegion}" : string.Empty;
            StatusMessage = $"{shown} de {total} {label} no período selecionado — " +
                            $"{pmusInScope.Count} PMUs em config.json{regionSuffix}.";
        }

        ShowVoltagePanel = IsShowingVoltage;

        if (IsShowingVoltage)
            RecomputePanel(VoltagePanel, filtered, pmusInScope, EventCategory.Voltage);

        if (!IsShowingVoltage)
        {
            var scopeIds = pmusInScope.Select(p => p.IdName).ToHashSet(StringComparer.Ordinal);
            RecomputeFrequency(frequencyEvents, pmusInScope,
                frequencyInPeriod.Count(e => IsUndefined(e) && scopeIds.Contains(e.IdName)));
        }

        Occurrences.Recompute(filtered, pmusInScope, null);
    }

    private static bool IsUndefined(PmuEvent e) =>
        e.EventType.Equals(UndefinedEventType, StringComparison.OrdinalIgnoreCase);

    private void RecomputeFrequency(IReadOnlyList<PmuEvent> filtered, IReadOnlyList<Pmu> pmusInScope, int undefinedCount)
    {
        var window = TimeSpan.FromSeconds(Math.Max(0.1, FrequencyCorrelation.CorrelationWindowSeconds));
        var minPmus = Math.Max(1, FrequencyCorrelation.MinimumPmus);
        var correlated = _freqEngine.Correlate(filtered, pmusInScope, window, minPmus);
        FrequencyCorrelation.Refresh(correlated, undefinedCount);
    }

    private IReadOnlyList<Pmu> PmusInScope() =>
        Filter.HasRegionFilter
            ? _allPmus.Where(p => string.Equals(p.Area, Filter.SelectedRegion, StringComparison.Ordinal)).ToList()
            : _allPmus;

    private static void ApplyMismatchWarning(GrandezaPanelViewModel panel, IdNameValidationResult mismatch)
    {
        panel.HasUnmatchedIdNameWarning = mismatch.HasMismatches;
        panel.UnmatchedIdNameSummary = mismatch.HasMismatches
            ? $"pmu_id sem correspondência no config.json ({mismatch.UnmatchedIdNames.Count}): " +
              string.Join(", ", mismatch.UnmatchedIdNames.Take(5)) +
              (mismatch.UnmatchedIdNames.Count > 5 ? "…" : "")
            : string.Empty;
    }

    private void RecomputePanel(
        GrandezaPanelViewModel panel, IReadOnlyList<PmuEvent> filtered,
        IReadOnlyList<Pmu> pmusInScope, EventCategory category)
    {
        var mode = Filter.ActiveMode;
        var aggregationMode = mode == MetricMode.Undefined ? MetricMode.Quantity : mode;
        panel.Mode = aggregationMode;
        panel.IsUndefinedMode = mode == MetricMode.Undefined;

        // In directional modes, restrict events to the selected direction
        var eventsForPanel = IsDirectionalMode(mode)
            ? filtered.Where(e => e.Direction == panel.SelectedDirection).ToList()
            : (IReadOnlyList<PmuEvent>)filtered;

        if (aggregationMode == MetricMode.Quantity)
            UpdateRegionalQuantity(panel, filtered, pmusInScope, category, mode == MetricMode.Undefined);

        var isSub = IsDirectionalMode(mode) && panel.SelectedDirection == EventDirection.Low;
        var isInverted = isSub && mode == MetricMode.Severity;
        var scale = new ThresholdScale(ParseThreshold(panel.ThresholdL1), ParseThreshold(panel.ThresholdL2), isInverted);
        var aggregates = _engine.Aggregate(eventsForPanel, pmusInScope, category, aggregationMode);

        UpdateRanking(panel, aggregates, aggregationMode, category, scale);

        var markers = aggregates.Select(a => BuildMarkerPayload(a, scale)).ToList();
        MarkersReady?.Invoke(category, markers);
        FitBoundsRequested?.Invoke(category);
    }

    // ── Ranking ────────────────────────────────────────────────────────────

    private const int TopN = 10;

    private static void UpdateRanking(
        GrandezaPanelViewModel panel, IReadOnlyList<PmuAggregate> aggregates,
        MetricMode mode, EventCategory category, ThresholdScale scale)
    {
        panel.Ranking.HasSecondary = false;
        panel.Ranking.ReplaceSecondary(Array.Empty<RankingItemViewModel>());

        const string head = "Top 10 PMUs/Terminais";
        panel.Ranking.PrimaryTitle = head;
        panel.Ranking.ValueHeader = mode switch
        {
            MetricMode.Severity => SeverityHeader(category, panel.SelectedDirection),
            MetricMode.Duration => "Duração máxima do evento",
            _                   => "Quantidade de eventos",
        };

        var largest = aggregates.Count == 0 ? 0 : aggregates.Max(a => a.MetricValue);

        if (mode == MetricMode.Severity)
        {
            var nominal = DomainConstants.NominalFor(category);
            var isSub = panel.SelectedDirection == EventDirection.Low;

            // "Badness" = how far from nominal in the bad direction (always positive, larger = worse)
            Func<double, double> badness = isSub
                ? v => nominal - v   // subtensão: lower value = worse
                : v => v - nominal;  // sobretensão: higher value = worse

            var maxBadness = aggregates.Count == 0 ? 0 : aggregates.Max(a => badness(a.MetricValue));

            panel.Ranking.ReplacePrimary(aggregates
                .OrderByDescending(a => badness(a.MetricValue))
                .Take(TopN)
                .Select(a =>
                {
                    var label = a.MetricValue.ToString("0.0000", PtBr) +
                                (category == EventCategory.Frequency ? " Hz" : "");
                    return new RankingItemViewModel(
                        a.Pmu.FullName, label,
                        maxBadness <= 0 ? 0 : badness(a.MetricValue) / maxBadness,
                        scale.ColorFor(a.MetricValue),
                        BuildRankingTooltip(a, MetricMode.Severity, category));
                }));
        }
        else
        {
            panel.Ranking.ReplacePrimary(aggregates
                .OrderByDescending(a => a.MetricValue)
                .Take(TopN)
                .Select(a => new RankingItemViewModel(
                    a.Pmu.FullName,
                    FormatValue(a.MetricValue, mode, category),
                    largest <= 0 ? 0 : a.MetricValue / largest,
                    scale.ColorFor(a.MetricValue),
                    BuildRankingTooltip(a, mode, category))));
        }
    }

    private void UpdateRegionalQuantity(
        GrandezaPanelViewModel panel, IReadOnlyList<PmuEvent> filtered,
        IReadOnlyList<Pmu> pmusInScope, EventCategory category, bool isUndefinedMode = false)
    {
        var regionCounts = _regionalEngine.Aggregate(filtered, pmusInScope, category);

        if (isUndefinedMode)
        {
            var maxCount = regionCounts.Count == 0 ? 0 : regionCounts.Max(r => r.IndeterminateCount);
            double Frac(int c) => maxCount == 0 ? 0 : (double)c / maxCount;
            panel.RegionalQuantity.Replace(regionCounts
                .Where(r => r.IndeterminateCount > 0)
                .Select(r => new RegionBarViewModel(r.Region, r.IndeterminateCount, 0, Frac(r.IndeterminateCount), 0)));
        }
        else
        {
            var maxSingle = regionCounts.Count == 0 ? 0 : regionCounts.Max(r => Math.Max(r.LowCount, r.HighCount));
            double Fraction(int count) => maxSingle == 0 ? 0 : (double)count / maxSingle;
            panel.RegionalQuantity.Replace(regionCounts.Select(r => new RegionBarViewModel(
                r.Region, r.LowCount, r.HighCount, Fraction(r.LowCount), Fraction(r.HighCount))));
        }
    }

    // ── Marker payload ─────────────────────────────────────────────────────

    private static MarkerPayload BuildMarkerPayload(PmuAggregate agg, ThresholdScale scale)
    {
        var visual = MarkerVisualBuilder.Build(agg.MetricValue, scale);

        var events = agg.MatchingEvents
            .OrderByDescending(e => e.StartTimestamp)
            .Select(e => new MarkerEventPayload(
                e.EventId, e.EventType, e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                e.StartTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                e.EndTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                e.ExtremeValue, e.QualityRaw))
            .ToList();

        var intensity = scale.IntensityFor(agg.MetricValue);

        return new MarkerPayload(
            agg.Pmu.IdName, agg.Pmu.Lat, agg.Pmu.Lon,
            visual.ColorHex, visual.RadiusPx, agg.MetricValue, intensity, events,
            Label: agg.Pmu.FullName);
    }

    // ── Ranking tooltip ────────────────────────────────────────────────────

    private static string BuildRankingTooltip(PmuAggregate agg, MetricMode mode, EventCategory category)
    {
        var nominal = DomainConstants.NominalFor(category);

        IEnumerable<PmuEvent> toShow = mode switch
        {
            MetricMode.Severity => new[] { agg.MatchingEvents.MaxBy(e => Math.Abs(e.ExtremeValue - nominal))! },
            MetricMode.Duration => new[] { agg.MatchingEvents.MaxBy(e => e.Duration)! },
            _                   => agg.MatchingEvents.OrderBy(e => e.StartTimestamp),
        };

        var events = toShow.Take(5).ToList();
        var sb = new System.Text.StringBuilder();

        if (mode == MetricMode.Quantity && agg.MatchingEvents.Count > 1)
            sb.AppendLine($"{agg.MatchingEvents.Count} evento(s)");

        for (int i = 0; i < events.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            var ev = events[i];
            sb.AppendLine($"Data:    {ev.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Início:  {ev.StartTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Fim:     {ev.EndTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)}");
            sb.Append(    $"Quality: {ev.QualityRaw}");
        }

        if (mode == MetricMode.Quantity && agg.MatchingEvents.Count > 5)
            sb.Append($"\n... e mais {agg.MatchingEvents.Count - 5}");

        return sb.ToString();
    }

    // ── Formatting ─────────────────────────────────────────────────────────

    private static string FormatValue(double value, MetricMode mode, EventCategory category) => mode switch
    {
        MetricMode.Quantity => ((int)value).ToString(PtBr),
        MetricMode.Duration => FormatDuration(value, category),
        _ => value.ToString("0.0000", PtBr) + (category == EventCategory.Frequency ? " Hz" : ""),
    };

    private static string SeverityHeader(EventCategory category, EventDirection direction) =>
        (category, direction) switch
        {
            (EventCategory.Voltage, EventDirection.Low)   => "Tensão mínima (pu)",
            (EventCategory.Voltage, _)                    => "Tensão máxima (pu)",
            (_, EventDirection.Low)                       => "Frequência mínima (Hz)",
            _                                             => "Frequência máxima (Hz)",
        };

    private static string BuildScaleUnit(MetricMode mode, EventCategory category) => mode switch
    {
        MetricMode.Severity => category == EventCategory.Voltage ? "pu" : "Hz",
        MetricMode.Duration => DomainConstants.DurationUnitFor(category),
        _ => "eventos",
    };

    private static string FormatDuration(double value, EventCategory category) =>
        $"{value.ToString(category == EventCategory.Voltage ? "0" : "0.##", PtBr)} " +
        DomainConstants.DurationUnitFor(category);
}
