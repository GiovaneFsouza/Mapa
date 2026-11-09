using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapa.Core.Aggregation;

namespace Mapa.App.ViewModels;

/// <summary>Period, event-type, region and active-mode filter state shared by both tabs.</summary>
public sealed partial class FilterViewModel : ObservableObject
{
    /// <summary>Sentinel for "no region filter" — regions themselves come from the loaded XML.</summary>
    public const string AllRegionsLabel = "Todas";

    [ObservableProperty] private DateTime _startDate = DateTime.Today.AddYears(-1);
    [ObservableProperty] private string _startTimeText = "00:00";

    [ObservableProperty] private DateTime _endDate = DateTime.Today;
    [ObservableProperty] private string _endTimeText = "23:59";

    [ObservableProperty] private EventTypeFilterOption _eventTypeFilter = EventTypeFilterOption.Ambos;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQualityFilterApplicable))]
    private MetricMode _activeMode = MetricMode.Quantity;

    /// <summary>Keeps only events whose quality field has every bit = 0.</summary>
    [ObservableProperty] private bool _onlyQualityOk;

    /// <summary>Indefinidos are by definition events with uncertain start/end bits, so the filter doesn't apply there.</summary>
    public bool IsQualityFilterApplicable => ActiveMode != MetricMode.Undefined;

    public ObservableCollection<string> Regions { get; } = new() { AllRegionsLabel };

    [ObservableProperty] private string _selectedRegion = AllRegionsLabel;

    public bool HasRegionFilter => SelectedRegion != AllRegionsLabel;

    /// <summary>Sets the period to cover the full date range found in the data.</summary>
    public void SetDateRangeToData(DateTime minDate, DateTime maxDate)
    {
        StartDate = minDate.Date;
        StartTimeText = "00:00";
        EndDate = maxDate.Date;
        EndTimeText = "23:59";
    }

    /// <summary>Refreshes the picker from the loaded PMUs, keeping the current pick when still valid.</summary>
    public void SetAvailableRegions(IEnumerable<string> regions)
    {
        var previous = SelectedRegion;

        // Edit in place (never Clear): clearing the list makes the ComboBox push null into
        // SelectedRegion, leaving the picker blank instead of on "Todas".
        var wanted = regions.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal).ToList();

        for (var i = Regions.Count - 1; i >= 1; i--)
            if (!wanted.Contains(Regions[i]))
                Regions.RemoveAt(i);
        for (var i = 0; i < wanted.Count; i++)
        {
            if (Regions.Count > i + 1 && Regions[i + 1] == wanted[i]) continue;
            Regions.Insert(i + 1, wanted[i]);
        }

        var target = previous is not null && Regions.Contains(previous) ? previous : AllRegionsLabel;
        if (SelectedRegion != target) SelectedRegion = target;
    }

    public DateTime CombinedStart => StartDate.Date + ParseTime(StartTimeText);

    public DateTime CombinedEnd => EndDate.Date + ParseTime(EndTimeText);

    private static TimeSpan ParseTime(string text) =>
        TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var t) ? t : TimeSpan.Zero;
}
