using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.Controls;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class BloomMotionTests
{
    [AvaloniaFact]
    public void InterruptingTheUnfoldStartsTheFoldAtTheCurrentPaintedOpacity()
    {
        var bloom = new BloomActions();
        var window = new Window { Content = bloom };
        try
        {
            window.Show();
            bloom.PlayOpening(true);
            Dispatcher.UIThread.RunJobs();
            var unfolding = bloom.Opacity;
            bloom.PlayClosing(true, () => { });
            Assert.InRange(Math.Abs(unfolding - bloom.Opacity), 0, .01);
            var folding = bloom.Opacity;
            bloom.PlayOpening(true);
            Assert.InRange(Math.Abs(folding - bloom.Opacity), 0, .01);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ActionsUnfoldIndividuallyFromTheCentreAndReducedMotionSnapsToTheFinalLayout()
    {
        var bloom = new BloomActions();
        var window = new Window { Width = 400, Height = 400, Content = bloom };
        try
        {
            window.Show();
            bloom.PlayOpening(true);
            Dispatcher.UIThread.RunJobs();
            var petals = bloom.GetVisualDescendants().OfType<Grid>().Where(item => item.Name?.EndsWith("Petal", StringComparison.Ordinal) == true).ToArray();
            Assert.Equal(4, petals.Length);
            Assert.All(petals, petal => Assert.Contains(petal.Transitions!, transition => transition is TransformOperationsTransition { Duration.TotalMilliseconds: 240 }));
            bloom.PlayOpening(false);
            Assert.All(petals, petal => { Assert.Null(petal.Transitions); Assert.Equal(1, petal.Opacity); Assert.Equal(1, petal.RenderTransform?.Value.M11 ?? 1); });
            Assert.Equal(1, bloom.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ClosingFoldsBeforeDismissalAndReopeningCancelsTheOldCompletion()
    {
        var bloom = new BloomActions();
        var window = new Window { Content = bloom };
        try
        {
            window.Show();
            bloom.PlayOpening(false);
            var closed = false;
            bloom.PlayClosing(true, () => closed = true);
            Assert.True(bloom.IsClosing);
            Assert.False(bloom.IsHitTestVisible);
            Assert.False(closed);
            bloom.PlayOpening(false);
            await Task.Delay(200, TestContext.Current.CancellationToken);
            Assert.False(closed);
            Assert.False(bloom.IsClosing);
            Assert.True(bloom.IsHitTestVisible);
            bloom.PlayClosing(false, () => closed = true);
            Assert.True(closed);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task AnIdlePointerMenuClosesButAKeyboardOrHeldMenuStaysAvailable()
    {
        var bloom = new BloomActions { IdleDelay = TimeSpan.FromMilliseconds(20) };
        var window = new Window { Width = 400, Height = 400, Content = bloom };
        try
        {
            window.Show();
            var closes = 0;
            bloom.ActionRequested += action => { if (action == BloomAction.Close) { closes++; } };
            bloom.BeginSession(pointerInitiated: true, holdActive: false);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.Equal(1, closes);
            bloom.BeginSession(pointerInitiated: false, holdActive: false);
            await Task.Delay(60, TestContext.Current.CancellationToken);
            Assert.Equal(1, closes);
            bloom.BeginSession(pointerInitiated: true, holdActive: true);
            await Task.Delay(60, TestContext.Current.CancellationToken);
            Assert.Equal(1, closes);
            bloom.SetHoldActive(false);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.Equal(2, closes);
            bloom.FontSize = 32;
            bloom.BeginSession(pointerInitiated: true, holdActive: false);
            await Task.Delay(60, TestContext.Current.CancellationToken);
            Assert.Equal(2, closes);
        }
        finally { window.Close(); }
    }
}
