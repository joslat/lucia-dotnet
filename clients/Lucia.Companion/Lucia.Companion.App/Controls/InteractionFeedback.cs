using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;

namespace Lucia.Companion.App.Controls;

/// <summary>Shared, bounded button feedback. Subscriptions and transitions live only while the button is attached.</summary>
public sealed class InteractionFeedback : AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnabledProperty = AvaloniaProperty.RegisterAttached<InteractionFeedback, Button, bool>("Enabled");
    private static readonly ConditionalWeakTable<Button, InteractionFeedback> Instances = new();
    private readonly Button button;
    private readonly PresentationVisibility visibility;
    private bool attached;
    private bool mouseOver;

    public static bool GetEnabled(Button button) => button.GetValue(EnabledProperty);
    public static void SetEnabled(Button button, bool value) => button.SetValue(EnabledProperty, value);

    static InteractionFeedback() => EnabledProperty.Changed.AddClassHandler<Button>((button, change) =>
    {
        if (change.NewValue is true) { Instances.GetValue(button, item => new InteractionFeedback(item)); }
        else if (Instances.TryGetValue(button, out var feedback)) { feedback.Remove(); Instances.Remove(button); }
    });

    private InteractionFeedback(Button button)
    {
        this.button = button;
        button.AttachedToVisualTree += Attached;
        button.DetachedFromVisualTree += Detached;
        button.TemplateApplied += TemplateApplied;
        button.PointerEntered += PointerEntered;
        button.PointerExited += PointerExited;
        button.PropertyChanged += ButtonPropertyChanged;
        if (TopLevel.GetTopLevel(button) is not null) { Attach(); }
        visibility = new PresentationVisibility(button, ApplyMotion);
    }

    private void Attached(object? sender, VisualTreeAttachmentEventArgs args) => Attach();
    private void Detached(object? sender, VisualTreeAttachmentEventArgs args) => Detach();
    private void TemplateApplied(object? sender, TemplateAppliedEventArgs args) { if (attached) { ApplyMotion(); } }
    private void Attach()
    {
        if (attached) { return; }
        attached = true;
        MotionSettings.Current.Changed += ApplyMotion;
        ApplyMotion();
    }

    private void Detach()
    {
        if (!attached) { return; }
        attached = false;
        MotionSettings.Current.Changed -= ApplyMotion;
        mouseOver = false;
        button.Classes.Remove("mouse-over");
        ApplyMotion();
    }

    private void ApplyMotion()
    {
        var animate = attached && button.IsEffectivelyVisible && MotionSettings.Current.IsEnabled;
        if (!attached || !button.IsEffectivelyVisible)
        {
            mouseOver = false;
            button.Classes.Remove("mouse-over");
        }
        // Remove active transitions first so reducing motion immediately settles any interpolation in flight.
        button.Transitions = null;
        button.RenderTransformOrigin = new RelativePoint(.5, .5, RelativeUnit.Relative);
        if (!animate) { button.RenderTransform = TransformOperations.Parse("scale(1)"); }
        if (animate)
        {
            button.Transitions =
            [
                new BrushTransition { Property = Button.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(180), Easing = new CubicEaseInOut() },
                new BrushTransition { Property = Button.ForegroundProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = new CubicEaseInOut() },
                new BrushTransition { Property = Button.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = new CubicEaseInOut() },
                new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(120), Easing = new CubicEaseOut() },
                new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseOut() },
            ];
        }
        foreach (var part in button.GetVisualDescendants().OfType<Control>())
        {
            if (part is Shape { Name: "StateRing" } ring)
            {
                ring.Transitions = animate ? [new BrushTransition { Property = Shape.FillProperty, Duration = TimeSpan.FromMilliseconds(180), Easing = new CubicEaseInOut() }] : null;
            }
            else if (part is Border { Name: "ActionCircle" } circle)
            {
                circle.Transitions = animate ? [
                    new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseInOut() },
                    new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseInOut() }] : null;
            }
            else if (part is ContentPresenter { Name: "PART_ContentPresenter" } presenter)
            {
                presenter.Transitions = animate ? [
                    new BrushTransition { Property = ContentPresenter.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseInOut() },
                    new BrushTransition { Property = ContentPresenter.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseInOut() }] : null;
            }
        }
        UpdateTransform();
    }

    private void PointerEntered(object? sender, PointerEventArgs args)
    {
        if (args.Pointer.Type != PointerType.Mouse) { return; }
        mouseOver = true;
        button.Classes.Set("mouse-over", button.IsEffectivelyEnabled);
        UpdateTransform();
    }

    private void PointerExited(object? sender, PointerEventArgs args)
    {
        mouseOver = false;
        button.Classes.Remove("mouse-over");
        UpdateTransform();
    }

    private void ButtonPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == Button.IsPressedProperty || args.Property == InputElement.IsEffectivelyEnabledProperty) { UpdateTransform(); }
    }

    private void UpdateTransform()
    {
        var scale = !attached || !button.IsEffectivelyVisible || !MotionSettings.Current.IsEnabled || !button.IsEffectivelyEnabled ? "1"
            : button.IsPressed ? "0.975"
            : mouseOver && button is PlaceOrb or BloomActionButton ? "1.02" : "1";
        button.RenderTransform = TransformOperations.Parse($"scale({scale})");
    }

    private void Remove()
    {
        Detach();
        button.AttachedToVisualTree -= Attached;
        button.DetachedFromVisualTree -= Detached;
        button.TemplateApplied -= TemplateApplied;
        button.PointerEntered -= PointerEntered;
        button.PointerExited -= PointerExited;
        button.PropertyChanged -= ButtonPropertyChanged;
        visibility.Dispose();
    }
}
