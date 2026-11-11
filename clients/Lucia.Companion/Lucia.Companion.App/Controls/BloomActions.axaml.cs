using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>
/// Named native shortcuts in a radial composition, or a list at large text sizes. Every action is a button; holding a place
/// and sliding to an action is only a faster way to reach the same buttons.
/// </summary>
public sealed partial class BloomActions : UserControl
{
    private static readonly (BloomAction Action, string Radial, string List)[] Buttons =
    [
        (BloomAction.Open, "OpenButton", "LargeOpen"),
        (BloomAction.History, "HistoryButton", "LargeHistory"),
        (BloomAction.Pin, "PinButton", "LargePin"),
        (BloomAction.Close, "CloseButton", "LargeClose"),
    ];

    public event Action<BloomAction>? ActionRequested;
    private readonly DispatcherTimer idleTimer = new();
    private readonly DispatcherTimer closeTimer = new();
    private bool pointerSession;
    private bool pointerHeld;
    private bool pointerOnAction;
    private bool sessionActive;
    private int motionVersion;
    private int textAdaptVersion;
    private Action? closeCompleted;
    public bool IsClosing { get; private set; }
    public TimeSpan IdleDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Only pointer-opened radial actions time out. Keyboard and large-text choices stay available.</summary>
    public void BeginSession(bool pointerInitiated, bool holdActive)
    {
        pointerSession = pointerInitiated;
        pointerHeld = holdActive;
        pointerOnAction = false;
        sessionActive = true;
        ScheduleIdle();
    }

    public void SetHoldActive(bool active) { pointerHeld = active; ScheduleIdle(); }

    public BloomActions()
    {
        AvaloniaXamlLoader.Load(this);
        foreach (var (action, radial, list) in Buttons)
        {
            this.FindControl<Button>(radial)!.Click += (_, _) => ActionRequested?.Invoke(action);
            this.FindControl<Button>(list)!.Click += (_, _) => ActionRequested?.Invoke(action);
        }
        PropertyChanged += (_, change) => { if (change.Property == FontSizeProperty) { AdaptText(); } };
        idleTimer.Tick += (_, _) => { idleTimer.Stop(); sessionActive = false; ActionRequested?.Invoke(BloomAction.Close); };
        closeTimer.Tick += (_, _) => FinishClose();
        AttachedToVisualTree += (_, _) => { MotionSettings.Current.Changed += MotionChanged; AdaptText(); ScheduleIdle(); };
        DetachedFromVisualTree += (_, _) =>
        {
            MotionSettings.Current.Changed -= MotionChanged;
            idleTimer.Stop(); closeTimer.Stop(); closeCompleted = null; sessionActive = false; motionVersion++;
            Transitions = null;
            foreach (var (petal, _) in Petals()) { petal.Transitions = null; }
        };
        PointerMoved += (_, args) => { pointerOnAction = ActionAt(args.GetPosition(this)) is not null; ScheduleIdle(); };
        PointerExited += (_, _) => { pointerOnAction = false; ScheduleIdle(); };
        AddHandler(PointerPressedEvent, (_, _) => { pointerSession = true; SetHoldActive(true); }, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, _) => SetHoldActive(false), RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyDown += (_, args) =>
        {
            pointerSession = false; idleTimer.Stop();
            if (args.Key == Key.Escape) { ActionRequested?.Invoke(BloomAction.Close); args.Handled = true; }
        };
    }

    public bool IsLargeText => FontSize > 20;

    public void SetPlace(Place place, bool pinned)
    {
        this.FindControl<TextBlock>("Heading")!.Text = place.Name;
        this.FindControl<TextBlock>("LargeHeading")!.Text = place.Name;
        this.FindControl<Avalonia.Controls.Shapes.Path>("PlaceIcon")!.Data = PlaceIcons.For(place.Icon);
        this.FindControl<BloomActionButton>("PinButton")!.Label = pinned ? "Unpin" : "Pin place";
        this.FindControl<Button>("LargePin")!.Content = pinned ? "Unpin" : "Pin place";
        foreach (var (action, radial, list) in Buttons)
        {
            var name = action switch
            {
                BloomAction.Open => $"Open {place.Name}",
                BloomAction.History => $"{place.Name} history in Story",
                BloomAction.Pin => $"{(pinned ? "Unpin" : "Pin")} {place.Name}",
                _ => $"Close {place.Name} actions",
            };
            this.FindControl<Button>(radial)!.SetValue(AutomationProperties.NameProperty, name);
            this.FindControl<Button>(list)!.SetValue(AutomationProperties.NameProperty, name);
            var help = action switch
            {
                BloomAction.Open => $"Read observations for {place.Name}. This does not control devices.",
                BloomAction.History => $"Open Story for {place.Name}. Connected history is not available yet.",
                BloomAction.Pin => $"{(pinned ? "Unpin" : "Pin")} {place.Name} on this device.",
                _ => "Fold these actions and return to the place.",
            };
            ContextHelp.Set(this.FindControl<Button>(radial)!, help);
            ContextHelp.Set(this.FindControl<Button>(list)!, help);
        }
    }

    /// <summary>The action whose button is under a point in this control's coordinates, if any.</summary>
    public BloomAction? ActionAt(Point point)
    {
        foreach (var (action, radial, list) in Buttons)
        {
            var button = this.FindControl<Button>(IsLargeText ? list : radial)!;
            if (button.IsEffectivelyVisible && button.TranslatePoint(default, this) is { } origin && new Rect(origin, button.Bounds.Size).Contains(point))
            {
                return action;
            }
        }
        return null;
    }

    /// <summary>True when a point has left the Bloom, which cancels a slide.</summary>
    public bool IsOutside(Point point) => !new Rect(Bounds.Size).Contains(point);

    /// <summary>Highlights the action a slide would choose on release. Null clears it.</summary>
    public void Arm(BloomAction? armed)
    {
        pointerOnAction = armed is not null;
        ScheduleIdle();
        foreach (var (action, radial, list) in Buttons)
        {
            this.FindControl<Button>(radial)!.Classes.Set("armed", action == armed);
            this.FindControl<Button>(list)!.Classes.Set("armed", action == armed);
        }
    }

    private void AdaptText()
    {
        var version = ++textAdaptVersion;
        var scope = TopLevel.GetTopLevel(this);
        var focused = scope?.FocusManager?.GetFocusedElement();
        Button? counterpart = null;
        foreach (var (_, radial, list) in Buttons)
        {
            var radialButton = this.FindControl<Button>(radial)!;
            var listButton = this.FindControl<Button>(list)!;
            if (ReferenceEquals(focused, radialButton) || ReferenceEquals(focused, listButton)) { counterpart = IsLargeText ? listButton : radialButton; break; }
        }
        this.FindControl<Grid>("RadialLayout")!.IsVisible = !IsLargeText;
        var fallback = this.FindControl<Border>("LargeTextActions")!;
        fallback.IsVisible = IsLargeText;
        fallback.MaxHeight = Math.Max(120, (TopLevel.GetTopLevel(this)?.Bounds.Height ?? 464) - 64);
        if (counterpart is not null)
        {
            // New list controls need their first layout before accepting focus. Do not reclaim focus if the user moved elsewhere.
            Dispatcher.UIThread.Post(() =>
            {
                var current = scope?.FocusManager?.GetFocusedElement();
                if (version == textAdaptVersion && ReferenceEquals(scope, TopLevel.GetTopLevel(this)) && IsEffectivelyVisible && counterpart.IsEffectivelyVisible
                    && (current is null || ReferenceEquals(current, focused) || ReferenceEquals(current, counterpart)))
                { counterpart.Focus(); counterpart.BringIntoView(); }
            }, DispatcherPriority.Loaded);
        }
        ScheduleIdle();
    }

    /// <summary>With motion, Bloom grows from 86% and fades in over 200 ms; without it, it simply appears.</summary>
    public void PlayOpening(bool animate)
    {
        var continuing = animate && IsClosing;
        var paintedOpacity = Opacity;
        var paintedTransform = RenderTransform;
        var paintedPetals = Petals().ToDictionary(item => item.Petal, item => (Transform: item.Petal.RenderTransform, Opacity: item.Petal.Opacity));
        var version = ++motionVersion;
        closeTimer.Stop(); closeCompleted = null;
        IsClosing = false;
        IsHitTestVisible = true;
        Transitions = null;
        RenderTransformOrigin = new RelativePoint(.5, .5, RelativeUnit.Relative);
        foreach (var (petal, _) in Petals()) { petal.Transitions = null; }
        if (!animate)
        {
            Opacity = 1;
            RenderTransform = null;
            foreach (var (petal, _) in Petals()) { petal.Opacity = 1; petal.RenderTransform = null; }
            ScheduleIdle();
            return;
        }
        Opacity = continuing ? paintedOpacity : 0;
        RenderTransform = continuing ? paintedTransform : TransformOperations.Parse("scale(0.94)");
        foreach (var (petal, offset) in Petals())
        {
            petal.RenderTransformOrigin = new RelativePoint(.5, .25, RelativeUnit.Relative);
            petal.RenderTransform = continuing ? paintedPetals[petal].Transform : TransformOperations.Parse($"translate({offset.X}px,{offset.Y}px) scale(0.65)");
            petal.Opacity = continuing ? paintedPetals[petal].Opacity : 0;
        }
        Transitions =
        [
            new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = new Avalonia.Animation.Easings.CubicEaseOut() },
            new Avalonia.Animation.TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = new Avalonia.Animation.Easings.CubicEaseOut() },
        ];
        var index = 0;
        foreach (var (petal, _) in Petals())
        {
            var delay = TimeSpan.FromMilliseconds(index++ * 15);
            petal.Transitions = [
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(240), Delay = delay, Easing = new CubicEaseOut() },
                new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(160), Delay = delay, Easing = new CubicEaseOut() }];
        }
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (version != motionVersion || IsClosing) { return; }
            Opacity = 1;
            RenderTransform = TransformOperations.Parse("scale(1)");
            foreach (var (petal, _) in Petals()) { petal.Opacity = 1; petal.RenderTransform = TransformOperations.Parse("translate(0px,0px) scale(1)"); }
        }, Avalonia.Threading.DispatcherPriority.Render);
        ScheduleIdle();
    }

    /// <summary>Folds the same petals inward, then dismisses. A subsequent opening cancels this completion.</summary>
    public void PlayClosing(bool animate, Action completed)
    {
        if (IsClosing) { return; }
        motionVersion++;
        idleTimer.Stop(); sessionActive = false;
        closeCompleted = completed;
        IsClosing = true;
        IsHitTestVisible = false;
        if (!animate) { FinishClose(); return; }
        var duration = TimeSpan.FromMilliseconds(140);
        var paintedOpacity = Opacity;
        var paintedTransform = RenderTransform;
        Transitions = null;
        Opacity = paintedOpacity;
        RenderTransform = paintedTransform;
        Transitions = [
            new DoubleTransition { Property = OpacityProperty, Duration = duration, Easing = new CubicEaseIn() },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = duration, Easing = new CubicEaseIn() }];
        foreach (var (petal, offset) in Petals())
        {
            var painted = petal.RenderTransform;
            petal.Transitions = null;
            petal.RenderTransform = painted;
            petal.Transitions = [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = duration, Easing = new CubicEaseIn() }];
            petal.RenderTransform = TransformOperations.Parse($"translate({offset.X}px,{offset.Y}px) scale(0.65)");
        }
        Opacity = 0;
        RenderTransform = TransformOperations.Parse("scale(0.94)");
        closeTimer.Interval = duration;
        closeTimer.Start();
    }

    private void FinishClose()
    {
        closeTimer.Stop();
        IsClosing = false;
        var completed = closeCompleted;
        closeCompleted = null;
        completed?.Invoke();
    }

    private IEnumerable<(Grid Petal, Point Offset)> Petals()
    {
        yield return (this.FindControl<Grid>("HistoryPetal")!, new Point(55, 95));
        yield return (this.FindControl<Grid>("ClosePetal")!, new Point(-55, 95));
        yield return (this.FindControl<Grid>("PinPetal")!, new Point(-98, -17));
        yield return (this.FindControl<Grid>("OpenPetal")!, new Point(98, -17));
    }

    private void ScheduleIdle()
    {
        idleTimer.Stop();
        if (!sessionActive || !pointerSession || pointerHeld || pointerOnAction || IsLargeText || IsClosing || MotionSettings.Current.IsPaused) { return; }
        idleTimer.Interval = IdleDelay > TimeSpan.Zero ? IdleDelay : TimeSpan.FromMilliseconds(1);
        idleTimer.Start();
    }

    private void MotionChanged()
    {
        if (!MotionSettings.Current.IsEnabled)
        {
            motionVersion++;
            Transitions = null;
            foreach (var (petal, _) in Petals()) { petal.Transitions = null; petal.Opacity = 1; petal.RenderTransform = null; }
            Opacity = 1; RenderTransform = null;
            if (IsClosing) { FinishClose(); }
        }
        if (MotionSettings.Current.IsPaused)
        {
            idleTimer.Stop();
            if (sessionActive) { sessionActive = false; ActionRequested?.Invoke(BloomAction.Close); }
        }
    }

    public void FocusFirstAction() => this.FindControl<Button>(IsLargeText ? "LargeOpen" : "OpenButton")!.Focus();
}
