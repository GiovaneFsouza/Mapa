using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapa.Core.Aggregation;
using Mapa.Core.Models;

namespace Mapa.App.ViewModels;

public sealed partial class GrandezaPanelViewModel : ObservableObject
{
    public EventCategory Category { get; }

    public string SubLabel   => Category == EventCategory.Voltage ? "Subtensão"   : "Subfrequência";
    public string SobreLabel => Category == EventCategory.Voltage ? "Sobretensão" : "Sobrefrequência";

    public GrandezaPanelViewModel(EventCategory category)
    {
        Category = category;
        Title = category == EventCategory.Voltage ? "Tensão" : "Frequência";
        RegionalQuantity.LowTooltip  = SubLabel;
        RegionalQuantity.HighTooltip = SobreLabel;
    }

    [ObservableProperty] private string _title;
    [ObservableProperty] private MetricMode _mode = MetricMode.Quantity;

    // Single threshold pair — always shown; represents the selected direction in directional modes
    [ObservableProperty] private string _thresholdL1 = "0";
    [ObservableProperty] private string _thresholdL2 = "0";

    [ObservableProperty] private string _scaleUnit = string.Empty;

    /// <summary>True for Severity and Duration — shows the direction selector above Limites.</summary>
    [ObservableProperty] private bool _hasDirectionalThresholds;

    /// <summary>Which direction is selected when HasDirectionalThresholds is true.</summary>
    [ObservableProperty] private EventDirection _selectedDirection = EventDirection.Low;

    /// <summary>True when the color scale is inverted (low values = red = worse).
    /// Applies only to Severity in Sub direction.</summary>
    public bool IsScaleInverted =>
        HasDirectionalThresholds &&
        SelectedDirection == EventDirection.Low &&
        Mode == MetricMode.Severity;

    // Color bar labels: green / yellow / red zones with their value ranges
    public string ColorBarGreenLabel  => ColorBarLabel(0);
    public string ColorBarYellowLabel => ColorBarLabel(1);
    public string ColorBarRedLabel    => ColorBarLabel(2);

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private string ColorBarLabel(int zone)
    {
        var l1 = FormatThresholdLabel(ThresholdL1);
        var l2 = FormatThresholdLabel(ThresholdL2);
        var unit = string.IsNullOrEmpty(ScaleUnit) ? "" : $" {ScaleUnit}";

        // Voltage severity: the green zone starts at the widest detection limit (config.json → detectors.vtcd.bands)
        var boundedByDetection = IsVoltageSeverity;

        if (IsScaleInverted)
        {
            // Sub Severity: lower value = worse; green = ≥ L2 (Superior), red = < L1 (Inferior)
            // Middle label shows L2 → L1 (high to low) to match visual top-to-bottom flow
            var green = boundedByDetection ? $"{Pu(Kv500LowerPu)} a {l2}{unit}" : $"≥ {l2}{unit}";
            return zone switch { 0 => green, 1 => $"{l2} a {l1}{unit}", _ => $"< {l1}{unit}" };
        }
        // Normal: higher value = worse; green = < L1, red = ≥ L2
        var greenNormal = boundedByDetection ? $"{Pu(DefaultUpperPu)} a {l1}{unit}" : $"< {l1}{unit}";
        return zone switch { 0 => greenNormal, 1 => $"{l1} a {l2}{unit}", _ => $"≥ {l2}{unit}" };
    }

    // VTCD detection bands (config.json → detectors.vtcd.bands)
    private const double DefaultLowerPu = 0.90;
    private const double DefaultUpperPu = 1.10;
    private const double Kv500LowerPu   = 0.945;
    private const double Kv500UpperPu   = 1.155;

    private static string Pu(double v) => v.ToString("0.00#", PtBr);

    private bool IsVoltageSeverity =>
        Category == EventCategory.Voltage && HasDirectionalThresholds && Mode == MetricMode.Severity;

    /// <summary>Explains which voltage values count as sub/sobretensão, including the 500 kV difference.</summary>
    public bool HasDetectionBandNote =>
        IsUndefinedMode || (Category == EventCategory.Voltage && HasDirectionalThresholds);

    /// <summary>True in the Indefinidos tab (Mode itself stays Quantity there, since events are just counted).</summary>
    [ObservableProperty] private bool _isUndefinedMode;

    partial void OnIsUndefinedModeChanged(bool value) => NotifyColorBarLabels();

    public string DetectionBandNote => IsUndefinedMode
        ? "Indefinidos: eventos em que o campo quality tem o bit 0 = 1 (início incerto) e/ou o bit 1 = 1 (fim incerto)."
        : SelectedDirection == EventDirection.Low
        ? $"Subtensão: tensão abaixo de {Pu(DefaultLowerPu)} pu.\nLinhas de 500 kV: abaixo de {Pu(Kv500LowerPu)} pu."
        : $"Sobretensão: tensão acima de {Pu(DefaultUpperPu)} pu.\nLinhas de 500 kV: acima de {Pu(Kv500UpperPu)} pu.";

    private static string FormatThresholdLabel(string s)
    {
        var norm = s.Replace(',', '.');
        return double.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? v.ToString("G", PtBr)
            : s;
    }

    private void NotifyColorBarLabels()
    {
        OnPropertyChanged(nameof(ColorBarGreenLabel));
        OnPropertyChanged(nameof(ColorBarYellowLabel));
        OnPropertyChanged(nameof(ColorBarRedLabel));
        OnPropertyChanged(nameof(HasDetectionBandNote));
        OnPropertyChanged(nameof(DetectionBandNote));
    }

    partial void OnSelectedDirectionChanged(EventDirection value)
    {
        OnPropertyChanged(nameof(IsSubSelected));
        OnPropertyChanged(nameof(IsSobreSelected));
        OnPropertyChanged(nameof(IsScaleInverted));
        NotifyColorBarLabels();
    }

    partial void OnModeChanged(MetricMode value)
    {
        OnPropertyChanged(nameof(IsScaleInverted));
        NotifyColorBarLabels();
    }

    partial void OnHasDirectionalThresholdsChanged(bool value)
    {
        OnPropertyChanged(nameof(IsScaleInverted));
        NotifyColorBarLabels();
    }

    partial void OnThresholdL1Changed(string value)  => NotifyColorBarLabels();
    partial void OnThresholdL2Changed(string value)  => NotifyColorBarLabels();
    partial void OnScaleUnitChanged(string value)    => NotifyColorBarLabels();

    public bool IsSubSelected
    {
        get => SelectedDirection == EventDirection.Low;
        set { if (value) SelectedDirection = EventDirection.Low; }
    }

    public bool IsSobreSelected
    {
        get => SelectedDirection == EventDirection.High;
        set { if (value) SelectedDirection = EventDirection.High; }
    }

    [ObservableProperty] private bool _hasUnmatchedIdNameWarning;
    [ObservableProperty] private string _unmatchedIdNameSummary = string.Empty;


    public RankingViewModel Ranking { get; } = new();
    public RegionalQuantityViewModel RegionalQuantity { get; } = new();
}
