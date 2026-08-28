namespace Stalksville.Application.Advanced;

/// <summary>Alert rule tuning (bound from the "Alerts" configuration section).</summary>
public sealed class AlertsOptions
{
    /// <summary>|Δlevel| at or above this raises a LevelJump alert.</summary>
    public int LevelJumpThreshold { get; set; } = 10;

    /// <summary>Rank movement (either direction) at or above this raises a RankShift alert.</summary>
    public int RankShiftThreshold { get; set; } = 10;
}
