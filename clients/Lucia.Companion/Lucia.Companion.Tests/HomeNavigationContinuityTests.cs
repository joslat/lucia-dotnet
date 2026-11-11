using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeNavigationContinuityTests
{
    [AvaloniaFact]
    public void ARemovedAttentionOriginFallsBackToTheSamePlacesVisibleOrb()
    {
        var source = SampleHome.Create();
        var state = new HomeViewModel(source);
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            home.FindControl<Button>("ReviewAttention")!.Focus(); Activate(window); Activate(window);
            var id = state.SelectedPlace!.Id;
            state.ReplaceHome(new HomeTopology(source.Id, source.Name, source.Places.Select(place => place.Id == id ? place with { State = PlaceState.Quiet } : place), source.Connections, source.Bindings), notifyPreferences: false);
            Activate(window);
            var focused = Assert.IsType<PlaceOrb>(window.FocusManager!.GetFocusedElement());
            Assert.Equal(id, focused.Tag);
            Assert.True(focused.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ADeferredOverviewReturnDoesNotStealFocusFromAnotherDestination()
    {
        MotionSettings.Current.ReduceMotion = false;
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            home.FindControl<Button>("ReviewAttention")!.Focus(); Activate(window); Activate(window);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            var more = shell.Navigation.ButtonFor(ShellDestination.More);
            more.Focus(); Activate(window);
            Assert.IsType<MoreScreen>(shell.CurrentScreen);
            Assert.Same(more, window.FocusManager!.GetFocusedElement());
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(834, false)]
    [InlineData(834, true)]
    public void APlacePageStartsAtItsOwnTopAfterOpeningFromAScrolledAttentionList(int width, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = 844, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            home.FindControl<Button>("ReviewAttention")!.Focus(); Activate(window);
            var scroll = home.FindControl<ScrollViewer>("ContextScroll")!;
            Assert.True(scroll.Offset.Y > 0);
            Activate(window);
            Assert.True(home.IsPageVisible);
            Assert.Equal(Vector.Zero, scroll.Offset);
            var back = home.FindControl<PlacePage>("PlacePage")!.FindControl<Button>("BackToHome")!;
            Assert.Same(back, window.FocusManager!.GetFocusedElement());
            Assert.True(back.TranslatePoint(default, window)!.Value.Y >= scroll.TranslatePoint(default, window)!.Value.Y);
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(834, false)]
    [InlineData(834, true)]
    public void BackRestoresTheAttentionActionAndOverviewPositionThatOpenedThePage(int width, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = 844, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            home.FindControl<Button>("ReviewAttention")!.Focus(); Activate(window);
            var row = home.FindControl<AttentionList>("Attention")!.Rows[0];
            Assert.Same(row, window.FocusManager!.GetFocusedElement());
            var scroll = home.FindControl<ScrollViewer>("ContextScroll")!;
            var offset = scroll.Offset;
            Activate(window);
            Assert.True(home.IsPageVisible);
            Activate(window);
            Assert.False(home.IsPageVisible);
            Assert.Same(row, window.FocusManager!.GetFocusedElement());
            Assert.Equal(offset.Y, scroll.Offset.Y, precision: 1);
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
