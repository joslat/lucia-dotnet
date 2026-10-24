using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.Configuration;

namespace Lucia.Companion.App.Controls;

public sealed class ConnectedLuciaNow : UserControl
{
    private readonly TextBlock status = new() { Name = "ActivityStatus", TextWrapping = TextWrapping.Wrap, Focusable = true };
    private readonly TextBlock coverage = new() { Classes = { "muted" }, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13") };
    private readonly Button refresh = new() { Name = "RefreshActivity", Content = "Refresh activity", Classes = { "theme" }, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button connection = new() { Name = "ReviewActivityConnection", Content = "Review connection", Classes = { "theme" }, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel primary = new() { Name = "PrimaryRequests", Spacing = 18 };
    private readonly StackPanel additional = new() { Name = "AdditionalRequests", Spacing = 18, Margin = new Thickness(0, 10, 0, 0) };
    private readonly Expander more;
    private readonly TextBlock moreCaption = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock refreshCaption = new() { Text = "Refresh activity", TextWrapping = TextWrapping.Wrap };
    private int additionalCount;
    private readonly Dictionary<string, LuciaStagePath> rows = new(StringComparer.Ordinal);
    private ConnectedActivityController? controller;
    private Action? openConnection;
    private bool attached;
    public ConnectedLuciaNow()
    {
        more = new() { Name = "MoreActivity", Header = moreCaption, Content = additional, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        more.PropertyChanged += (_, change) => { if (change.Property == Expander.IsExpandedProperty) { UpdateMoreCaption(); } };
        refresh.Content = refreshCaption;
        connection.Content = new TextBlock { Text = "Review connection", TextWrapping = TextWrapping.Wrap };
        SizeChanged += (_, _) => { refresh.MaxWidth = connection.MaxWidth = Math.Max(0, Bounds.Width); };
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock { Text = "Lucia now", Classes = { "section" } });
        content.Children.Add(status); content.Children.Add(primary); content.Children.Add(more); content.Children.Add(coverage);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        actions.Children.Add(refresh); actions.Children.Add(connection); content.Children.Add(actions);
        Content = content;
        status.SetValue(AutomationProperties.LiveSettingProperty, AutomationLiveSetting.Polite);
        refresh.Click += async (_, _) =>
        {
            if (controller is null) { return; }
            var restoreFocus = refresh.IsFocused;
            var pending = controller.RefreshAsync();
            if (restoreFocus && !refresh.IsEnabled) { status.Focus(); }
            await pending;
            if (restoreFocus && (status.IsFocused || refresh.IsFocused || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is null)) { RevealStatus(); }
        };
        connection.Click += (_, _) => openConnection?.Invoke();
        AttachedToVisualTree += (_, _) => { attached = true; if (controller is not null) { controller.Changed += RenderState; } RenderState(); };
        DetachedFromVisualTree += (_, _) => { attached = false; if (controller is not null) { controller.Changed -= RenderState; } };
        RenderState();
    }
    public void SetController(ConnectedActivityController value, Action? reviewConnection = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (attached && controller is not null) { controller.Changed -= RenderState; }
        if (!ReferenceEquals(controller, value))
        { rows.Clear(); primary.Children.Clear(); additional.Children.Clear(); more.IsExpanded = false; }
        controller = value; openConnection = reviewConnection;
        if (attached) { controller.Changed += RenderState; }
        RenderState();
    }
    private void RenderState()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var focusedRequest = focused is not null && (ReferenceEquals(focused, more) || more.IsVisualAncestorOf(focused)
            || primary.IsVisualAncestorOf(focused));
        if (controller is null)
        {
            status.Text = "Activity has not been connected to this view.";
            refresh.IsEnabled = false; connection.IsVisible = more.IsVisible = false;
            return;
        }
        if (!controller.IsBusy || controller.Snapshot is null) { status.Text = controller.StatusText; }
        refresh.IsEnabled = controller.CanRefresh;
        refreshCaption.Text = controller.IsBusy ? "Checking activity" : controller.AccessDenied ? "Check activity access" : "Refresh activity";
        connection.IsVisible = openConnection is not null && (controller.AccessDenied || controller.AuthorityLost || controller.Snapshot is null && !controller.IsBusy);
        if (controller.Snapshot is not { } snapshot)
        {
            primary.Children.Clear(); additional.Children.Clear(); rows.Clear(); more.IsExpanded = false; more.IsVisible = false;
            coverage.Text = "Attributed requests only. Missing data is not proof that nothing is happening.";
            if (focusedRequest) { RevealStatus(); }
            return;
        }
        var retainedIds = snapshot.Requests.Select(item => item.RequestId).ToHashSet(StringComparer.Ordinal);
        foreach (var id in rows.Keys.Where(id => !retainedIds.Contains(id)).ToArray()) { rows.Remove(id); }
        var controls = snapshot.Requests.Select(item =>
        {
            if (!rows.TryGetValue(item.RequestId, out var row)) { rows.Add(item.RequestId, row = new LuciaStagePath()); }
            row.SetRequest(item); return row;
        }).ToArray();
        if (!primary.Children.SequenceEqual(controls.Take(3)) || !additional.Children.SequenceEqual(controls.Skip(3)))
        {
            primary.Children.Clear(); additional.Children.Clear();
            foreach (var row in controls.Take(3)) { primary.Children.Add(row); }
            foreach (var row in controls.Skip(3)) { additional.Children.Add(row); }
        }
        more.IsVisible = controls.Length > 3;
        additionalCount = Math.Max(0, controls.Length - 3); UpdateMoreCaption();
        coverage.Text = !snapshot.IsConfigured ? "No activity source is attributed to this member. Home readings remain separate."
            : $"Captured {snapshot.CapturedAt.UtcDateTime:HH:mm:ss 'UTC'}. Attributed requests only, not all household work. Areas show current target registry assignments, not origin or verified physical results."
                + (snapshot.HasGap ? " Some requests were lost from this host's bounded buffer." : "")
                + (snapshot.HasMore ? " Only 20 retained requests are included in this snapshot." : "");
    }
    private void RevealStatus()
    {
        var current = controller;
        var expectedFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        Dispatcher.UIThread.Post(() =>
        {
            if (!attached || !IsEffectivelyVisible || !ReferenceEquals(controller, current)
                || !ReferenceEquals(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement(), expectedFocus)) { return; }
            status.Focus();
            TopLevel.GetTopLevel(this)?.UpdateLayout();
            // A disabled inner scroller can consume BringIntoView without revealing the
            // target in Home's large-text outer scroller. Address each active viewport.
            foreach (var scroll in status.GetVisualAncestors().OfType<ScrollViewer>()
                .Where(item => item.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled))
            {
                if (scroll.Content is Visual content && status.TranslatePoint(default, content) is { } point)
                { scroll.Offset = new Vector(scroll.Offset.X, point.Y); }
            }
        }, DispatcherPriority.Loaded);
    }
    private void UpdateMoreCaption() => moreCaption.Text = $"{(more.IsExpanded ? "Hide" : "Show")} {additionalCount} more recent requests";
}
