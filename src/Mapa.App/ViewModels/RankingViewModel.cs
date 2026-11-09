using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mapa.App.ViewModels;

public sealed partial class RankingItemViewModel : ObservableObject
{
    public RankingItemViewModel(string label, string valueDisplay, double fraction, string colorHex, string? tooltipText = null)
    {
        Label = label;
        ValueDisplay = valueDisplay;
        Fraction = fraction;
        ColorHex = colorHex;
        TooltipText = tooltipText;
    }

    [ObservableProperty] private string _label;
    [ObservableProperty] private string _valueDisplay;

    /// <summary>0..1, bar length relative to the worst case currently on screen.</summary>
    [ObservableProperty] private double _fraction;

    [ObservableProperty] private string _colorHex;
    [ObservableProperty] private string? _tooltipText;
}

/// <summary>
/// Ranking panel. Only worst cases are listed — the bottom of a ranking ("as PMUs menos
/// afetadas") carries no operational meaning. Quantidade and Duração therefore show a single
/// top-10 list; Severidade splits into two top-5 lists, one per direction (sub e sobretensão,
/// ou sub e sobrefrequência), because those are different phenomena and must not be mixed in
/// one ordering.
/// </summary>
public sealed partial class RankingViewModel : ObservableObject
{
    public ObservableCollection<RankingItemViewModel> Primary { get; } = new();
    public ObservableCollection<RankingItemViewModel> Secondary { get; } = new();

    [ObservableProperty] private string _primaryTitle = string.Empty;
    /// <summary>Caption above the value column (e.g. "quantidade de eventos").</summary>
    [ObservableProperty] private string _valueHeader = string.Empty;
    [ObservableProperty] private string _secondaryTitle = string.Empty;

    /// <summary>False for the single-list modes, so the second block disappears entirely.</summary>
    [ObservableProperty] private bool _hasSecondary;

    public void ReplacePrimary(IEnumerable<RankingItemViewModel> items) => Replace(Primary, items);

    public void ReplaceSecondary(IEnumerable<RankingItemViewModel> items) => Replace(Secondary, items);

    private static void Replace(ObservableCollection<RankingItemViewModel> target, IEnumerable<RankingItemViewModel> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}
