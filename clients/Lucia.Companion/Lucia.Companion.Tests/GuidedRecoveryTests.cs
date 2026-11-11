using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class GuidedRecoveryTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false, false)]
    [InlineData(390, 844, true, false)]
    [InlineData(1194, 834, false, false)]
    [InlineData(1194, 834, true, false)]
    [InlineData(390, 844, false, true)]
    [InlineData(390, 844, true, true)]
    [InlineData(1194, 834, false, true)]
    [InlineData(1194, 834, true, true)]
    public void EmptyHomeHasAnImmediateKeyboardRouteToTheExistingSetupFlow(int width, int height, bool dark, bool large)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var state = new HomeViewModel(new HomeTopology("empty", "New home", [], []), isDemo: false);
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            Assert.Equal("Set up your home", home.FindControl<TextBlock>("AttentionHeadline")!.Text);
            Assert.DoesNotContain(home.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible &&
                (text.Text == "Tap a place for readings. Use Place actions to pin it." || text.Text == "Lines show configured connections, not a floor plan."));
            var setup = home.FindControl<Button>("OpenEmptySetup");
            Assert.NotNull(setup);
            Assert.True(setup.IsEffectivelyVisible);
            Assert.True(setup.Bounds.Height >= 48);
            for (var index = 0; index < 32 && !setup.IsFocused; index++)
            {
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
            }
            Assert.True(setup.IsFocused);
            window.UpdateLayout();
            var position = setup.TranslatePoint(new Point(0, setup.Bounds.Height), window)!.Value;
            Assert.InRange(position.Y, 0, height - (shell.Navigation.IsRail ? 0 : shell.Navigation.Bounds.Height));
            NativeCapture.Save(window, $"quality-empty-{width}-{dark}-{large}.png");
            Activate(window);
            Assert.IsType<HomeEditorScreen>(shell.CurrentScreen);
            Assert.False(shell.Navigation.IsVisible);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(home, shell.CurrentScreen);
            Assert.Empty(state.Home.Places);
            Assert.Same(setup, window.FocusManager!.GetFocusedElement());
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(390, 844, false, false)]
    [InlineData(390, 844, true, false)]
    [InlineData(1194, 834, false, false)]
    [InlineData(1194, 834, true, false)]
    [InlineData(390, 844, false, true)]
    [InlineData(390, 844, true, true)]
    [InlineData(1194, 834, false, true)]
    [InlineData(1194, 834, true, true)]
    public async Task RecoveryLeadsWithRetryAndDisclosesTheExistingRemovalChoices(int width, int height, bool dark, bool large)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var service = new TestCompanionConnection { HasSavedConnection = true, Restore = () => throw new CompanionConnectionException(ConnectionFailure.Transport) };
        var state = new ConnectionViewModel(service);
        var screen = new ConnectionScreen(state);
        var window = new Window { Width = width, Height = height, Content = screen };
        try
        {
            window.Show(); await state.InitializeAsync(TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
            var retry = screen.FindControl<Button>("RetryConnection")!;
            Assert.True(retry.IsEffectivelyVisible);
            var options = screen.FindControl<Expander>("ConnectionOptions");
            Assert.NotNull(options);
            Assert.False(options.IsExpanded);
            NativeCapture.Save(window, $"quality-recovery-{width}-{dark}-{large}.png");
            var forget = screen.FindControl<Button>("AskForget")!;
            Assert.False(forget.IsEffectivelyVisible);
            options.IsExpanded = true; window.UpdateLayout();
            Assert.True(forget.IsEffectivelyVisible);
            forget.Focus(); Activate(window);
            Assert.Same(screen.FindControl<Button>("KeepConnection"), window.FocusManager!.GetFocusedElement());
            Assert.True(screen.TryNavigateBack());
            Assert.Same(forget, window.FocusManager.GetFocusedElement());
            Assert.True(service.HasSavedConnection);
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }

    private static void Activate(Window window)
    {
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
    }
}
