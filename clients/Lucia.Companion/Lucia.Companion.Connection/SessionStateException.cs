namespace Lucia.Companion.Connection;

public sealed class SessionStateException(SessionStateFailure failure) : Exception(failure switch
{
    SessionStateFailure.WrongEndpoint => "The protected connection belongs to another host. Disconnect or explicitly forget it before switching hosts.",
    SessionStateFailure.AlreadyConnected => "A protected connection already exists. Restore it or disconnect before pairing again.",
    SessionStateFailure.IdentityChanged => "The host returned a different device or home identity. The local connection was refused; connect again.",
    _ => "This connection cannot be recovered safely. Pair this device again."
})
{
    public SessionStateFailure Failure { get; } = failure;
}
