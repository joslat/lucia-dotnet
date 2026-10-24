using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.ViewModels;

namespace Lucia.Companion.App.Screens;

public sealed partial class ConnectionScreen : UserControl
{
    private readonly DispatcherTimer polling = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool confirmForget;
    private bool pollingEnabled = true;
    private bool attached;
    private Button? removalInvoker;
    private ConnectionViewStage displayedStage = ConnectionViewStage.Checking;
    private bool displayedBusy;
    public ConnectionViewModel State { get; }
    public Task PendingOperation { get; private set; } = Task.CompletedTask;
    public event Action? DemoRequested;
    public event Action? HomeRequested;
    public ConnectionScreen() : this(new ConnectionViewModel(null)) { }
    public ConnectionScreen(ConnectionViewModel state)
    {
        State = state;
        AvaloniaXamlLoader.Load(this);
        this.FindControl<TextBox>("HostAddress")!.TextChanged += (_, _) => State.HostAddress = this.FindControl<TextBox>("HostAddress")!.Text ?? string.Empty;
        this.FindControl<TextBox>("DeviceName")!.TextChanged += (_, _) => State.DeviceName = this.FindControl<TextBox>("DeviceName")!.Text ?? string.Empty;
        this.FindControl<Button>("StartConnection")!.Click += async (_, _) => await RunAsync(State.StartAsync());
        this.FindControl<Button>("ExploreDemo")!.Click += (_, _) => { State.StopWaiting(); DemoRequested?.Invoke(); };
        this.FindControl<Button>("OpenConnectedHome")!.Click += (_, _) => { if (State.IsConnected && !State.IsBusy) { HomeRequested?.Invoke(); } };
        this.FindControl<Button>("StopWaiting")!.Click += (_, _) => State.StopWaiting();
        this.FindControl<Button>("ResumeWaiting")!.Click += (_, _) => State.ResumeWaiting();
        this.FindControl<Button>("ChangeHost")!.Click += (_, _) => State.ChangeHost();
        this.FindControl<Button>("RetryConnection")!.Click += async (_, _) => await RunAsync(State.InitializeAsync());
        this.FindControl<Button>("AskDisconnect")!.Click += (_, _) => AskRemoval(forget: false);
        this.FindControl<Button>("AskForget")!.Click += (_, _) => AskRemoval(forget: true);
        this.FindControl<Button>("KeepConnection")!.Click += (_, _) => CloseConfirmation();
        this.FindControl<Button>("ConfirmRemoval")!.Click += async (_, _) =>
        {
            await RunAsync(confirmForget ? State.ForgetAsync() : State.DisconnectAsync());
            CloseConfirmation();
        };
        polling.Tick += async (_, _) => await RunAsync(State.TickAsync());
        AttachedToVisualTree += (_, _) => { attached = true; State.Changed += Refresh; if (pollingEnabled) { polling.Start(); } Refresh(); };
        DetachedFromVisualTree += (_, _) => { attached = false; State.Changed -= Refresh; polling.Stop(); };
        PropertyChanged += (_, args) => { if (args.Property == BoundsProperty) { ArrangeFlow(); } };
        this.FindControl<TextBlock>("StatusNotice")!.PropertyChanged += (_, args) => { if (args.Property == TextBlock.FontSizeProperty) { ArrangeFlow(); } };
        Refresh();
    }

    public bool TryNavigateBack()
    {
        if (!this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible) { return false; }
        CloseConfirmation(); return true;
    }
    public void SetPollingEnabled(bool enabled)
    {
        pollingEnabled = enabled;
        if (enabled && attached) { polling.Start(); } else { polling.Stop(); }
    }
    private async Task RunAsync(Task operation) { PendingOperation = operation; await operation; }
    private void AskRemoval(bool forget)
    {
        confirmForget = forget;
        removalInvoker = this.FindControl<Button>(forget ? "AskForget" : "AskDisconnect");
        this.FindControl<TextBlock>("RemovalQuestion")!.Text = forget
            ? "Forget the protected connection on this device only? This does not revoke host access. The owner can revoke it from connected devices."
            : "Disconnect this device? Its local credentials will be removed and Lucia will be asked to revoke the session. If offline, only local removal can be confirmed.";
        this.FindControl<TextBlock>("RemovalAction")!.Text = forget ? "Forget locally" : "Disconnect";
        this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible = true;
        this.FindControl<Button>("KeepConnection")!.Focus();
    }
    private void CloseConfirmation()
    {
        this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible = false;
        if (removalInvoker is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true }) { removalInvoker.Focus(); }
        else { FocusState(); }
        removalInvoker = null;
    }

    private void Refresh()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var stageChanged = displayedStage != State.Stage;
        var finishedBusy = displayedBusy && !State.IsBusy;
        displayedStage = State.Stage;
        displayedBusy = State.IsBusy;
        this.FindControl<TextBlock>("StatusNotice")!.Text = State.StatusText;
        var status = this.FindControl<TextBlock>("StatusNotice")!;
        status.Bind(TextBlock.ForegroundProperty, status.GetResourceObservable(State.IsProblem ? "ProblemBrush" : "MutedBrush"));
        this.FindControl<TextBlock>("ConnectionTitle")!.Text = State.IsConnected ? "Your Lucia connection" : "Connect to Lucia";
        var showForm = State.Stage == ConnectionViewStage.Form;
        this.FindControl<StackPanel>("HostForm")!.IsVisible = showForm;
        this.FindControl<TextBox>("HostAddress")!.IsEnabled = !State.IsBusy;
        this.FindControl<TextBox>("DeviceName")!.IsEnabled = !State.IsBusy;
        if (this.FindControl<TextBox>("HostAddress")!.Text != State.HostAddress) { this.FindControl<TextBox>("HostAddress")!.Text = State.HostAddress; }
        if (this.FindControl<TextBox>("DeviceName")!.Text != State.DeviceName) { this.FindControl<TextBox>("DeviceName")!.Text = State.DeviceName; }
        this.FindControl<Button>("StartConnection")!.IsEnabled = State.CanStart;
        this.FindControl<StackPanel>("ApprovalPrompt")!.IsVisible = State.Prompt is not null;
        this.FindControl<TextBlock>("ApprovalCode")!.Text = State.Prompt?.UserCode ?? string.Empty;
        this.FindControl<TextBlock>("ApprovalHost")!.Text = State.ApprovalHost;
        this.FindControl<TextBlock>("ApprovalDevice")!.Text = State.ApprovalDeviceName;
        this.FindControl<TextBlock>("CodeExpiry")!.Text = State.ExpiryText;
        this.FindControl<Button>("StopWaiting")!.IsVisible = State.IsWaiting;
        this.FindControl<Button>("ResumeWaiting")!.IsVisible = !State.IsWaiting;
        this.FindControl<Button>("ResumeWaiting")!.IsEnabled = !State.IsBusy;
        this.FindControl<Button>("ChangeHost")!.IsEnabled = !State.IsBusy;
        this.FindControl<StackPanel>("VerifiedConnection")!.IsVisible = State.IsConnected;
        this.FindControl<Button>("OpenConnectedHome")!.IsEnabled = State.IsConnected && !State.IsBusy;
        var verified = State.Connection;
        this.FindControl<TextBlock>("SessionSummary")!.Text = verified is null ? string.Empty
            : $"{verified.Session.DeviceName}\n{verified.Endpoint.Host}\n{verified.Session.Places.Count} {(verified.Session.Places.Count == 1 ? "place" : "places")} approved for read access";
        this.FindControl<TextBlock>("GrantDetails")!.Text = verified is null ? string.Empty
            : $"Home: {verified.Session.HomeId}\nMember: {verified.Session.MemberId}\n"
                + string.Join("\n", verified.Session.Places.Select(place => $"{place.PlaceId}: {string.Join(", ", place.Fields)}"))
                + $"\nActivity: {Permission(verified.Session.CanReadActivity)}\nHistory: {Permission(verified.Session.CanReadHistory)}\nEngine: {Permission(verified.Session.CanReadEngine)}";
        this.FindControl<WrapPanel>("RecoveryActions")!.IsVisible = State.Stage == ConnectionViewStage.Recovery;
        this.FindControl<Button>("RetryConnection")!.IsEnabled = !State.IsBusy;
        this.FindControl<Button>("AskForget")!.IsEnabled = State.CanForget;
        this.FindControl<Button>("AskDisconnect")!.IsVisible = State.IsConnected || State.HasSavedConnection;
        this.FindControl<Button>("AskDisconnect")!.IsEnabled = !State.IsBusy;
        this.FindControl<Button>("KeepConnection")!.IsEnabled = !State.IsBusy;
        this.FindControl<Button>("ConfirmRemoval")!.IsEnabled = !State.IsBusy;
        ArrangeFlow();
        if (attached && (focused is null || focused.GetVisualAncestors().Contains(this))
            && (stageChanged || finishedBusy && focused == status || focused is { IsEffectivelyVisible: false } or { IsEffectivelyEnabled: false }))
        {
            FocusState();
        }
    }
    private void FocusState()
    {
        var destination = State.IsBusy ? "StatusNotice" : State.Stage switch
        {
            ConnectionViewStage.Form => "HostAddress",
            ConnectionViewStage.AwaitingApproval => "ApprovalCode",
            ConnectionViewStage.Paused => "ResumeWaiting",
            ConnectionViewStage.Connected => "VerifiedTitle",
            ConnectionViewStage.Recovery => "RetryConnection",
            _ => "StatusNotice"
        };
        this.FindControl<Control>(destination)?.Focus();
    }
    private static string Permission(bool allowed) => allowed ? "Allowed" : "Not allowed";
    private void ArrangeFlow()
    {
        if (this.FindControl<Grid>("ConnectionLayout") is not { } layout) { return; }
        var expanded = Bounds.Width >= 960 && this.FindControl<TextBlock>("StatusNotice")!.FontSize <= 20;
        if (layout.ColumnDefinitions.Count != (expanded ? 2 : 1))
        {
            layout.ColumnDefinitions = ColumnDefinitions.Parse(expanded ? "0.8*,1.2*" : "*");
            Grid.SetColumn(this.FindControl<Border>("ConnectionPanel")!, expanded ? 1 : 0);
            Grid.SetRow(this.FindControl<Border>("ConnectionPanel")!, expanded ? 0 : 1);
            Grid.SetRow(this.FindControl<StackPanel>("DemoChoice")!, expanded ? 1 : 2);
        }
        foreach (var button in this.GetVisualDescendants().OfType<Button>()) { button.MaxWidth = Math.Max(120, Math.Min(550, Bounds.Width - 88)); }
    }
}
