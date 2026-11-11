using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class PresentationStressTests
{
    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(1194, false)]
    [InlineData(1194, true)]
    public async Task RapidKeyboardNavigationSettlesOnTheLatestDestinationWithoutLosingFocus(int width, bool reduced)
    {
        MotionSettings.Current.ReduceMotion = reduced;
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = 844, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            foreach (var destination in new[] { ShellDestination.Story, ShellDestination.More, ShellDestination.Lucia, ShellDestination.Home, ShellDestination.Story, ShellDestination.Home })
            {
                var button = shell.Navigation.ButtonFor(destination);
                button.Focus();
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
                Assert.Equal(destination, shell.Navigation.Selected);
                Assert.Same(button, window.FocusManager!.GetFocusedElement());
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }
            for (var index = 0; index < 8; index++)
            {
                await Task.Delay(35, TestContext.Current.CancellationToken);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            }
            var screen = Assert.IsAssignableFrom<Control>(shell.CurrentScreen);
            Assert.True(screen.IsEffectivelyVisible);
            Assert.Equal(1, screen.Opacity);
            Assert.All(screen.GetVisualAncestors().TakeWhile(item => !ReferenceEquals(item, shell)), item => Assert.Equal(1, item.Opacity));
            Assert.Same(shell.Navigation.ButtonFor(ShellDestination.Home), window.FocusManager!.GetFocusedElement());
            NativeCapture.Save(window, $"quality-navigation-settled-{width}-{reduced}.png");
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public void HidingAnAlreadyOpenBloomRemovesItsPopupImmediately()
    {
        var home = new App.Screens.HomeScreen(new HomeViewModel(SampleHome.Create()));
        var parent = new Border { Child = home };
        var window = new Window { Width = 390, Height = 844, Content = parent };
        try
        {
            window.Show(); home.State.OpenActionsFor("garden"); Dispatcher.UIThread.RunJobs();
            var popup = home.FindControl<Avalonia.Controls.Primitives.Popup>("ActionPopup")!;
            Assert.True(popup.IsOpen);
            parent.IsVisible = false;
            Assert.False(popup.IsOpen);
            Assert.False(home.State.IsActionsOpen);
            Assert.False(home.FindControl<Border>("BloomShade")!.IsVisible);
            parent.IsVisible = true; Dispatcher.UIThread.RunJobs();
            Assert.False(popup.IsOpen);
        }
        finally { window.Close(); }
    }
}
