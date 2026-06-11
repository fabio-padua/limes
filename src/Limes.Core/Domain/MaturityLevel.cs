namespace Limes.Core.Domain;

/// <summary>
/// The 1-5 maturity scale applied to each pillar and to the overall Readiness Index.
/// </summary>
public enum MaturityLevel
{
    Initial = 1,
    Developing = 2,
    Defined = 3,
    Managed = 4,
    Optimized = 5,
}

public static class MaturityLevelInfo
{
    public static string DisplayName(this MaturityLevel level) => level switch
    {
        MaturityLevel.Initial => "Initial",
        MaturityLevel.Developing => "Developing",
        MaturityLevel.Defined => "Defined",
        MaturityLevel.Managed => "Managed",
        MaturityLevel.Optimized => "Optimized",
        _ => level.ToString(),
    };

    /// <summary>Maps a continuous 1.0-5.0 score to the nearest discrete maturity level.</summary>
    public static MaturityLevel FromScore(double score)
    {
        var rounded = (int)Math.Round(Math.Clamp(score, 1.0, 5.0), MidpointRounding.AwayFromZero);
        return (MaturityLevel)rounded;
    }
}
