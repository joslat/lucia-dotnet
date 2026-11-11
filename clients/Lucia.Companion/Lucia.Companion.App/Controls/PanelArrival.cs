using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Lucia.Companion.App.Controls;

/// <summary>One bounded panel assembly per presented screen/component, without delaying data or native input.</summary>
public sealed class PanelArrival : AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnabledProperty = AvaloniaProperty.RegisterAttached<PanelArrival, Control, bool>("Enabled");
    public static readonly AttachedProperty<int> OrderProperty = AvaloniaProperty.RegisterAttached<PanelArrival, Control, int>("Order", -1);
    private static readonly ConditionalWeakTable<Control, PanelArrival> Instances = new();
    private readonly Control scope;
    private readonly PresentationVisibility visibility;
    private readonly HashSet<Control> active = [];
    private readonly List<IDisposable> scheduled = [];
    private readonly List<PresentationVisibility> panelVisibility = [];
    private bool attached;
    private bool presented;
    private int version;

    public static void SetEnabled(Control control, bool value) => control.SetValue(EnabledProperty, value);
    public static bool GetEnabled(Control control) => control.GetValue(EnabledProperty);
    public static void SetOrder(Control control, int value) => control.SetValue(OrderProperty, value);
    public static int GetOrder(Control control) => control.GetValue(OrderProperty);

    static PanelArrival() => EnabledProperty.Changed.AddClassHandler<Control>((control, change) =>
    {
        if (change.NewValue is true) { Instances.GetValue(control, owner => new(owner)); }
        else if (Instances.TryGetValue(control, out var arrival)) { arrival.Remove(); Instances.Remove(control); }
    });

    private PanelArrival(Control scope)
    {
        this.scope = scope;
        scope.AttachedToVisualTree += Attached;
        scope.DetachedFromVisualTree += Detached;
        if (TopLevel.GetTopLevel(scope) is not null) { attached = true; MotionSettings.Current.Changed += MotionChanged; }
        visibility = new(scope, VisibilityChanged);
    }

    private void Attached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        attached = true;
        MotionSettings.Current.Changed += MotionChanged;
        VisibilityChanged();
    }

    private void Detached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        attached = false; presented = false;
        MotionSettings.Current.Changed -= MotionChanged;
        Stop();
    }

    private void VisibilityChanged()
    {
        var visible = attached && scope.IsEffectivelyVisible;
        if (visible == presented) { return; }
        presented = visible;
        Stop();
        if (!visible || !MotionSettings.Current.IsEnabled) { return; }
        var activation = version;
        var root = TopLevel.GetTopLevel(scope);
        Dispatcher.UIThread.Post(() =>
        {
            if (activation != version || !attached || !scope.IsEffectivelyVisible || !MotionSettings.Current.IsEnabled
                || !ReferenceEquals(root, TopLevel.GetTopLevel(scope))) { return; }
            // Stable sorting respects the actual saved/visual reading order within each priority.
            var panels = scope.GetVisualDescendants().OfType<Control>().Prepend(scope)
                .Where(panel => GetOrder(panel) >= 0 && panel.IsEffectivelyVisible && (ReferenceEquals(panel, scope)
                    || !GetEnabled(panel) && ReferenceEquals(panel.GetVisualAncestors().OfType<Control>().FirstOrDefault(GetEnabled), scope)))
                .OrderBy(GetOrder).ToArray();
            for (var index = 0; index < panels.Length; index++)
            {
                var panel = panels[index];
                active.Add(panel);
                panel.Transitions = null;
                panel.Opacity = .12;
                panel.RenderTransform = TransformOperations.Parse("translate(0px,6px)");
                panel.Transitions =
                [
                    new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(220), Easing = new CubicEaseOut() },
                    new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(220), Easing = new CubicEaseOut() },
                ];
                panelVisibility.Add(new(panel, () =>
                {
                    if (!panel.IsEffectivelyVisible || !ReferenceEquals(root, TopLevel.GetTopLevel(panel))) { Settle(panel); }
                }));
                var delay = TimeSpan.FromMilliseconds(Math.Min(index, 3) * 28);
                void Begin()
                {
                    if (activation != version || !active.Contains(panel)) { return; }
                    if (!panel.IsEffectivelyVisible || !MotionSettings.Current.IsEnabled || !ReferenceEquals(root, TopLevel.GetTopLevel(panel)))
                    { Settle(panel); return; }
                    panel.Opacity = 1;
                    panel.RenderTransform = TransformOperations.Parse("translate(0px,0px)");
                }
                if (index == 0) { Dispatcher.UIThread.Post(Begin, DispatcherPriority.Render); }
                else { scheduled.Add(DispatcherTimer.RunOnce(Begin, delay)); }
            }
            if (panels.Length > 0) { scheduled.Add(DispatcherTimer.RunOnce(Stop, TimeSpan.FromMilliseconds(335))); }
        }, DispatcherPriority.Loaded);
    }

    private void Settle(Control panel)
    {
        if (!active.Remove(panel)) { return; }
        panel.Transitions = null;
        panel.Opacity = 1;
        panel.RenderTransform = null;
    }

    private void Stop()
    {
        version++;
        foreach (var callback in scheduled) { callback.Dispose(); }
        scheduled.Clear();
        foreach (var watcher in panelVisibility) { watcher.Dispose(); }
        panelVisibility.Clear();
        foreach (var panel in active.ToArray()) { Settle(panel); }
    }

    private void MotionChanged() { if (!MotionSettings.Current.IsEnabled) { Stop(); } }

    private void Remove()
    {
        Stop();
        scope.AttachedToVisualTree -= Attached;
        scope.DetachedFromVisualTree -= Detached;
        MotionSettings.Current.Changed -= MotionChanged;
        visibility.Dispose();
    }
}
