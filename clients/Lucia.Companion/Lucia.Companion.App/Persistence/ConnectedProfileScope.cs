using Lucia.Companion.Connection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lucia.Companion.App.Persistence;

public static class ConnectedProfileScope
{
    /// <summary>Stable across re-pairing, distinct across host/home/person. No secret or raw label in the filename.</summary>
    public static string FileName(ConnectionSession connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(connection.Session);
        var endpoint = CompanionEndpoint.Validate(connection.Endpoint);
        var descriptor = JsonSerializer.SerializeToUtf8Bytes(new[] { endpoint.AbsoluteUri, connection.Session.HomeId, connection.Session.MemberId });
        return "connected-home-" + Convert.ToHexStringLower(SHA256.HashData(descriptor)) + ".json";
    }
}
