using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class PlaceHitTargetTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnEnlargedHoldCanSlideToAChoiceOrReturnToItsOrigin(bool returnToOrigin)
    {
        CompanionTypography.Apply(size => size * 2);
        var state = new HomeViewModel(SampleHome.Create()); var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 390, Height = 800, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); var home = (HomeScreen)shell.CurrentScreen!;
            var map = home.FindControl<HomeConstellation>("Constellation")!; map.HoldDelay = TimeSpan.Zero;
            var orb = map.Orbs[0];
            foreach (var scroll in orb.GetVisualAncestors().OfType<ScrollViewer>().Where(item => item.VerticalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled))
            {
                if (scroll.Content is Visual content && orb.TranslatePoint(default, content) is { } top) { scroll.Offset = new Vector(0, top.Y); }
            }
            Dispatcher.UIThread.RunJobs();
            var origin = orb.TranslatePoint(new Point(orb.Bounds.Width / 2, orb.Bounds.Height / 2), window)!.Value;
            window.MouseDown(origin, MouseButton.Left); Dispatcher.UIThread.RunJobs();
            var history = home.FindControl<BloomActions>("PlaceActions")!.FindControl<Button>("LargeHistory")!;
            var choice = history.TranslatePoint(new Point(history.Bounds.Width / 2, history.Bounds.Height / 2), window)!.Value;
            Assert.True((choice.X-origin.X)*(choice.X-origin.X) + (choice.Y-origin.Y)*(choice.Y-origin.Y) > 100);
            window.MouseMove(choice); Assert.Contains("armed", history.Classes);
            if (returnToOrigin)
            {
                window.MouseMove(origin); Assert.DoesNotContain("armed", history.Classes);
                window.MouseUp(origin, MouseButton.Left); Dispatcher.UIThread.RunJobs();
                Assert.Same(home, shell.CurrentScreen); Assert.True(state.IsActionsOpen); Assert.Empty(state.PinnedPlaceIds);
            }
            else
            {
                window.MouseUp(choice, MouseButton.Left); Dispatcher.UIThread.RunJobs();
                Assert.Equal((string)orb.Tag!, Assert.IsType<StoryScreen>(shell.CurrentScreen).State.SelectedPlaceId);
            }
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaFact]
    public void LayoutMovementDoesNotTurnAStationaryHeldPointerIntoASlide()
    {
        var map = new HomeConstellation { HoldDelay = TimeSpan.Zero };
        map.SetHome(new HomeTopology("target-test", "Target test", [SampleHome.Create().Places[0]], []));
        var spacer = new Border { Height = 0 };
        var window = new Window { Width = 390, Height = 800, Content = new StackPanel { Children = { spacer, map } } };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); var orb = Assert.Single(map.Orbs);
            var point = orb.TranslatePoint(new Point(orb.Bounds.Width / 2, orb.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); Dispatcher.UIThread.RunJobs(); Assert.True(map.IsHolding);
            spacer.Height = 24; Dispatcher.UIThread.RunJobs(); window.MouseMove(point);
            Assert.False(map.IsSliding);
            window.MouseUp(point, MouseButton.Left);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(.25, 1)]
    [InlineData(.5, 1)]
    [InlineData(.75, 1)]
    [InlineData(.25, 2)]
    [InlineData(.5, 2)]
    [InlineData(.75, 2)]
    public void LiftingAStationaryHoldDoesNotChooseAnAction(double fraction, int scale)
    {
        CompanionTypography.Apply(size => size * scale);
        var state = new HomeViewModel(SampleHome.Create()); var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 390, Height = 800, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); var home = (HomeScreen)shell.CurrentScreen!;
            var map = home.FindControl<HomeConstellation>("Constellation")!; map.HoldDelay = TimeSpan.Zero;
            var orb = map.Orbs[0];
            foreach (var scroll in orb.GetVisualAncestors().OfType<ScrollViewer>().Where(item => item.VerticalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled))
            {
                if (scroll.Content is Visual content && orb.TranslatePoint(default, content) is { } top) { scroll.Offset = new Vector(0, top.Y); }
            }
            Dispatcher.UIThread.RunJobs();
            var point = orb.TranslatePoint(new Point(orb.Bounds.Width / 2, orb.Bounds.Height * fraction), window)!.Value;
            Assert.InRange(point.Y, 0, 800);
            window.MouseDown(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
            Assert.True(state.IsActionsOpen);
            window.MouseUp(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
            Assert.Same(home, shell.CurrentScreen); Assert.True(state.IsActionsOpen); Assert.False(home.IsPageVisible);
            Assert.Empty(state.PinnedPlaceIds);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void SpacingAndPaddingInsideAPlaceRemainPartOfItsInteractiveTarget(int scale, bool padding)
    {
        CompanionTypography.Apply(size => size * scale);
        var map = new HomeConstellation { HoldDelay = TimeSpan.Zero };
        var place = SampleHome.Create().Places[0];
        map.SetHome(new HomeTopology("target-test", "Target test", [place], []));
        var holds = 0; map.HoldStarted += _ => holds++;
        var window = new Window { Width = 390, Height = 800, Content = map };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var orb = Assert.Single(map.Orbs);
            Point point;
            if (padding) { point = orb.TranslatePoint(new Point(orb.Bounds.Width - 6, orb.Bounds.Height / 2), window)!.Value; }
            else
            {
                var circle = orb.GetVisualDescendants().OfType<Grid>().Single(item => item.Name == "Orb");
                var label = orb.GetVisualDescendants().OfType<TextBlock>().Single(item => item.Text == place.Name);
                var bottom = circle.TranslatePoint(new Point(circle.Bounds.Width / 2, circle.Bounds.Height), window)!.Value;
                var top = label.TranslatePoint(default, window)!.Value.Y;
                Assert.True(top > bottom.Y);
                point = new Point(bottom.X, (bottom.Y + top) / 2);
            }
            window.MouseDown(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, holds); Assert.True(map.IsHolding);
            window.MouseUp(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
            Assert.False(map.IsHolding);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }
}
