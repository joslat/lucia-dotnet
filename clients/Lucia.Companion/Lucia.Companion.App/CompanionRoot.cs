using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;
using Lucia.Companion.App.Persistence;

namespace Lucia.Companion.App;

/// <summary>Explicit application modes. A failed connection never falls through to the synthetic Home shell.</summary>
public sealed class CompanionRoot : UserControl
{
    private readonly ContentControl host = new() { HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch };
    private readonly Func<CompanionShell> demoFactory;
    private readonly ConnectionScreen connectionScreen;
    private CompanionShell? demo;
    private TopLevel? topLevel;
    private bool initialized;
    private bool cameFromDemo;
    private readonly ICompanionConnection? connection;
    private readonly Func<ConnectionSession, LocalHomeStore?> connectedStoreFactory;
    private ConnectedHomeWorkspace? connected;
    public ConnectionViewModel State { get; }
    public Task PendingOperation { get; private set; } = Task.CompletedTask;

    public CompanionRoot(ICompanionConnection? connection, Func<CompanionShell>? demoFactory = null, Func<ConnectionSession, LocalHomeStore?>? connectedStoreFactory = null)
    {
        this.connection = connection;
        this.connectedStoreFactory = connectedStoreFactory ?? (identity => new LocalHomeStore(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(LocalHomeStore.DefaultPath)!, ConnectedProfileScope.FileName(identity))));
        this.demoFactory = demoFactory ?? (() => new CompanionShell());
        State = new(connection);
        connectionScreen = new(State);
        connectionScreen.DemoRequested += ShowDemo;
        connectionScreen.HomeRequested += async () => { PendingOperation = OpenConnectedHomeAsync(); await PendingOperation; };
        host.Content = connectionScreen; Content = host;
        AttachedToVisualTree += async (_, _) =>
        {
            topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not null) { topLevel.BackRequested += OnBack; }
            if (!initialized) { initialized = true; PendingOperation = State.InitializeAsync(); await PendingOperation; }
        };
        DetachedFromVisualTree += (_, _) => { if (topLevel is not null) { topLevel.BackRequested -= OnBack; } topLevel = null; };
        KeyDown += (_, args) => { if (!args.Handled && args.Key == Key.Escape) { args.Handled = NavigateBack(); } };
    }
    public void SuspendConnectionPolling() { connectionScreen.SetPollingEnabled(false); connected?.Suspend(); }
    public async Task VerifyForegroundAsync()
    {
        connectionScreen.SetPollingEnabled(true);
        if (connected is not null && ReferenceEquals(host.Content, connected))
        { PendingOperation = connected.ResumeAsync(); await PendingOperation; return; }
        if (ReferenceEquals(host.Content, connectionScreen) && State.IsConnected)
        { PendingOperation = State.InitializeAsync(); await PendingOperation; }
    }
    private void OnBack(object? sender, RoutedEventArgs args) { if (!args.Handled) { args.Handled = NavigateBack(); } }
    private bool NavigateBack()
    {
        if (!ReferenceEquals(host.Content, connectionScreen)) { return false; }
        if (connectionScreen.TryNavigateBack()) { return true; }
        if (!cameFromDemo || State.IsConnected) { return false; }
        State.StopWaiting(); ShowDemo(); return true;
    }
    private void ShowDemo()
    {
        connected?.Suspend();
        if (demo is null) { demo = demoFactory(); demo.ConnectionRequested += ShowConnection; }
        host.Content = demo;
    }
    private async void ShowConnection()
    {
        cameFromDemo = true; host.Content = connectionScreen;
        if (!State.IsBusy && State.Prompt is null) { PendingOperation = State.InitializeAsync(); await PendingOperation; }
    }

    private async Task OpenConnectedHomeAsync()
    {
        if (!State.IsConnected || State.Connection is not { } identity || connection is null) { return; }
        if (connected is null || connected.Identity.Endpoint != identity.Endpoint || connected.Identity.Session.SessionId != identity.Session.SessionId
            || ConnectedProfileScope.FileName(connected.Identity) != ConnectedProfileScope.FileName(identity))
        {
            connected?.Dispose();
            connected = new(connection, identity, connectedStoreFactory(identity));
            connected.ConnectionRequested += async () => { PendingOperation = ReturnFromHomeAsync(); await PendingOperation; };
        }
        cameFromDemo = false; host.Content = connected;
        await connected.ResumeAsync();
    }

    private async Task ReturnFromHomeAsync()
    {
        connected?.Suspend(); cameFromDemo = false;
        host.Content = connectionScreen; connectionScreen.SetPollingEnabled(true);
        if (!State.IsBusy) { await State.InitializeAsync(); }
    }
}
