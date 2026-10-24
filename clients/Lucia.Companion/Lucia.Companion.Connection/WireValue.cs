using System.Globalization;

namespace Lucia.Companion.Connection;

internal static class WireValue
{
    public static string Secret(string value, string prefix)
    {
        if (value is null || value.Length != 47 || !value.StartsWith(prefix, StringComparison.Ordinal)
            || value.AsSpan(4).ContainsAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"))
        { throw new ArgumentException("The device credential format is invalid."); }
        return value;
    }

    public static string Identifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128 || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not ':' and not '_' and not '-'))
        { throw new ArgumentException("The identifier format is invalid."); }
        return value;
    }

    public static string DeviceName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        value = value.Trim();
        if (value.Length > 80 || value.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format))
        { throw new ArgumentException("Use a readable device name of at most 80 characters."); }
        return value;
    }

    public static string Label(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 200 || value.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format))
        { throw new ArgumentException("The source label is invalid."); }
        return value;
    }

    public static string SourceIdentifier(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 256 || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format))
        { throw new ArgumentException("The source identifier is invalid."); }
        return value;
    }
}
