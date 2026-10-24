using System.Globalization;

namespace lucia.Agents.Companion;

public static class CompanionDeviceName
{
    public static string Validate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        if (name.Length > 80 || name.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format))
        {
            throw new ArgumentException("Use a device name with 1 to 80 readable characters and no hidden formatting.", nameof(name));
        }
        return name;
    }
}
