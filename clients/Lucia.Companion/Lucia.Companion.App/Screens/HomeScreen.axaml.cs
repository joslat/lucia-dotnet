using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using System.Globalization;

namespace Lucia.Companion.App.Screens;

/// <summary>Composes controls and adapts layout. Household selection and preferences belong to the shared view model.</summary>
public sealed partial class HomeScreen : UserControl
{
    private HomeTopology? renderedHome;
    private Place? renderedPlace;
    private DateTimeOffset? renderedReportTime;
    private DateTimeOffset? renderedPageReportTime;
    private (bool Expanded, bool StackBadge, bool InlineReview)? headerLayout;
    private bool actionsWereOpen;
    private bool detailWasOpen;
    private bool pointerInput;
    private Control? pageInvoker;
    private Vector overviewOffset;
    private int pageNavigationVersion;
    public HomeViewModel State { get; }
    public bool IsExpanded => Bounds.Width >= 900;
    /// <summary>Phone shows the place page whenever detail is open; tablet only after the side pane's Detail.</summary>
    public bool IsPageVisible => IsExpanded ? State.IsPageOpen : State.IsDetailOpen;
    /// <summary>For the shell to open Story on today: null for the whole home (Today so far, Whole home story), a place ID for Bloom's History.</summary>
    public event Action<string?>? StoryRequested;
    public event Action? SetupRequested;
    public void SetSetupAvailable(bool available) => this.FindControl<Button>("OpenEmptySetup")!.IsEnabled = available;
    public void SetSaveFeedback(string? text)
    {
        var feedback = this.FindControl<TextBlock>("HomeSaveFeedback")!;
        feedback.Text = text;
        feedback.IsVisible = !string.IsNullOrWhiteSpace(text);
        var pageFeedback = this.FindControl<PlacePage>("PlacePage")!.FindControl<TextBlock>("PageSaveFeedback")!;
        pageFeedback.Text = text;
        pageFeedback.IsVisible = feedback.IsVisible;
    }

    public HomeScreen() : this(new HomeViewModel(SampleHome.Create())) { }

    public HomeScreen(HomeViewModel state)
    {
        State = state;
        AvaloniaXamlLoader.Load(this);
        var constellation = this.FindControl<HomeConstellation>("Constellation")!;
        constellation.PlaceInvoked += id => { RememberPlaceInvoker(id); State.SelectPlace(id); };
        constellation.ActionsRequested += id => { RememberPlaceInvoker(id); State.OpenActionsFor(id); };
        constellation.PinRequested += id => { RememberPlaceInvoker(id); State.SelectPlace(id); State.TogglePin(); };
        var attention = this.FindControl<AttentionList>("Attention")!;
        attention.PlaceInvoked += id => { pageInvoker = attention.Rows.FirstOrDefault(row => Equals(row.Tag, id)); State.SelectPlace(id); };
        this.FindControl<Button>("OpenEmptySetup")!.Click += (_, _) => SetupRequested?.Invoke();
        this.FindControl<Button>("ReviewAttention")!.Click += (_, _) =>
        {
            var list = this.FindControl<AttentionList>("Attention")!;
            if (list.Rows.FirstOrDefault() is { } first) { first.BringIntoView(); first.Focus(); }
        };
        ContextHelp.Set(this.FindControl<Button>("ReviewAttention")!, "Go to Needs you and review the reports, most urgent first.");
        var page = this.FindControl<PlacePage>("PlacePage")!;
        page.BackRequested += () => { if (IsExpanded) { ClosePage(); } else { ReturnHome(); } };
        page.PinToggled += State.TogglePin;
        page.ActionsRequested += State.OpenActions;
        page.StoryRequested += () => StoryRequested?.Invoke(null);
        this.FindControl<TodaySoFar>("TodaySoFar")!.StoryRequested += () => StoryRequested?.Invoke(null);
        var detail = this.FindControl<PlaceDetail>("DesktopDetail")!;
        detail.PageRequested += () => { pageInvoker = detail.FindControl<Button>("OpenPlacePage"); State.OpenPage(); };
        this.FindControl<HomeSectionOrder>("SectionOrderEditor")!.OrderChanged += order => State.SetSectionOrder(order);
        detail.ActionsRequested += () => { pageInvoker = detail.FindControl<Button>("ActionsButton"); State.OpenActions(); };

        var actions = this.FindControl<BloomActions>("PlaceActions")!;
        actions.ActionRequested += action => Invoke(action, fromSlide: false);
        constellation.HoldStarted += id => { RememberPlaceInvoker(id); pointerInput = true; actions.SetHoldActive(true); State.OpenActionsFor(id); };
        constellation.HoldMoved += args =>
        {
            if (this.FindControl<Popup>("ActionPopup")!.IsOpen) { actions.Arm(actions.ActionAt(args.GetPosition(actions))); }
        };
        constellation.HoldCancelled += () => { actions.SetHoldActive(false); actions.Arm(null); };
        constellation.HoldReleased += args =>
        {
            actions.SetHoldActive(false);
            actions.Arm(null);
            if (!State.IsActionsOpen) { return; }
            var point = args.GetPosition(actions);
            if (actions.ActionAt(point) is { } action) { Invoke(action, fromSlide: true); }
            else if (actions.IsOutside(point)) { CloseActions(); }
        };
        var popup = this.FindControl<Popup>("ActionPopup")!;
        popup.Opened += (_, _) => Dispatcher.UIThread.Post(actions.FocusFirstAction, DispatcherPriority.Loaded);
        popup.Closed += (_, _) => { if (State.IsActionsOpen) { CloseActions(); } };
        _ = new PresentationVisibility(this, () =>
        {
            if (IsEffectivelyVisible) { return; }
            if (State.IsActionsOpen) { State.CloseActions(); }
            popup.IsOpen = false;
            this.FindControl<Border>("BloomShade")!.IsVisible = false;
        });

        AttachedToVisualTree += (_, _) => { State.PropertyChanged += StateChanged; Refresh(); };
        DetachedFromVisualTree += (_, _) => { State.PropertyChanged -= StateChanged; popup.IsOpen = false; };
        SizeChanged += (_, _) => Refresh();
        this.FindControl<TextBlock>("HomeName")!.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBlock.FontSizeProperty) { Refresh(); }
        };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => pointerInput = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        if (State.SelectedPlace is null && State.Home.Places.Count > 0)
        {
            State.SelectPlace((State.AttentionPlaces.FirstOrDefault() ?? State.Home.Places[0]).Id);
            State.CloseDetail();
        }

        Refresh();
    }

    private void StateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        this.FindControl<TextBlock>("SourceNotice")!.Text = State.SourceNotice;
        this.FindControl<LuciaNow>("DemoLuciaNow")!.IsVisible = State.IsDemo;
        this.FindControl<ConnectedLuciaNow>("ConnectedLuciaNotice")!.IsVisible = !State.IsDemo;
        ArrangeContextForTextSize();
        ArrangeHeader();
        var constellation = this.FindControl<HomeConstellation>("Constellation")!;
        var homeChanged = !ReferenceEquals(renderedHome, State.Home);
        if (homeChanged)
        {
            this.FindControl<TextBlock>("HomeName")!.Text = State.Home.Name;
            // Record first: rebuilding can raise events that refresh again, which must not rebuild a second time.
            renderedHome = State.Home;
            constellation.SetHome(State.Home);
            this.FindControl<Button>("OpenEmptySetup")!.IsVisible = State.Home.Places.Count == 0;
            this.FindControl<TextBlock>("PlacesInstruction")!.IsVisible = State.Home.Places.Count > 0;
            // Today so far needs history; only the demo has a (sample) day to show.
            this.FindControl<Border>("TodayCard")!.IsVisible = State.IsDemo;
            if (State.IsDemo) { this.FindControl<TodaySoFar>("TodaySoFar")!.SetDay(SampleStory.Day(State.Home, SampleStory.Today)); }
        }
        if (homeChanged || renderedReportTime != State.ReportTime)
        {
            RefreshHeader();
            this.FindControl<AttentionList>("Attention")!.SetPlaces(State.AttentionPlaces, State.IsDemo, State.ReportTime);
            renderedReportTime = State.ReportTime;
        }

        constellation.UpdateSelection(State.SelectedPlace?.Id, State.IsPinned);
        if (State.SelectedPlace is { } place)
        {
            if (homeChanged || !ReferenceEquals(renderedPlace, place) || renderedPageReportTime != State.ReportTime)
            {
                this.FindControl<PlaceDetail>("DesktopDetail")!.SetPlace(place, State.Home);
                this.FindControl<PlacePage>("PlacePage")!.SetPlace(place, State.Home, State.IsDemo, State.ReportTime);
                renderedPlace = place;
                renderedPageReportTime = State.ReportTime;
            }
            this.FindControl<PlacePage>("PlacePage")!.SetPinned(State.IsPinned(place.Id));

            this.FindControl<BloomActions>("PlaceActions")!.SetPlace(place, State.IsPinned(place.Id));
        }

        var grid = this.FindControl<Grid>("HomeGrid")!;
        grid.ColumnDefinitions[1].Width = IsExpanded ? new GridLength(340) : new GridLength(0);
        grid.ColumnSpacing = IsExpanded ? 16 : 0;
        var side = this.FindControl<StackPanel>("SidePanel")!;
        var main = this.FindControl<StackPanel>("MainPanel")!;
        ArrangeSections(main, side);
        this.FindControl<HomeSectionOrder>("SectionOrderEditor")!.SetOrder(State.SectionOrder);
        side.IsVisible = IsExpanded;
        this.FindControl<Border>("DesktopDetailCard")!.IsVisible = IsExpanded && State.SelectedPlace is not null;
        var pageOpen = IsPageVisible;
        if (pageOpen != detailWasOpen)
        {
            pageNavigationVersion++;
            if (pageOpen)
            {
                overviewOffset = this.FindControl<ScrollViewer>("ContextScroll")!.Offset;
                this.FindControl<ScrollViewer>("ContextScroll")!.Offset = default;
            }
            else { RestoreOverview(restoreFocus: false); }
        }
        this.FindControl<ScrollViewer>("HomeScroll")!.IsVisible = !pageOpen;
        this.FindControl<ScrollViewer>("DetailScroll")!.IsVisible = pageOpen;
        this.FindControl<Grid>("HomeContext")!.IsVisible = !pageOpen;
        var popup = this.FindControl<Popup>("ActionPopup")!;
        var actions = this.FindControl<BloomActions>("PlaceActions")!;
        this.FindControl<Border>("BloomShade")!.IsVisible = State.IsActionsOpen || popup.IsOpen;
        if (!State.IsActionsOpen && popup.IsOpen && !actions.IsClosing)
        {
            actions.PlayClosing(MotionSettings.Current.IsEnabled, () =>
            {
                if (State.IsActionsOpen) { return; }
                popup.IsOpen = false;
                this.FindControl<Border>("BloomShade")!.IsVisible = false;
            });
        }
        if (State.IsActionsOpen && actionsWereOpen && homeChanged && popup.IsOpen)
        {
            // A layout change rebuilt the places; move Bloom to the new control for its place.
            popup.PlacementTarget = IsPageVisible
                ? this.FindControl<PlacePage>("PlacePage")!.GetPlaceAnchor()
                : constellation.GetPlaceAnchor(State.SelectedPlace?.Id);
        }
        if (State.IsActionsOpen && !actionsWereOpen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (State.IsActionsOpen)
                {
                    popup.PlacementTarget = IsPageVisible
                        ? this.FindControl<PlacePage>("PlacePage")!.GetPlaceAnchor()
                        : constellation.GetPlaceAnchor(State.SelectedPlace?.Id);
                    if (IsEffectivelyVisible && popup.PlacementTarget is not null && TopLevel.GetTopLevel(this) is not null)
                    {
                        actions.BeginSession(pointerInput, constellation.IsHolding);
                        actions.PlayOpening(MotionSettings.Current.IsEnabled);
                        popup.IsOpen = true;
                    }
                }
            }, DispatcherPriority.Loaded);
        }
        else if (pageOpen && !detailWasOpen)
        {
            var version = pageNavigationVersion;
            this.FindControl<ScrollViewer>("DetailScroll")!.Offset = default;
            Dispatcher.UIThread.Post(() =>
            {
                if (version == pageNavigationVersion && IsEffectivelyVisible && IsPageVisible && !State.IsActionsOpen)
                {
                    this.FindControl<ScrollViewer>("ContextScroll")!.Offset = default;
                    this.FindControl<PlacePage>("PlacePage")!.FocusBack();
                }
            }, DispatcherPriority.Loaded);
        }

        actionsWereOpen = State.IsActionsOpen;
        detailWasOpen = pageOpen;
    }

    private void RefreshHeader()
    {
        var attention = State.AttentionPlaces;
        this.FindControl<TextBlock>("AttentionHeadline")!.Text = State.Home.Places.Count == 0 ? "Set up your home" : HomeAttention.Headline(attention.Count);
        var review = this.FindControl<Button>("ReviewAttention")!;
        review.IsVisible = attention.Count > 0;
        AutomationProperties.SetName(review, $"Review Needs you, {attention.Count} {(attention.Count == 1 ? "place" : "places")}");
        this.FindControl<TextBlock>("AttentionSummary")!.Text = attention.FirstOrDefault() is { } leading
            ? $"{leading.Name}. {leading.Reason}"
            : HomeAttention.Subline(null, 0, State.Home.Places.Count, State.IsDemo, State.Home.Places.Count(place => place.State == PlaceState.Quiet));
        var date = this.FindControl<TextBlock>("HomeDate")!;
        date.IsVisible = State.ReportTime is not null;
        date.Text = State.ReportTime is { } time ? time.UtcDateTime.ToString("dddd d MMM · HH:mm 'UTC'", CultureInfo.InvariantCulture) : null;
        var hasReports = !State.IsDemo && State.ReportTime is not null;
        // Stale only when sources are bound and none of their readings is current; a home with no bindings yet is not stale.
        var live = hasReports && (State.Home.Bindings.Count == 0 || State.Home.Places.Any(place => place.Observations.Any(item => item.IsCurrent)));
        var label = this.FindControl<TextBlock>("HomeModeLabel")!;
        label.Text = State.IsDemo ? "Offline demo" : live ? "Live" : hasReports ? "Reports stale" : "Connected";
        label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(State.IsDemo ? "AccentBrush" : live ? "SageBrush" : "MutedBrush");
        this.FindControl<Ellipse>("LiveDot")!.IsVisible = live;
        AutomationProperties.SetName(this.FindControl<Border>("DemoBadge")!, State.IsDemo ? "Offline demo, synthetic readings" : live ? $"Live reports, read {date.Text}" : hasReports ? $"Reports read {date.Text} are no longer current" : "Connected, no reports yet");
    }

    /// <summary>Phone stacks the header; tablet puts the date and name, a divider, the headline and the mode chip on one line.</summary>
    private void ArrangeHeader()
    {
        var context = this.FindControl<Grid>("HomeContext")!;
        var identity = this.FindControl<StackPanel>("HomeIdentity")!;
        var headline = this.FindControl<StackPanel>("HeadlineBlock")!;
        var date = this.FindControl<TextBlock>("HomeDate")!;
        var name = this.FindControl<TextBlock>("HomeName")!;
        var badge = this.FindControl<Border>("DemoBadge")!;
        var notice = this.FindControl<StackPanel>("SourceContext")!;
        var divider = this.FindControl<Border>("HeaderDivider")!;
        var title = this.FindControl<TextBlock>("AttentionHeadline")!;
        var expanded = IsExpanded;
        var stacked = !expanded && Bounds.Width < 600 && name.FontSize > 32;
        var inlineReview = expanded && name.FontSize <= 32;
        if (headerLayout == (expanded, stacked, inlineReview)) { return; }
        headerLayout = (expanded, stacked, inlineReview);
        var headlineContent = this.FindControl<Grid>("HeadlineContent")!;
        headlineContent.ColumnDefinitions = new ColumnDefinitions(inlineReview ? "*,Auto" : "*");
        headlineContent.RowDefinitions = new RowDefinitions(inlineReview ? "Auto,Auto" : "Auto,Auto,Auto");
        var review = this.FindControl<Button>("ReviewAttention")!;
        Grid.SetRow(review, inlineReview ? 0 : 2);
        Grid.SetColumn(review, inlineReview ? 1 : 0);
        Grid.SetRowSpan(review, inlineReview ? 2 : 1);
        var dateHome = expanded ? identity : headline;
        if (!ReferenceEquals(date.Parent, dateHome))
        {
            ((Panel)date.Parent!).Children.Remove(date);
            dateHome.Children.Insert(0, date);
        }
        title[!TextBlock.FontSizeProperty] = new DynamicResourceExtension(expanded ? "TextSize24" : "TextSize34");
        divider.IsVisible = expanded;
        context.ColumnDefinitions = new ColumnDefinitions(expanded ? "Auto,Auto,*,Auto" : "*,Auto");
        context.RowDefinitions = new RowDefinitions(expanded ? "Auto,Auto" : "Auto,Auto,Auto,Auto");
        identity.MaxWidth = expanded ? 380 : double.PositiveInfinity;
        if (expanded)
        {
            Place(identity, 0, 0); Place(divider, 0, 1); Place(headline, 0, 2); Place(badge, 0, 3); Place(notice, 1, 0, 4);
            badge.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            return;
        }
        // At large text on a narrow screen the chip moves under the name so the name can use the full width.
        var stackBadge = stacked;
        Place(identity, 0, 0, stackBadge ? 2 : 1);
        Place(badge, stackBadge ? 1 : 0, stackBadge ? 0 : 1);
        badge.HorizontalAlignment = stackBadge ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Right;
        Place(headline, stackBadge ? 2 : 1, 0, 2);
        Place(notice, stackBadge ? 3 : 2, 0, 2);
    }

    private static void Place(Control control, int row, int column, int columnSpan = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, columnSpan);
    }

    private void ArrangeContextForTextSize()
    {
        // Phone widths scroll the header away with the content, as in the Home artboard; large text does too at any width.
        var scrolling = this.FindControl<TextBlock>("HomeName")!.FontSize > 32 || !IsExpanded;
        this.FindControl<ScrollViewer>("ContextScroll")!.VerticalScrollBarVisibility = scrolling ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        foreach (var name in new[] { "HomeScroll", "DetailScroll" })
        {
            this.FindControl<ScrollViewer>(name)!.VerticalScrollBarVisibility = scrolling ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        }
    }

    private void ArrangeSections(StackPanel main, StackPanel side)
    {
        var sections = State.SectionOrder.Select(section => this.FindControl<Border>(section switch
        {
            HomeSection.Places => "PlacesCard", HomeSection.Attention => "AttentionCard", _ => "LuciaCard"
        })!).ToArray();
        var detail = this.FindControl<Border>("DesktopDetailCard")!;
        // Keep the home graph wide even when another section is promoted above it.
        var places = this.FindControl<Border>("PlacesCard")!;
        var secondaryMain = ReferenceEquals(sections[0], places) ? sections[2] : places;
        var today = this.FindControl<Border>("TodayCard")!;
        // Phone: Today so far follows Your places. Tablet: it sits in the side column under the side section, as in the artboard.
        Control[] mainOrder = IsExpanded ? [sections[0], secondaryMain]
            : sections.SelectMany(section => ReferenceEquals(section, places) ? new Control[] { section, today } : [section]).ToArray();
        Control[] sideOrder = IsExpanded ? [sections.Single(section => !mainOrder.Contains(section)), today, detail] : [detail];
        if (main.Children.SequenceEqual(mainOrder) && side.Children.SequenceEqual(sideOrder)) { return; }
        main.Children.Clear();
        side.Children.Clear();
        main.Children.AddRange(mainOrder);
        side.Children.AddRange(sideOrder);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        pointerInput = false;
        if (args.Key != Key.Escape) { return; }
        args.Handled = TryNavigateBack();
    }

    public void OpenCustomization()
    {
        State.CloseDetail();
        var customize = this.FindControl<Expander>("CustomizeHome")!;
        customize.IsExpanded = true;
        Dispatcher.UIThread.Post(() => { customize.BringIntoView(); customize.Focus(); }, DispatcherPriority.Loaded);
    }

    public bool TryNavigateBack()
    {
        if (State.IsActionsOpen) { CloseActions(); return true; }
        if (IsPageVisible) { if (IsExpanded) { ClosePage(); } else { ReturnHome(); } return true; }
        var customize = this.FindControl<Expander>("CustomizeHome")!;
        if (customize.IsExpanded) { customize.IsExpanded = false; customize.Focus(); return true; }
        return false;
    }

    /// <summary>Runs a Bloom action. Pin from a button keeps Bloom open to show the new label; every slide closes it.</summary>
    private void Invoke(BloomAction action, bool fromSlide)
    {
        if (State.SelectedPlace is not { } place) { return; }
        switch (action)
        {
            case BloomAction.Open:
                State.CloseActions();
                State.OpenPage();
                break;
            case BloomAction.History:
                State.CloseActions();
                StoryRequested?.Invoke(place.Id);
                break;
            case BloomAction.Pin:
                State.TogglePin();
                if (fromSlide) { CloseActions(); }
                break;
            default:
                CloseActions();
                break;
        }
    }

    private void CloseActions()
    {
        State.CloseActions();
        if (IsPageVisible) { this.FindControl<PlacePage>("PlacePage")!.FocusActions(); }
        else { this.FindControl<HomeConstellation>("Constellation")!.FocusPlace(State.SelectedPlace?.Id); }
    }

    private void ClosePage()
    {
        State.ClosePage();
        RestoreOverview(restoreFocus: true);
    }

    private void ReturnHome()
    {
        State.CloseDetail();
        RestoreOverview(restoreFocus: true);
    }

    private void RememberPlaceInvoker(string id) => pageInvoker = this.FindControl<HomeConstellation>("Constellation")!.Orbs.FirstOrDefault(orb => Equals(orb.Tag, id));

    private void RestoreOverview(bool restoreFocus)
    {
        var version = ++pageNavigationVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (version != pageNavigationVersion || IsPageVisible || !IsEffectivelyVisible) { return; }
            this.FindControl<ScrollViewer>("ContextScroll")!.Offset = overviewOffset;
            if (!restoreFocus) { return; }
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            if (focused is not null && !this.IsVisualAncestorOf(focused)) { return; }
            if (pageInvoker is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } && this.IsVisualAncestorOf(pageInvoker))
            { pageInvoker.Focus(); pageInvoker.BringIntoView(); }
            else if (IsExpanded) { this.FindControl<PlaceDetail>("DesktopDetail")!.FocusActions(); }
            else { this.FindControl<HomeConstellation>("Constellation")!.FocusPlace(State.SelectedPlace?.Id); }
        }, DispatcherPriority.Loaded);
    }
}
