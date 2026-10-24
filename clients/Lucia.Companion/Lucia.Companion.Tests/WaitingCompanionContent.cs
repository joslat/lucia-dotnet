using System.Net;

namespace Lucia.Companion.Tests;

internal sealed class WaitingCompanionContent : HttpContent
{
    public WaitingCompanionContent() => Headers.ContentType = new("application/json");
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new InvalidOperationException("Use the cancellation-aware serialization overload.");
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
}
