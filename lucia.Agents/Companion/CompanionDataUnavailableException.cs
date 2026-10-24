namespace lucia.Agents.Companion;

/// <summary>Safe boundary failure. Never retains an upstream response or diagnostic as its message/inner exception.</summary>
public sealed class CompanionDataUnavailableException(bool tooLarge = false) : Exception("Qualified home data is unavailable.")
{
    public bool TooLarge { get; } = tooLarge;
}
