namespace Mapa.Core.Settings;

public sealed class AppSettings
{
    public string? BdedFolderPath { get; set; }
    public string? ConfigJsonPath { get; set; }

    // Quantity / Undefined: single pair (no directional split)
    public double QuantityVoltageL1   { get; set; } = 10;
    public double QuantityVoltageL2   { get; set; } = 20;
    public double QuantityFreqL1      { get; set; } = 5;
    public double QuantityFreqL2      { get; set; } = 10;
    public double UndefinedVoltageL1  { get; set; } = 5;
    public double UndefinedVoltageL2  { get; set; } = 15;
    public double UndefinedFreqL1     { get; set; } = 3;
    public double UndefinedFreqL2     { get; set; } = 8;

    // Severity Sub: L1 < L2 (L1 = Inferior = lower boundary, L2 = Superior = upper boundary;
    //   ThresholdScale uses IsInverted=true so value < L1 → red, value ≥ L2 → green)
    public double SeverityVoltageSubL1   { get; set; } = 0.70;   // pu: Inferior (below → red)
    public double SeverityVoltageSubL2   { get; set; } = 0.90;   // pu: Superior (above → green)
    public double SeverityFreqSubL1      { get; set; } = 58.0;   // Hz: Inferior
    public double SeverityFreqSubL2      { get; set; } = 59.5;   // Hz: Superior
    // Severity Sobre: L1 < L2 (normal scale — higher value = worse)
    public double SeverityVoltageSobreL1 { get; set; } = 1.10;   // pu
    public double SeverityVoltageSobreL2 { get; set; } = 1.30;   // pu
    public double SeverityFreqSobreL1    { get; set; } = 60.5;   // Hz
    public double SeverityFreqSobreL2    { get; set; } = 62.0;   // Hz

    // Duration: separate Sub / Sobre (ms for voltage, s for frequency)
    public double DurationVoltageSubL1   { get; set; } = 100;
    public double DurationVoltageSubL2   { get; set; } = 500;
    public double DurationVoltageSobreL1 { get; set; } = 100;
    public double DurationVoltageSobreL2 { get; set; } = 500;
    public double DurationFreqSubL1      { get; set; } = 10;
    public double DurationFreqSubL2      { get; set; } = 60;
    public double DurationFreqSobreL1    { get; set; } = 10;
    public double DurationFreqSobreL2    { get; set; } = 60;

    // VTCD occurrence correlation — dual-constraint
    public double VtcdGapMaxMs   { get; set; } = 500.0;
    public double VtcdRangeMaxMs { get; set; } = 1000.0;

    // Frequency event correlation (Análise tab)
    public double FrequencyCorrelationWindowSeconds { get; set; } = 2.0;
    public int    FrequencyMinimumPmus              { get; set; } = 3;

    // Frequency occurrence grouping (Eventos Detectados tab — independent from Análise)
    public double OccurrencesFreqWindowMs  { get; set; } = 2000.0;
    public int    OccurrencesFreqMinPmus   { get; set; } = 3;
}
