namespace lucia.Tests.TestDoubles;

/// <summary>History body without Content-Length, including a controllable slow recorder response.</summary>
internal sealed class UnknownLengthHistoryContent(Func<Stream, CancellationToken, Task> write) : HttpContent
{
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => write(stream, CancellationToken.None);
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => write(stream, cancellationToken);
}
