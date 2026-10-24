using Lucia.Companion.Connection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lucia.Companion.App.Persistence;

public static class ConnectedProfileScope
{
    /// <summary>Stable across equal-access re-pairing, isolated by host/home/person and Home grant. No secret or raw label in the filename.</summary>
    public static string FileName(ConnectionSession connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(connection.Session);
        var endpoint = CompanionEndpoint.Validate(connection.Endpoint);
        var descriptor = JsonSerializer.SerializeToUtf8Bytes(new
        {
            origin = endpoint.AbsoluteUri, connection.Session.HomeId, connection.Session.MemberId,
            places = connection.Session.Places.OrderBy(place => place.PlaceId, StringComparer.Ordinal)
                .Select(place => new { place.PlaceId, fields = place.Fields.Select(field => field.ToString()).Order(StringComparer.Ordinal).ToArray() }).ToArray()
        });
        return "connected-home-" + Convert.ToHexStringLower(SHA256.HashData(descriptor)) + ".json";
    }
}
