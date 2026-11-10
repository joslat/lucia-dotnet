using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace Lucia.Companion.App.Controls;

/// <summary>A finite arrival for explicit view boundaries. Refreshing data is not an arrival.</summary>
public sealed class SurfaceEntrance : AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnabledProperty = AvaloniaProperty.RegisterAttached<SurfaceEntrance, Control, bool>("Enabled");
    private static readonly ConditionalWeakTable<Control, SurfaceEntrance> Instances = new();
    private readonly Control control;
    private readonly PresentationVisibility visibility;
    private bool attached;
    private bool presented;
    private int version;

    public static void SetEnabled(Control control, bool value) => control.SetValue(EnabledProperty, value);
    public static bool GetEnabled(Control control) => control.GetValue(EnabledProperty);
    static SurfaceEntrance() => EnabledProperty.Changed.AddClassHandler<Control>((control, args) =>
    {
        if (args.NewValue is true) { Instances.GetValue(control, owner => new(owner)); }
        else if (Instances.TryGetValue(control, out var entrance)) { entrance.Remove(); Instances.Remove(control); }
    });

    private SurfaceEntrance(Control control)
    {
        this.control = control;
        control.AttachedToVisualTree += Attached;
        control.DetachedFromVisualTree += Detached;
        if (TopLevel.GetTopLevel(control) is not null) { attached = true; MotionSettings.Current.Changed += MotionChanged; }
        visibility = new PresentationVisibility(control, VisibilityChanged);
    }
    private void Attached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        attached = true;
        MotionSettings.Current.Changed += MotionChanged;
    }
    private void Detached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        attached = false; presented = false;
        MotionSettings.Current.Changed -= MotionChanged;
        Stop();
    }
    private void VisibilityChanged()
    {
        var visible = attached && control.IsEffectivelyVisible;
        if (visible == presented) { return; }
        presented = visible;
        if (visible && MotionSettings.Current.IsEnabled) { Play(); }
        else { Stop(); }
    }
    private void Play()
    {
        var current = ++version;
        control.Transitions = null;
        control.RenderTransform = TransformOperations.Parse("translate(0px,4px)");
        control.Transitions = [new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(220), Easing = new CubicEaseOut() }];
        Dispatcher.UIThread.Post(() =>
        {
            if (current == version && attached && control.IsEffectivelyVisible && MotionSettings.Current.IsEnabled)
            { control.RenderTransform = TransformOperations.Parse("translate(0px,0px)"); }
        }, DispatcherPriority.Render);
    }
    private void Stop()
    {
        version++;
        control.Transitions = null;
        control.RenderTransform = TransformOperations.Parse("translate(0px,0px)");
    }
    private void MotionChanged() { if (!MotionSettings.Current.IsEnabled) { Stop(); } }
    private void Remove()
    {
        MotionSettings.Current.Changed -= MotionChanged;
        control.AttachedToVisualTree -= Attached;
        control.DetachedFromVisualTree -= Detached;
        visibility.Dispose();
        Stop();
    }
}
