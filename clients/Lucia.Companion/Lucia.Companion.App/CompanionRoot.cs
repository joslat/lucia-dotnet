using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;

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
    public ConnectionViewModel State { get; }
    public Task PendingOperation { get; private set; } = Task.CompletedTask;

    public CompanionRoot(ICompanionConnection? connection, Func<CompanionShell>? demoFactory = null)
    {
        this.demoFactory = demoFactory ?? (() => new CompanionShell());
        State = new(connection);
        connectionScreen = new(State);
        connectionScreen.DemoRequested += ShowDemo;
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
    public void SuspendConnectionPolling() => connectionScreen.SetPollingEnabled(false);
    public async Task VerifyForegroundAsync()
    {
        connectionScreen.SetPollingEnabled(true);
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
        if (demo is null) { demo = demoFactory(); demo.ConnectionRequested += ShowConnection; }
        host.Content = demo;
    }
    private async void ShowConnection()
    {
        cameFromDemo = true; host.Content = connectionScreen;
        if (!State.IsBusy && State.Prompt is null) { PendingOperation = State.InitializeAsync(); await PendingOperation; }
    }
}
