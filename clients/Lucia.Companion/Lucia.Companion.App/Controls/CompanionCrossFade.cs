using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Threading;

namespace Lucia.Companion.App.Controls;

/// <summary>A finite native opacity transition that can settle synchronously when motion is reduced.</summary>
public sealed class CompanionCrossFade(TimeSpan duration) : IPageTransition
{
    private (CancellationTokenSource Cancellation, Visual? From, Visual? To)? active;
    public TimeSpan Duration => duration;

    public void Stop()
    {
        if (active is not { } operation) { return; }
        active = null;
        operation.Cancellation.Cancel();
        Settle(operation.From, operation.To);
    }

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        var continuing = active is not null;
        var fromOpacity = from?.Opacity ?? 1;
        var toOpacity = continuing ? to?.Opacity ?? 0 : 0;
        Stop();
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        active = (operation, from, to);
        Prepare(from, fromOpacity);
        Prepare(to, toOpacity);
        Dispatcher.UIThread.Post(() =>
        {
            if (active?.Cancellation != operation || operation.IsCancellationRequested) { return; }
            if (from is not null) { from.Opacity = 0; }
            if (to is not null) { to.Opacity = 1; }
        }, DispatcherPriority.Render);
        try { await Task.Delay(duration, operation.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A policy change completes the current navigation; the host can discard the old presenter.
        }
        finally
        {
            if (active?.Cancellation == operation) { active = null; Settle(from, to); }
        }
    }

    private void Prepare(Visual? visual, double opacity)
    {
        if (visual is null) { return; }
        visual.Transitions = null;
        visual.IsVisible = true;
        visual.Opacity = opacity;
        visual.Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Easing = new CubicEaseInOut() }];
    }

    private static void Settle(Visual? from, Visual? to)
    {
        if (from is not null) { from.Transitions = null; from.Opacity = 1; from.IsVisible = false; }
        if (to is not null) { to.Transitions = null; to.Opacity = 1; to.IsVisible = true; }
    }
}
