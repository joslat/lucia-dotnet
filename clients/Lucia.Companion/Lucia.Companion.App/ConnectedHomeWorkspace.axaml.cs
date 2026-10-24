using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;

namespace Lucia.Companion.App;

/// <summary>Composes the shared native shell with verified-data and lifecycle states.</summary>
public sealed partial class ConnectedHomeWorkspace : UserControl, IDisposable
{
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(5) };
    private TopLevel? topLevel;
    private bool attached;
    private bool reportedAuthorityLoss;
    private bool focusedReadState;
    public ConnectionSession Identity { get; }
    public HomeViewModel State { get; }
    public CompanionShell Shell { get; }
    public ConnectedHomeController Controller { get; }
    public event Action? ConnectionRequested;

    public ConnectedHomeWorkspace() => throw new InvalidOperationException("A verified connection and protected data service are required.");

    public ConnectedHomeWorkspace(ICompanionConnection service, ConnectionSession identity, LocalHomeStore? store, TimeProvider? clock = null)
    {
        Identity = identity;
        State = new(new(identity.Session.HomeId, "Your home", [], []), isDemo: false);
        Shell = new(State, store);
        Controller = new(service, identity, State, Shell.ProfileSession, clock);
        AvaloniaXamlLoader.Load(this);
        this.FindControl<ContentControl>("ConnectedShellHost")!.Content = Shell;
        Shell.ConnectionRequested += () => ConnectionRequested?.Invoke();
        Shell.RefreshRequested += async () => await RefreshAsync();
        this.FindControl<Button>("RetryHomeReports")!.Click += async (_, _) => await RefreshAsync();
        this.FindControl<Button>("ManageHomeConnection")!.Click += (_, _) => ConnectionRequested?.Invoke();
        Controller.Changed += Update;
        refresh.Tick += async (_, _) => await Controller.TickAsync();
        AttachedToVisualTree += (_, _) =>
        {
            attached = true; topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not null) { topLevel.BackRequested += OnBack; }
            refresh.Start(); Update();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            attached = false; refresh.Stop(); Controller.Suspend();
            if (topLevel is not null) { topLevel.BackRequested -= OnBack; }
            topLevel = null;
        };
        Update();
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Controller.RefreshAsync(cancellationToken: cancellationToken);
    public async Task ResumeAsync()
    {
        if (attached) { refresh.Start(); }
        await Controller.ResumeAsync();
    }
    public void Suspend() { refresh.Stop(); Controller.Suspend(); }
    private void OnBack(object? sender, RoutedEventArgs args)
    {
        if (!args.Handled && !Controller.HasData) { args.Handled = true; ConnectionRequested?.Invoke(); }
    }
    private void Update()
    {
        this.FindControl<ContentControl>("ConnectedShellHost")!.IsVisible = Controller.HasData;
        this.FindControl<ScrollViewer>("HomeReadState")!.IsVisible = !Controller.HasData;
        this.FindControl<TextBlock>("HomeReadStatus")!.Text = Controller.StatusText;
        this.FindControl<Button>("RetryHomeReports")!.IsEnabled = Controller.CanRefresh;
        Shell.SetImportCatalog(Controller.Catalog); Shell.SetRefreshing(Controller.IsBusy);
        if (Controller.HasData) { focusedReadState = false; }
        else if (attached && !focusedReadState)
        { focusedReadState = true; this.FindControl<TextBlock>("HomeReadStatus")!.Focus(); }
        if (Controller.AuthorityLost && !Controller.IsBusy && !reportedAuthorityLoss)
        { reportedAuthorityLoss = true; refresh.Stop(); ConnectionRequested?.Invoke(); }
    }
    public void Dispose() { refresh.Stop(); Controller.Changed -= Update; Controller.Dispose(); }
}
