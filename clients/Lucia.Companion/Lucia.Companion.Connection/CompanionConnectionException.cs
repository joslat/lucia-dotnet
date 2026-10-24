using System.Net;

namespace Lucia.Companion.Connection;

/// <summary>Safe to show/log. Never includes response bodies, credentials or raw transport diagnostics.</summary>
public sealed class CompanionConnectionException(ConnectionFailure failure, HttpStatusCode? statusCode = null)
    : Exception(failure switch
    {
        ConnectionFailure.Transport => "Lucia could not be reached securely. A credential-changing request may have completed; do not replay it.",
        ConnectionFailure.Unauthorized => "This device session is no longer authorized. Connect again.",
        ConnectionFailure.Rejected => "Lucia refused the request.",
        _ => "Lucia returned an unsupported response."
    })
{
    public ConnectionFailure Failure { get; } = failure;
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
