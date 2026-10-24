using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectionScreenTests
{
    [AvaloniaFact]
    public async Task FirstLaunchAndRecoveryNeverDisplayDemoWithoutAnExplicitChoice()
    {
        var service = new TestCompanionConnection();
        var root = new CompanionRoot(service, demoFactory: () => new CompanionShell(new(SampleHome.Create()), null));
        var window = new Window { Width = 390, Height = 844, Content = root };
        window.Show();
        try
        {
            await root.PendingOperation;
            Assert.Empty(root.GetVisualDescendants().OfType<PlaceOrb>());
            Assert.NotNull(root.GetVisualDescendants().OfType<ConnectionScreen>().SingleOrDefault());
            root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ExploreDemo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(root.GetVisualDescendants().OfType<PlaceOrb>());
            root.GetVisualDescendants().OfType<CompanionShell>().Single().RequestConnection();
            await root.PendingOperation;
            Assert.Empty(root.GetVisualDescendants().OfType<PlaceOrb>());
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectedBackNeverFallsThroughToDemoAndForegroundRevocationClearsSummary(bool fromDemo)
    {
        var service = new TestCompanionConnection();
        var root = new CompanionRoot(service, () => new CompanionShell(new(SampleHome.Create()), null));
        var window = new Window { Width = 390, Height = 844, Content = root };
        window.Show();
        try
        {
            await root.PendingOperation;
            if (fromDemo)
            {
                root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ExploreDemo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                root.GetVisualDescendants().OfType<CompanionShell>().Single().RequestConnection();
                await root.PendingOperation;
            }
            service.HasSavedConnection = true;
            service.Restore = () => Task.FromResult<ConnectionSession?>(TestCompanionConnection.Session());
            await root.State.InitializeAsync(TestContext.Current.CancellationToken);
            var back = new RoutedEventArgs(TopLevel.BackRequestedEvent);
            window.RaiseEvent(back);
            Assert.False(back.Handled);
            Assert.Empty(root.GetVisualDescendants().OfType<PlaceOrb>());
            root.SuspendConnectionPolling();
            service.Restore = () => { service.HasSavedConnection = false; throw new CompanionConnectionException(ConnectionFailure.Unauthorized); };
            await root.VerifyForegroundAsync();
            Assert.False(root.State.IsConnected);
            Assert.Empty(root.GetVisualDescendants().OfType<PlaceOrb>());
            Assert.Equal(string.Empty, root.GetVisualDescendants().OfType<ConnectionScreen>().Single().FindControl<TextBlock>("SessionSummary")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(390, false, 1.0)]
    [InlineData(390, true, 1.0)]
    [InlineData(1000, false, 1.0)]
    [InlineData(1000, true, 1.0)]
    [InlineData(390, false, 2.0)]
    [InlineData(390, true, 2.0)]
    [InlineData(1000, false, 2.0)]
    [InlineData(1000, true, 2.0)]
    public async Task ConnectionStatesRemainReadableAndConnectedHasNoSampleHome(int width, bool dark, double scale)
    {
        CompanionTypography.Apply(size => size * scale);
        var service = new TestCompanionConnection();
        var root = new CompanionRoot(service);
        var window = new Window { Width = width, Height = 900, Content = root, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await root.PendingOperation;
            window.UpdateLayout();
            var screen = root.GetVisualDescendants().OfType<ConnectionScreen>().Single();
            Assert.Equal(width >= 960 && scale == 1 ? 2 : 1, screen.FindControl<Grid>("ConnectionLayout")!.ColumnDefinitions.Count);
            Assert.True(screen.FindControl<TextBox>("HostAddress")!.IsVisible);
            Assert.True(screen.FindControl<Button>("StartConnection")!.MinHeight >= 48);
            NativeCapture.Save(window, $"connect-form-{width}-{dark}-{scale:0.0}.png");
            root.State.HostAddress = "https://synthetic.invalid";
            root.State.DeviceName = "  Porch tablet  ";
            await root.State.StartAsync(TestContext.Current.CancellationToken);
            window.UpdateLayout();
            Assert.Equal("ABCDE-FGHJK", screen.FindControl<TextBlock>("ApprovalCode")!.Text);
            Assert.Equal("https://synthetic.invalid/", screen.FindControl<TextBlock>("ApprovalHost")?.Text);
            Assert.Equal("Porch tablet", screen.FindControl<TextBlock>("ApprovalDevice")?.Text);
            NativeCapture.Save(window, $"connect-code-{width}-{dark}-{scale:0.0}.png");
            root.State.StopWaiting();
            Assert.True(screen.FindControl<TextBlock>("ApprovalHost")!.IsEffectivelyVisible);
            service.HasSavedConnection = true;
            service.Restore = () => Task.FromResult<ConnectionSession?>(TestCompanionConnection.Session());
            await root.State.InitializeAsync(TestContext.Current.CancellationToken);
            window.UpdateLayout();
            Assert.Empty(root.GetVisualDescendants().OfType<PlaceOrb>());
            Assert.True(screen.FindControl<StackPanel>("VerifiedConnection")!.IsVisible);
            NativeCapture.Save(window, $"connect-ready-{width}-{dark}-{scale:0.0}.png");
            screen.FindControl<Button>("AskDisconnect")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(screen.TryNavigateBack());
            Assert.False(screen.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible);
            service.Restore = () => throw new CompanionConnectionException(ConnectionFailure.Transport);
            await root.State.InitializeAsync(TestContext.Current.CancellationToken);
            window.UpdateLayout();
            Assert.Empty(root.GetVisualDescendants().OfType<PlaceOrb>());
            NativeCapture.Save(window, $"connect-recovery-{width}-{dark}-{scale:0.0}.png");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaFact]
    public async Task FocusContinuesThroughApprovalRecoveryAndRemovalWithoutLandingInHiddenContent()
    {
        var service = new TestCompanionConnection();
        var state = new App.ViewModels.ConnectionViewModel(service);
        var screen = new ConnectionScreen(state);
        var window = new Window { Width = 1000, Height = 900, Content = screen };
        window.Show();
        try
        {
            await state.InitializeAsync(TestContext.Current.CancellationToken);
            screen.FindControl<TextBox>("HostAddress")!.Text = "synthetic.invalid";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            screen.FindControl<Button>("StartConnection")!.Focus();
            await state.StartAsync(TestContext.Current.CancellationToken);
            Assert.Equal(screen.FindControl<TextBlock>("ApprovalCode"), window.FocusManager!.GetFocusedElement());
            screen.FindControl<Button>("StopWaiting")!.Focus();
            state.StopWaiting();
            Assert.Equal(screen.FindControl<Button>("ResumeWaiting"), window.FocusManager.GetFocusedElement());
            state.ChangeHost();
            Assert.Equal(screen.FindControl<TextBox>("HostAddress"), window.FocusManager.GetFocusedElement());
            service.HasSavedConnection = true;
            service.Restore = () => Task.FromResult<ConnectionSession?>(TestCompanionConnection.Session());
            await state.InitializeAsync(TestContext.Current.CancellationToken);
            Assert.Equal(screen.FindControl<TextBlock>("VerifiedTitle"), window.FocusManager.GetFocusedElement());
            var disclosure = screen.FindControl<Expander>("ApprovedAccess")!;
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Assert.Contains(window.FocusManager.GetFocusedElement(), disclosure.GetVisualDescendants().OfType<ToggleButton>());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.UpdateLayout();
            Assert.True(disclosure.IsExpanded);
            Assert.True(screen.FindControl<TextBlock>("GrantDetails")!.IsEffectivelyVisible);
            NativeCapture.Save(window, "connect-keyboard-expanded-grants.png");
            var disconnect = screen.FindControl<Button>("AskDisconnect")!;
            disconnect.Focus();
            disconnect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(screen.FindControl<Button>("KeepConnection"), window.FocusManager.GetFocusedElement());
            NativeCapture.Save(window, "connect-keyboard-removal-confirmation.png");
            screen.FindControl<Button>("KeepConnection")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(disconnect, window.FocusManager.GetFocusedElement());
            disconnect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(screen.TryNavigateBack());
            Assert.Equal(disconnect, window.FocusManager.GetFocusedElement());
            service.Restore = () => throw new CompanionConnectionException(ConnectionFailure.Transport);
            await state.InitializeAsync(TestContext.Current.CancellationToken);
            var forget = screen.FindControl<Button>("AskForget")!;
            forget.Focus();
            forget.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            screen.FindControl<Button>("ConfirmRemoval")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await screen.PendingOperation;
            Assert.Equal(screen.FindControl<TextBox>("HostAddress"), window.FocusManager.GetFocusedElement());
        }
        finally { window.Close(); }
    }
}
