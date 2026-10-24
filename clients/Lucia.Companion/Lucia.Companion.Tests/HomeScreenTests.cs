using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeScreenTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void SharedHomeAdaptsAndRenders(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var view = new HomeScreen();
        var window = new Window { Width = width, Height = height, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(width >= 900, view.IsExpanded);
            var orbs = view.GetVisualDescendants().OfType<PlaceOrb>().ToArray();
            Assert.Equal(6, orbs.Length);
            Assert.All(orbs, orb => Assert.True(orb.Bounds.Width >= 48));
            Assert.All(orbs, orb => Assert.InRange(orb.Bounds.Right, 0, width));
            Assert.Equal(3, view.State.AttentionPlaces.Count);
            NativeCapture.Save(window, $"home-{width}-{(dark ? "dark" : "light")}.png");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PhoneSelectionActionsAndBackUseOneState()
    {
        var view = new HomeScreen();
        var window = new Window { Width = 390, Height = 844, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var garden = view.GetVisualDescendants().OfType<PlaceOrb>().Single(orb => orb.PlaceName == "Garden");
            garden.Focus();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("garden", view.State.SelectedPlace!.Id);
            Assert.True(view.State.IsDetailOpen);
            Assert.True(view.FindControl<ScrollViewer>("DetailScroll")!.IsVisible);
            NativeCapture.Save(window, "home-phone-readings.png");
            view.State.OpenActions();
            Dispatcher.UIThread.RunJobs();
            var bloom = view.FindControl<BloomActions>("PlaceActions")!;
            Assert.True(view.FindControl<Avalonia.Controls.Primitives.Popup>("ActionPopup")!.IsOpen);
            bloom.FindControl<Button>("PinButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(view.State.IsPinned("garden"));
            NativeCapture.Save(window, "home-phone-actions.png");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.False(view.State.IsActionsOpen);
            view.FindControl<Button>("BackToHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(view.State.IsDetailOpen);
            Assert.Equal("garden", view.State.SelectedPlace.Id);
            Assert.True(garden.IsSelected);
            var actions = garden.ContextMenu!.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Place actions"));
            actions.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.State.IsDetailOpen);
            Assert.True(view.State.IsActionsOpen);
            NativeCapture.Save(window, "home-phone-orb-bloom.png");
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(40)]
    public void EmptySingleAndLargeHomesHaveNoInvalidLayout(int count)
    {
        var places = Enumerable.Range(0, count).Select(index => new Place($"room-{index}",
            $"A room with a longer name {index}", PlaceState.Quiet, "Sample only", []));
        var view = new HomeScreen(new HomeViewModel(new("layout-test", "Layout test", places, [])));
        var window = new Window { Width = 360, Height = 800, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var orbs = view.GetVisualDescendants().OfType<PlaceOrb>().ToArray();
            Assert.Equal(count, orbs.Length);
            Assert.All(orbs, orb => Assert.True(double.IsFinite(orb.Bounds.Y) && orb.Bounds.Height >= 48));
            var sorted = orbs.OrderBy(orb => orb.Bounds.Y).ThenBy(orb => orb.Bounds.X).ToArray();
            Assert.Equal(count, sorted.Select(orb => orb.Bounds.Position).Distinct().Count());
        }
        finally { window.Close(); }
    }

}
