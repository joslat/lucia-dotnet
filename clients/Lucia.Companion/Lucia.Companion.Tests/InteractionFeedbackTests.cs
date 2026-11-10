using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class InteractionFeedbackTests
{
    [AvaloniaFact]
    public void CustomButtonsHaveAHandCursorAndCentredContentWithRoomForTheirLabels()
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 1194, Height = 834, Content = shell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var orb = shell.GetVisualDescendants().OfType<PlaceOrb>().First();
            Assert.Equal("Hand", orb.Cursor?.ToString());
            Assert.Equal(VerticalAlignment.Center, orb.VerticalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, shell.Navigation.ButtonFor(ShellDestination.More).VerticalContentAlignment);
            var attention = shell.GetVisualDescendants().OfType<AttentionList>().First().Rows.First();
            Assert.Equal(VerticalAlignment.Center, attention.VerticalContentAlignment);
            Assert.True(attention.Padding.Left >= 10);
            Assert.Equal(HorizontalAlignment.Stretch, attention.HorizontalContentAlignment);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PointerFeedbackIsMouseOnlyAndReducedMotionRemovesItsTransitions()
    {
        var motion = MotionSettings.Current;
        motion.ReduceMotion = false;
        var orb = new PlaceOrb(SampleHome.Create().Places[0], PlaceIcons.For(Core.PlaceIcon.Home));
        var window = new Window { Width = 300, Height = 300, Content = orb };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(orb.Transitions);
            Assert.Contains(orb.Transitions, item => item is BrushTransition transition && transition.Property == Button.BorderBrushProperty);
            window.MouseMove(new Point(150, 150));
            Assert.Contains("mouse-over", orb.Classes);
            motion.ReduceMotion = true;
            Assert.Null(orb.Transitions);
            Assert.Equal(1, orb.RenderTransform?.Value.M11 ?? 1);
            motion.ReduceMotion = false;
            Assert.NotNull(orb.Transitions);
            motion.Pause();
            Assert.Null(orb.Transitions);
        }
        finally { motion.ReduceMotion = true; motion.Resume(); window.Close(); }
        Assert.Null(orb.Transitions);
    }

    [AvaloniaFact]
    public void ReapplyingTheSamePlaceDoesNotRestartItsStateRepaint()
    {
        var place = SampleHome.Create().Places.First(item => item.State == Core.PlaceState.Waiting);
        var orb = new PlaceOrb(place, PlaceIcons.For(place.Icon));
        var window = new Window { Content = orb };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var repaints = 0;
            orb.PropertyChanged += (_, args) => { if (args.Property == Button.ForegroundProperty || args.Property == Button.BorderBrushProperty) { repaints++; } };
            orb.Apply(place, PlaceIcons.For(place.Icon));
            Assert.Equal(0, repaints);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextHelpIsDelayedAndExplainsTheNavigationAndActions()
    {
        var navigation = new ShellNavigation();
        var more = navigation.ButtonFor(ShellDestination.More);
        Assert.Equal(650, ToolTip.GetShowDelay(more));
        Assert.NotNull(ToolTip.GetTip(more));
        Assert.Contains("Appearance", more.GetValue(Avalonia.Automation.AutomationProperties.HelpTextProperty));
        var bloom = new BloomActions();
        bloom.SetPlace(SampleHome.Create().Places[0], pinned: false);
        var pin = bloom.FindControl<Button>("PinButton")!;
        Assert.Equal(650, ToolTip.GetShowDelay(pin));
        Assert.NotNull(ToolTip.GetTip(pin));
    }
}
