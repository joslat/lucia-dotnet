namespace Lucia.Companion.Connection;

public sealed class SessionStorageException() : Exception("The protected connection could not be read or saved safely. Repair storage or explicitly forget this connection. No plaintext fallback was used.");
