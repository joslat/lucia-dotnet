using System.Security.Cryptography;
using System.Text;

namespace lucia.Agents.Companion;

public static class CompanionTokens
{
    public const string AccessPrefix = "lca_";
    public const string RefreshPrefix = "lcr_";
    public static string Create(string prefix) => prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool HasFormat(string? value, string prefix) => value is { Length: 47 } && value.StartsWith(prefix, StringComparison.Ordinal)
        && value.AsSpan(prefix.Length).IndexOfAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".AsSpan()) < 0;
}
