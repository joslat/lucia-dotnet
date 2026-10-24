namespace Lucia.Companion.Connection;

public static class CompanionEndpoint
{
    public static Uri Validate(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(endpoint.Host)
            || endpoint.UserInfo.Length > 0 || endpoint.AbsolutePath != "/" || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0)
        { throw new ArgumentException("Use an HTTPS host address without a path, query, fragment or credentials.", nameof(endpoint)); }
        return endpoint;
    }
}
