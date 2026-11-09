using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapa.Core.Correlation;

namespace Mapa.App.ViewModels;

public sealed partial class FrequencyCorrelationViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public ObservableCollection<FrequencyEventRowViewModel> Events { get; } = new();

    public IReadOnlyList<FrequencyEventRowViewModel> BySeverity { get; private set; } = [];
    public IReadOnlyList<FrequencyEventRowViewModel> ByDuration { get; private set; } = [];

    [ObservableProperty] private FrequencyEventRowViewModel? _selectedEvent;

    // Summary stats
    [ObservableProperty] private int    _totalEvents;
    [ObservableProperty] private int    _subCount;
    [ObservableProperty] private int    _sobreCount;
    [ObservableProperty] private int    _mistoCount;
    /// <summary>Frequency events the detector couldn't characterise (not correlated, just counted).</summary>
    [ObservableProperty] private int    _undefinedCount;
    [ObservableProperty] private string _statusLabel = "Nenhum dado carregado.";

    // Editable correlation settings — MainViewModel observes changes and reruns the engine
    [ObservableProperty] private double _correlationWindowSeconds = 2.0;
    [ObservableProperty] private int    _minimumPmus = 3;

    /// <summary>View-facing wrapper: janela em millisegundos.</summary>
    public double CorrelationWindowMs
    {
        get => CorrelationWindowSeconds * 1000.0;
        set => CorrelationWindowSeconds = value / 1000.0;
    }

    partial void OnCorrelationWindowSecondsChanged(double value) =>
        OnPropertyChanged(nameof(CorrelationWindowMs));

    public void Refresh(IReadOnlyList<CorrelatedFrequencyEvent> correlatedEvents, int undefinedCount = 0)
    {
        UndefinedCount = undefinedCount;
        var rows = correlatedEvents.Select(BuildRow).ToList();

        SelectedEvent = null;
        Events.Clear();
        foreach (var r in rows) Events.Add(r);

        BySeverity = rows.OrderByDescending(r => r.MaxDeviationHz).Take(10).ToList();
        ByDuration = rows.OrderByDescending(r => r.DurationSeconds).Take(10).ToList();
        OnPropertyChanged(nameof(BySeverity));
        OnPropertyChanged(nameof(ByDuration));

        TotalEvents = rows.Count;
        SubCount    = correlatedEvents.Count(e => e.EventType == FrequencyEventDirection.Sub);
        SobreCount  = correlatedEvents.Count(e => e.EventType == FrequencyEventDirection.Sobre);
        MistoCount  = correlatedEvents.Count(e => e.EventType == FrequencyEventDirection.Misto);
        StatusLabel = rows.Count == 0
            ? "Nenhum evento correlacionado no período."
            : $"{TotalEvents} evento(s) — {SubCount} sub  ·  {SobreCount} sobre  ·  {MistoCount} misto";
    }

    private static FrequencyEventRowViewModel BuildRow(CorrelatedFrequencyEvent ev)
    {
        var typeLabel = ev.EventType switch
        {
            FrequencyEventDirection.Sub   => "Sub",
            FrequencyEventDirection.Sobre => "Sobre",
            _                             => "Misto",
        };

        var subLabel = ev.MedianSubHz.HasValue
            ? $"{ev.MedianSubHz.Value.ToString("0.000", PtBr)} Hz ({ev.SubParticipants.Count})"
            : "—";
        var sobreLabel = ev.MedianSobreHz.HasValue
            ? $"{ev.MedianSobreHz.Value.ToString("0.000", PtBr)} Hz ({ev.SobreParticipants.Count})"
            : "—";

        var subDev   = ev.MedianSubHz.HasValue   ? 60.0 - ev.MedianSubHz.Value   : 0.0;
        var sobreDev = ev.MedianSobreHz.HasValue ? ev.MedianSobreHz.Value - 60.0 : 0.0;
        var maxDev   = Math.Max(subDev, sobreDev);

        bool subWins  = subDev >= sobreDev;
        var extremeHz = subWins
            ? (ev.MedianSubHz   ?? 60.0 - maxDev)
            : (ev.MedianSobreHz ?? 60.0 + maxDev);
        var signedDev = subWins ? -maxDev : +maxDev;
        var devSign   = signedDev >= 0 ? "+" : "−";

        return new FrequencyEventRowViewModel
        {
            StartTime        = ev.StartTime,
            StartTimeLabel   = ev.StartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateLabel        = ev.StartTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeLabel        = ev.StartTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            TypeLabel        = typeLabel,
            SubHzLabel       = subLabel,
            SobreHzLabel     = sobreLabel,
            DurationLabel    = $"{ev.MedianDurationSeconds.ToString("0.##", PtBr)} s",
            DurationSeconds  = ev.MedianDurationSeconds,
            ParticipantCount = ev.Participants.Count,
            MaxDeviationHz   = maxDev,
            DeviationLabel   = $"{devSign}{maxDev.ToString("0.000", PtBr)} Hz",
            ExtremeHzLabel   = $"{extremeHz.ToString("0.000", PtBr)} Hz",
            Participants     = ev.Participants,
        };
    }
}
