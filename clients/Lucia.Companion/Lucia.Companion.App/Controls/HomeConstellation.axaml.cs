using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

public sealed partial class HomeConstellation : UserControl
{
    private readonly Dictionary<string, PlaceOrb> orbs = new(StringComparer.Ordinal);
    private readonly List<PlaceOrb> orderedOrbs = [];
    private readonly List<TextBlock> zoneLabels = [];
    /// <summary>Places in display order: grouped by zone when any zone is set, otherwise in setup order.</summary>
    public IReadOnlyList<PlaceOrb> Orbs => orderedOrbs;
    public IReadOnlyList<TextBlock> ZoneLabels => zoneLabels;
    public event Action<string>? PlaceInvoked;
    public event Action<string>? ActionsRequested;
    public event Action<string>? PinRequested;
    /// <summary>A place was held long enough to open Bloom; the pointer is still down and may slide to an action.</summary>
    public event Action<string>? HoldStarted;
    public event Action<PointerEventArgs>? HoldMoved;
    public event Action<PointerReleasedEventArgs>? HoldReleased;
    /// <summary>A hold ended without a release on the place: capture was lost or the places were rebuilt.</summary>
    public event Action? HoldCancelled;
    /// <summary>How long a press must last before it opens Bloom. Shorter presses stay taps.</summary>
    public TimeSpan HoldDelay { get; set; } = TimeSpan.FromMilliseconds(450);
    private HoldGesture gesture = new(TimeSpan.FromMilliseconds(450), slop: 10);
    private readonly DispatcherTimer holdTimer = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    public bool IsHolding => gesture.State == HoldGestureState.Holding;
    private PlaceOrb? pressedOrb;
    private bool suppressClick;
    private bool rebuilding;
    private HomeTopology? pendingHome;

    public HomeConstellation()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<ConstellationPanel>("Map")!.AnchorsChanged += this.FindControl<ConnectionLayer>("Links")!.SetAnchors;
        holdTimer.Tick += (_, _) =>
        {
            if (!IsEffectivelyVisible) { EndHold(cancelled: true); return; }
            if (gesture.Tick(clock.Elapsed)) { holdTimer.Stop(); BeginHold(); }
            else if (gesture.State == HoldGestureState.Pending) { UpdateCharge(); }
            else { holdTimer.Stop(); }
        };
        AttachedToVisualTree += (_, _) => MotionSettings.Current.Changed += MotionChanged;
        DetachedFromVisualTree += (_, _) => { MotionSettings.Current.Changed -= MotionChanged; EndHold(cancelled: true); };
        _ = new PresentationVisibility(this, () => { if (!IsEffectivelyVisible) { EndHold(cancelled: true); } });
    }

    public void SetHome(HomeTopology home)
    {
        this.FindControl<TextBlock>("ConnectionGuide")!.IsVisible = home.Places.Count > 0;
        if (rebuilding) { pendingHome = home; return; }
        var order = DisplayOrder(home.Places).ToArray();
        if (SameLayout(order))
        {
            // Connected refreshes replace the home with new readings every few seconds; keep the same orbs so presses,
            // holds, focus and the Bloom anchor survive.
            foreach (var place in home.Places)
            {
                var existing = orbs[place.Id];
                existing.Apply(place, PlaceIcons.For(place.Icon));
                existing.ShowSummary(PlaceCaption.Caption(place), PlaceCaption.Status(place), PlaceStateIcons.For(place.State), place.Reason);
            }
            this.FindControl<ConnectionLayer>("Links")!.SetConnections(home.Connections);
            return;
        }
        // Rebuilding removes the place being held, so its release would never arrive.
        rebuilding = true;
        try { Rebuild(home, order); }
        finally { rebuilding = false; }
        // Something during the rebuild offered a newer home; draw it now rather than leave older places on screen.
        if (pendingHome is { } newer) { pendingHome = null; SetHome(newer); }
    }

    private void Rebuild(HomeTopology home, IReadOnlyList<(string? Label, Place? Place)> order)
    {
        EndHold(cancelled: true);
        var map = this.FindControl<ConstellationPanel>("Map")!;
        map.Children.Clear();
        orbs.Clear();
        orderedOrbs.Clear();
        zoneLabels.Clear();
        this.FindControl<TextBlock>("EmptyText")!.IsVisible = home.Places.Count == 0;
        foreach (var (zone, place) in order)
        {
            if (zone is not null)
            {
                var label = new TextBlock { Text = zone, Classes = { "zone" } };
                zoneLabels.Add(label);
                map.Children.Add(label);
            }
            if (place is null) { continue; }
            var orb = new PlaceOrb(place, PlaceIcons.For(place.Icon)) { Tag = place.Id };
            orb.Classes.Add("constellation");
            orb.ShowSummary(PlaceCaption.Caption(place), PlaceCaption.Status(place), PlaceStateIcons.For(place.State), place.Reason);
            // The hold-and-slide gesture owns long presses; touch holds must not also open the context menu.
            orb.SetValue(InputElement.IsHoldingEnabledProperty, false);
            // Android can request a native context menu independently of Avalonia's Holding gesture.
            orb.AddHandler(ContextRequestedEvent, (_, args) =>
            {
                if (ReferenceEquals(pressedOrb, orb) && gesture.State is HoldGestureState.Pending or HoldGestureState.Holding)
                { args.Handled = true; }
            }, RoutingStrategies.Tunnel);
            orb.Click += (_, _) =>
            {
                // A hold already opened Bloom; the release that ends it is not also a tap.
                if (suppressClick) { suppressClick = false; return; }
                PlaceInvoked?.Invoke(place.Id);
            };
            orb.AddHandler(PointerPressedEvent, OnOrbPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            orb.AddHandler(PointerMovedEvent, OnOrbMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            orb.AddHandler(PointerReleasedEvent, OnOrbReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            orb.AddHandler(PointerCaptureLostEvent, (_, _) => EndHold(cancelled: true), RoutingStrategies.Direct);
            var readings = new MenuItem { Header = "View readings" };
            readings.Click += (_, _) => PlaceInvoked?.Invoke(place.Id);
            var actions = new MenuItem { Header = "Place actions" };
            actions.Click += (_, _) => ActionsRequested?.Invoke(place.Id);
            var pin = new MenuItem { Header = "Toggle pin" };
            pin.Click += (_, _) => PinRequested?.Invoke(place.Id);
            orb.ContextMenu = new ContextMenu { ItemsSource = new[] { readings, actions, pin } };
            orbs.Add(place.Id, orb);
            orderedOrbs.Add(orb);
            map.Children.Add(orb);
        }

        this.FindControl<ConnectionLayer>("Links")!.SetConnections(home.Connections);
    }

    /// <summary>
    /// Yields a zone label before the first place of each zone, then that zone's places in setup order. Zones appear in the order
    /// their first place does. Places without a zone follow under "Other places" when other places have zones.
    /// </summary>
    private static IEnumerable<(string? Label, Place? Place)> DisplayOrder(IReadOnlyList<Place> places)
    {
        if (places.All(place => place.Zone is null))
        {
            foreach (var place in places) { yield return (null, place); }
            yield break;
        }
        foreach (var group in places.Where(place => place.Zone is not null).GroupBy(place => place.Zone))
        {
            yield return (PlaceZones.Label(group.Key), null);
            foreach (var place in group) { yield return (null, place); }
        }
        var other = places.Where(place => place.Zone is null).ToArray();
        if (other.Length == 0) { yield break; }
        yield return ("Other places", null);
        foreach (var place in other) { yield return (null, place); }
    }

    /// <summary>True when the places, their order and the zone labels are unchanged, so only readings and states differ.</summary>
    private bool SameLayout(IReadOnlyList<(string? Label, Place? Place)> order)
    {
        var labels = order.Where(item => item.Label is not null).Select(item => item.Label!).ToArray();
        var ids = order.Where(item => item.Place is not null).Select(item => item.Place!.Id).ToArray();
        return orderedOrbs.Count > 0 && ids.SequenceEqual(orderedOrbs.Select(orb => (string)orb.Tag!))
            && labels.SequenceEqual(zoneLabels.Select(label => label.Text ?? string.Empty));
    }

    private void OnOrbPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!IsEffectivelyVisible || sender is not PlaceOrb orb || !args.GetCurrentPoint(orb).Properties.IsLeftButtonPressed) { return; }
        EndHold(cancelled: true);
        pressedOrb = orb;
        suppressClick = false;
        var point = args.GetPosition(this);
        gesture = new HoldGesture(HoldDelay < TimeSpan.Zero ? TimeSpan.Zero : HoldDelay, slop: 10);
        gesture.Press(point.X, point.Y, clock.Elapsed);
        if (gesture.Tick(clock.Elapsed)) { BeginHold(); return; }
        UpdateCharge();
        holdTimer.Interval = TimeSpan.FromMilliseconds(16);
        holdTimer.Start();
    }

    private void OnOrbMoved(object? sender, PointerEventArgs args)
    {
        if (gesture.State == HoldGestureState.Holding)
        {
            // On touch, an ancestor scroll viewer would otherwise take the pointer mid-slide and cancel the hold.
            args.PreventGestureRecognition();
            HoldMoved?.Invoke(args);
            args.Handled = true;
            return;
        }
        var point = args.GetPosition(this);
        if (gesture.State == HoldGestureState.Pending && !gesture.Move(point.X, point.Y))
        {
            holdTimer.Stop();
            if (pressedOrb is not null) { pressedOrb.HoldProgress = 0; }
        }
    }

    private void OnOrbReleased(object? sender, PointerReleasedEventArgs args)
    {
        holdTimer.Stop();
        if (pressedOrb is not null) { pressedOrb.HoldProgress = 0; }
        pressedOrb = null;
        if (gesture.Release() != HoldGestureState.Holding) { return; }
        // The orb raises Click after this release only if the pointer is still over it; clear the suppression after that.
        Dispatcher.UIThread.Post(() => suppressClick = false, DispatcherPriority.Input);
        HoldReleased?.Invoke(args);
    }

    private void BeginHold()
    {
        if (pressedOrb?.Tag is not string id) { gesture.Release(); return; }
        pressedOrb.HoldProgress = 0;
        suppressClick = true;
        HoldStarted?.Invoke(id);
    }

    private void EndHold(bool cancelled)
    {
        holdTimer.Stop();
        if (pressedOrb is not null) { pressedOrb.HoldProgress = 0; }
        pressedOrb = null;
        var wasHolding = gesture.Release() == HoldGestureState.Holding;
        if (!wasHolding) { suppressClick = false; return; }
        // A release may still reach the orb after a lost capture; keep it from also counting as a tap until then.
        Dispatcher.UIThread.Post(() => suppressClick = false, DispatcherPriority.Input);
        if (cancelled) { HoldCancelled?.Invoke(); }
    }

    private void UpdateCharge()
    {
        if (pressedOrb is null || gesture.State != HoldGestureState.Pending) { return; }
        pressedOrb.HoldProgress = MotionSettings.Current.IsEnabled ? gesture.Progress(clock.Elapsed) : 1;
    }

    private void MotionChanged()
    {
        if (MotionSettings.Current.IsPaused) { EndHold(cancelled: true); }
        else { UpdateCharge(); }
    }

    public void UpdateSelection(string? selectedId, Func<string, bool> isPinned)
    {
        foreach (var (id, orb) in orbs)
        {
            orb.IsSelected = id == selectedId;
            orb.IsPinned = isPinned(id);
            orb.SetValue(AutomationProperties.HelpTextProperty,
                $"{(orb.IsSelected ? "Selected. " : string.Empty)}{(orb.IsPinned ? "Pinned. " : string.Empty)}Activate for readings. Context menu has place actions.");
        }
    }

    public void FocusPlace(string? id)
    {
        if (id is not null && orbs.TryGetValue(id, out var orb)) { orb.Focus(); }
    }

    public Control? GetPlaceAnchor(string? id) => id is not null && orbs.TryGetValue(id, out var orb)
        ? orb.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.Name == "Orb") ?? orb
        : null;
}
