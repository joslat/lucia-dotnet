namespace Lucia.Companion.Connection;

/// <summary>Activity-only denial after the protected session was verified. Not a whole-connection revocation.</summary>
public sealed class ActivityAccessException() : Exception("Activity is not permitted for this device. Its verified Home access has not been removed.");
