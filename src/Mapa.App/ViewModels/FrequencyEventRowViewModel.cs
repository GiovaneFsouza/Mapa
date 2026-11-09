using Mapa.Core.Correlation;

namespace Mapa.App.ViewModels;

public sealed class FrequencyEventRowViewModel
{
    public DateTime StartTime    { get; init; }
    public string StartTimeLabel { get; init; } = "";
    public string DateLabel      { get; init; } = "";
    public string TimeLabel      { get; init; } = "";
    public string TypeLabel      { get; init; } = "";
    public string SubHzLabel       { get; init; } = "—";
    public string SobreHzLabel     { get; init; } = "—";
    public string DurationLabel    { get; init; } = "";
    public double DurationSeconds  { get; init; }
    public int    ParticipantCount { get; init; }

    /// <summary>Max |Hz − 60| across sub/sobre medians — used for severity ranking.</summary>
    public double MaxDeviationHz   { get; init; }
    /// <summary>Signed deviation of the most extreme side (e.g. "−1,150 Hz" or "+1,100 Hz").</summary>
    public string DeviationLabel   { get; init; } = "";
    /// <summary>Hz value of the most extreme side — shown in the "Mais Severos" ranking.</summary>
    public string ExtremeHzLabel   { get; init; } = "";

    public IReadOnlyList<FrequencyParticipant> Participants { get; init; } = [];
}
