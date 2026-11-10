namespace Lucia.Companion.Core;

/// <summary>One numeric reading at a time, for a chart. Carries no quality: a series is drawn only where readings exist.</summary>
public sealed record ReadingPoint(DateTimeOffset At, double Value);
