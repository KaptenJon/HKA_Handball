namespace HKA_Handball;

/// <summary>
/// AI difficulty levels that adjust opponent behavior and goalkeeper performance.
/// </summary>
public enum Difficulty
{
    /// <summary>Slower AI, weaker opponent goalkeeper, stronger home goalkeeper.</summary>
    Easy,

    /// <summary>Balanced gameplay (default).</summary>
    Medium,

    /// <summary>Faster AI, stronger opponent goalkeeper, higher interception rates.</summary>
    Hard
}
