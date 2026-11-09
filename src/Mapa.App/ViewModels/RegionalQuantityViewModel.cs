using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mapa.App.ViewModels;

public sealed partial class RegionBarViewModel : ObservableObject
{
    public RegionBarViewModel(string regionLabel, int lowCount, int highCount, double lowFraction, double highFraction)
    {
        RegionLabel = regionLabel;
        LowCount = lowCount;
        HighCount = highCount;
        LowFraction = lowFraction;
        HighFraction = highFraction;
    }

    [ObservableProperty] private string _regionLabel;
    [ObservableProperty] private int _lowCount;
    [ObservableProperty] private int _highCount;

    /// <summary>0..1, relative to the largest single-direction count across all regions.</summary>
    [ObservableProperty] private double _lowFraction;
    [ObservableProperty] private double _highFraction;

    public int Total => LowCount + HighCount;
}

/// <summary>
/// Regional breakdown shown alongside the map/ranking for every mode: event counts grouped by
/// PMU region (Pmu.Area), split by direction (sag/swell for tensão, under/over for frequência).
/// </summary>
public sealed partial class RegionalQuantityViewModel : ObservableObject
{
    public ObservableCollection<RegionBarViewModel> Regions { get; } = new();

    [ObservableProperty] private string _lowLabel = "Sub";
    [ObservableProperty] private string _highLabel = "Sobre";
    [ObservableProperty] private string _lowTooltip = "Subtensão";
    [ObservableProperty] private string _highTooltip = "Sobretensão";

    public void Replace(IEnumerable<RegionBarViewModel> regions)
    {
        Regions.Clear();
        foreach (var region in regions)
            Regions.Add(region);
    }
}
