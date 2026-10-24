namespace lucia.Agents.Companion;

/// <summary>Exact canonical registry IDs, never friendly names or free-text search terms.</summary>
public static class CompanionEntityIdentifier
{
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 256) { return false; }
        var parts = value.Split('.');
        return parts.Length == 2 && parts.All(part => part.Length > 0
            && part.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_'));
    }
}
