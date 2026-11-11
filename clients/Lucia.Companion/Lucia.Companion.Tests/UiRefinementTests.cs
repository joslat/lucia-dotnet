using Avalonia;
using Avalonia.Automation;
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

public sealed class UiRefinementTests
{
    [AvaloniaFact]
    public void AReportRefreshKeepsTheAttentionButtonBeingPressed()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var home = new HomeScreen(state);
        var window = new Window { Width = 1194, Height = 834, Content = home };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var list = home.FindControl<AttentionList>("Attention")!;
            var row = list.Rows[0];
            var center = Center(row, window);
            window.MouseMove(center); window.MouseDown(center, MouseButton.Left);
            Assert.True(row.IsPressed);
            state.SetReportTime(SampleHome.CapturedAt.AddMinutes(1));
            Dispatcher.UIThread.RunJobs();
            Assert.Same(row, list.Rows[0]);
            Assert.True(row.IsPressed);
            window.MouseUp(center, MouseButton.Left);
            Assert.True(state.IsDetailOpen);
            Assert.Equal(row.Tag, state.SelectedPlace?.Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AttentionUpdatesAndReorderingKeepKeyboardFocusAndCurrentWords()
    {
        var places = SampleHome.Create().Places.Where(HomeAttention.NeedsYou).ToArray();
        var list = new AttentionList();
        list.SetPlaces(places);
        var window = new Window { Width = 500, Height = 900, Content = list };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var row = list.Rows[0];
            row.Focus();
            var updated = places[0] with { Reason = "The source reports a new reason for attention." };
            list.SetPlaces(places.Skip(1).Append(updated).ToArray(), reportTime: SampleHome.CapturedAt.AddMinutes(2));
            Assert.Same(row, list.Rows[^1]);
            Assert.Same(row, window.FocusManager!.GetFocusedElement());
            Assert.Contains(updated.Reason, AutomationProperties.GetName(row));
            string? invoked = null;
            list.PlaceInvoked += id => invoked = id;
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            Assert.Equal(updated.Id, invoked);
            list.SetPlaces(places.Skip(1).ToArray());
            Assert.DoesNotContain(row, list.Rows);
            Assert.Null(row.Parent);
            Assert.Same(list.Rows[0], window.FocusManager.GetFocusedElement());
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void TheAttentionShortcutReachesAndFocusesTheActualList(int width, int height, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var home = new HomeScreen(new HomeViewModel(SampleHome.Create()));
        var window = new Window { Width = width, Height = height, Content = home };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var shortcut = home.FindControl<Button>("ReviewAttention");
            Assert.NotNull(shortcut);
            Assert.True(shortcut.IsEffectivelyVisible);
            Assert.True(shortcut.Bounds.Height >= 48);
            Assert.Contains("Needs you", AutomationProperties.GetName(shortcut));
            shortcut.Focus();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            var row = home.FindControl<AttentionList>("Attention")!.Rows[0];
            Assert.Same(row, window.FocusManager!.GetFocusedElement());
            var position = row.TranslatePoint(default, window)!.Value;
            Assert.InRange(position.Y, 0, height - 48);
            Assert.False(home.State.IsDetailOpen);
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }

    [AvaloniaFact]
    public void HiddenButtonFeedbackStopsAndDoesNotCarryAnOldHoverIntoItsNextAppearance()
    {
        MotionSettings.Current.ReduceMotion = false;
        var place = SampleHome.Create().Places[0];
        var orb = new PlaceOrb(place, PlaceIcons.For(place.Icon));
        var parent = new Border { Child = orb };
        var window = new Window { Width = 300, Height = 300, Content = parent };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            window.MouseMove(Center(orb, window));
            Assert.Contains("mouse-over", orb.Classes);
            parent.IsVisible = false;
            Assert.Null(orb.Transitions);
            Assert.Equal(1, orb.RenderTransform!.Value.M11);
            Assert.DoesNotContain("mouse-over", orb.Classes);
            window.MouseMove(new Point(0, 0));
            parent.IsVisible = true;
            Assert.DoesNotContain("mouse-over", orb.Classes);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    private static Point Center(Control control, Window window) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
}
