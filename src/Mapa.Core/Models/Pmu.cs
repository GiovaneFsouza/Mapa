namespace Mapa.Core.Models;

/// <summary>PMU metadata read from a smsf2 XML file (idName + geolocation).</summary>
public sealed record Pmu(
    string IdName,
    string FullName,
    int VoltLevel,
    string Area,
    string State,
    string Station,
    double Lat,
    double Lon);
