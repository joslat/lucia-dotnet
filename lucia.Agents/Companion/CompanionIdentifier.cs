namespace lucia.Agents.Companion;

public static class CompanionIdentifier
{
    public static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 128 || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.' and not ':'))
        {
            throw new ArgumentException("A companion identifier must contain 1 to 128 ASCII letters, digits, dots, colons, underscores or hyphens.", nameof(value));
        }
        return value;
    }
}
