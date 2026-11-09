using Mapa.Core.Models;

namespace Mapa.Core.Filtering;

/// <summary>Date/time range + optional category restriction. CategoryFilter=null means "Ambos".</summary>
public sealed record FilterCriteria(DateTime Start, DateTime End, EventCategory? CategoryFilter);
