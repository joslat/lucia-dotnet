using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class RemovedSelectionReturnTests
{
    [AvaloniaFact]
    public void SavingARetainedPlacePreservesItsPageAndReadingOffset()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 1194, Height = 500, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            state.SelectPlace("garden"); state.OpenPage(); Dispatcher.UIThread.RunJobs();
            var reading = home.FindControl<ScrollViewer>("DetailScroll")!;
            reading.Offset = new Vector(0, 180); Dispatcher.UIThread.RunJobs(); var offset = reading.Offset;
            Assert.True(offset.Y > 0);
            shell.Navigate(ShellDestination.More);
            Assert.IsType<MoreScreen>(shell.CurrentScreen).FindControl<Button>("OpenHomeSetup")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsType<HomeEditorScreen>(shell.CurrentScreen).FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(home.IsPageVisible); Assert.Equal(offset, reading.Offset);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ARemovalReturnCannotStealFocusFromANewerDestination()
    {
        var state = new HomeViewModel(SampleHome.Create()); var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 1194, Height = 800, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); state.SelectPlace("living"); state.OpenPage(); Dispatcher.UIThread.RunJobs();
            shell.Navigate(ShellDestination.More);
            Assert.IsType<MoreScreen>(shell.CurrentScreen).FindControl<Button>("OpenHomeSetup")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var editor = Assert.IsType<HomeEditorScreen>(shell.CurrentScreen);
            editor.FindControl<Button>("RemovePlace")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editor.FindControl<Button>("ConfirmRemove")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            shell.Navigate(ShellDestination.More); var more = shell.Navigation.ButtonFor(ShellDestination.More); more.Focus();
            Dispatcher.UIThread.RunJobs(); Assert.IsType<MoreScreen>(shell.CurrentScreen);
            Assert.Same(more, window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(390, false, false)]
    [InlineData(390, true, false)]
    [InlineData(1194, false, false)]
    [InlineData(1194, true, false)]
    [InlineData(390, true, true)]
    public void SavingRemovalRevealsTheCurrentOverviewAnchor(int width, bool large, bool empty)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var source = SampleHome.Create();
        if (empty) { source = new(source.Id, source.Name, [source.Places[0]], [], []); }
        var state = new HomeViewModel(source);
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = 800, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            home.FindControl<ScrollViewer>(large ? "ContextScroll" : "HomeScroll")!.Offset = new Vector(0, 450);
            Dispatcher.UIThread.RunJobs();
            state.SelectPlace(source.Places[0].Id); state.OpenPage(); Dispatcher.UIThread.RunJobs();
            shell.Navigate(ShellDestination.More);
            Assert.IsType<MoreScreen>(shell.CurrentScreen).FindControl<Button>("OpenHomeSetup")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var editor = Assert.IsType<HomeEditorScreen>(shell.CurrentScreen);
            editor.FindControl<Button>("RemovePlace")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editor.FindControl<Button>("ConfirmRemove")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var target = home.FindControl<Control>(empty ? "OpenEmptySetup" : "PlacesTitle");
            Assert.NotNull(target); Assert.Same(target, window.FocusManager!.GetFocusedElement());
            var point = target.TranslatePoint(default, window)!.Value;
            Assert.True(point.Y >= 0 && point.Y + target.Bounds.Height <= 800);
            Assert.False(home.IsPageVisible);
            NativeCapture.Save(window, $"removed-selection-anchor-{width}-{large}-{empty}.png");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }
}
