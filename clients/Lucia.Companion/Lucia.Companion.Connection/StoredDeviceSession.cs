namespace Lucia.Companion.Connection;

/// <summary>Only for the protected vault, never the local home profile. Contains no observations or task history.</summary>
public sealed class StoredDeviceSession
{
    public StoredDeviceSession(int schemaVersion, Uri endpoint, DeviceCredentials credentials, SessionPhase phase, string? homeId, string? memberId)
    {
        if (schemaVersion != 1 || !Enum.IsDefined(phase)) { throw new ArgumentException("Unsupported stored connection."); }
        SchemaVersion = schemaVersion;
        Endpoint = CompanionEndpoint.Validate(endpoint);
        ArgumentNullException.ThrowIfNull(credentials);
        Credentials = credentials;
        Phase = phase;
        if (phase == SessionPhase.Verifying)
        {
            if (homeId is not null || memberId is not null) { throw new ArgumentException("An unverified connection cannot contain trusted member identifiers."); }
        }
        else if (phase == SessionPhase.Active || homeId is not null || memberId is not null)
        { HomeId = WireValue.Identifier(homeId!); MemberId = WireValue.Identifier(memberId!); }
    }
    public int SchemaVersion { get; }
    public Uri Endpoint { get; }
    public DeviceCredentials Credentials { get; }
    public SessionPhase Phase { get; }
    public string? HomeId { get; }
    public string? MemberId { get; }
    public override string ToString() => "Protected device session redacted";
}
