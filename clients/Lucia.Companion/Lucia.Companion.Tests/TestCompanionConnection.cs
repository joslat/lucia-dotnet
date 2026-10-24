using Lucia.Companion.Connection;

namespace Lucia.Companion.Tests;

internal sealed class TestCompanionConnection : ICompanionConnection
{
    public Uri? Endpoint { get; set; }
    public bool HasSavedConnection { get; set; }
    public int BeginCalls { get; private set; }
    public int PollCalls { get; private set; }
    public Func<Task<ConnectionSession?>> Restore { get; set; } = () => Task.FromResult<ConnectionSession?>(null);
    public Func<Task<PairingUpdate>> Poll { get; set; } = () => Task.FromResult(new PairingUpdate(PairingPollStatus.Pending, TimeSpan.FromSeconds(5)));
    public Func<Task<bool>> Disconnect { get; set; } = () => Task.FromResult(true);
    public Func<Task<HomeSnapshot>> Home { get; set; } = () => throw new SessionStateException(SessionStateFailure.ConnectionRequired);
    public Func<string?, Task<HomeUpdate>>? HomeUpdate { get; set; }
    public Func<Task<ActivitySnapshot>> Activity { get; set; } = () =>
    { var now = DateTimeOffset.UtcNow; return Task.FromResult(new ActivitySnapshot("home-a", "member-a", now, now, false, false, false, [])); };
    public Task<ConnectionSession?> RestoreAsync(CancellationToken cancellationToken = default) => Restore();
    public Task<PairingPrompt> BeginAsync(Uri endpoint, string deviceName, CancellationToken cancellationToken = default)
    {
        BeginCalls++; Endpoint = endpoint;
        return Task.FromResult(new PairingPrompt("ABCDE-FGHJK", DateTimeOffset.UtcNow.AddMinutes(10), 5));
    }
    public Task<PairingUpdate> PollAsync(CancellationToken cancellationToken = default) { PollCalls++; return Poll(); }
    public async Task<bool> DisconnectAsync(CancellationToken cancellationToken = default) { var result = await Disconnect(); HasSavedConnection = false; return result; }
    public Task ForgetAsync(CancellationToken cancellationToken = default) { HasSavedConnection = false; return Task.CompletedTask; }
    public Task<HomeSnapshot> ReadHomeAsync(CancellationToken cancellationToken = default) => Home();
    public Task<HomeSnapshot> ReadHomeAsync(ConnectionSession expected, CancellationToken cancellationToken = default) => Home();
    public async Task<HomeUpdate> ReadHomeUpdateAsync(ConnectionSession expected, string? revision, CancellationToken cancellationToken = default) =>
        HomeUpdate is { } read ? await read(revision) : new(HomeUpdateClientTests.Revision, await Home());
    public Task<HomeCatalog> DiscoverAsync(CancellationToken cancellationToken = default) => throw new SessionStateException(SessionStateFailure.ConnectionRequired);
    public Task<ActivitySnapshot> ReadActivityAsync(ConnectionSession expected, CancellationToken cancellationToken = default) => Activity();
    public Task<SharedHomeLayout?> ReadSharedLayoutAsync(ConnectionSession expected, CancellationToken cancellationToken = default) => Task.FromResult<SharedHomeLayout?>(null);
    internal static ConnectionSession Session() => new(new Uri("https://synthetic.invalid"), new DeviceSession(new string('a', 32), "Phone",
        DateTimeOffset.UtcNow.AddDays(30), "home-a", "member-a", [new("porch", [Core.ObservationRole.Contact])], canReadHistory: true));
}
