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
    private bool actionsWereOpen;
    private bool detailWasOpen;
    public HomeViewModel State { get; }
    public bool IsExpanded => Bounds.Width >= 900;

    public HomeScreen() : this(new HomeViewModel(SampleHome.Create())) { }

    public HomeScreen(HomeViewModel state)
    {
        State = state;
        AvaloniaXamlLoader.Load(this);
        var constellation = this.FindControl<HomeConstellation>("Constellation")!;
        constellation.PlaceInvoked += State.SelectPlace;
        constellation.ActionsRequested += id => { State.SelectPlace(id); State.CloseDetail(); State.OpenActions(); };
        constellation.PinRequested += id => { State.SelectPlace(id); State.TogglePin(); };
        this.FindControl<AttentionList>("Attention")!.PlaceInvoked += State.SelectPlace;
        this.FindControl<Button>("BackToHome")!.Click += (_, _) => ReturnHome();
        this.FindControl<HomeSectionOrder>("SectionOrderEditor")!.OrderChanged += order => State.SetSectionOrder(order);
        foreach (var name in new[] { "DesktopDetail", "PhoneDetail" })
        {
            this.FindControl<PlaceDetail>(name)!.ActionsRequested += State.OpenActions;
        }

        var actions = this.FindControl<BloomActions>("PlaceActions")!;
        actions.PinRequested += State.TogglePin;
        actions.CloseRequested += CloseActions;
        actions.ReadingsRequested += () => { State.CloseActions(); if (State.SelectedPlace is { } selected) { State.SelectPlace(selected.Id); } };
        var popup = this.FindControl<Popup>("ActionPopup")!;
        popup.Opened += (_, _) => Dispatcher.UIThread.Post(actions.FocusFirstAction, DispatcherPriority.Loaded);
        popup.Closed += (_, _) => { if (State.IsActionsOpen) { CloseActions(); } };

        AttachedToVisualTree += (_, _) => { State.PropertyChanged += StateChanged; Refresh(); };
        DetachedFromVisualTree += (_, _) => { State.PropertyChanged -= StateChanged; popup.IsOpen = false; };
        SizeChanged += (_, _) => Refresh();
        this.FindControl<TextBlock>("HomeName")!.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBlock.FontSizeProperty) { Refresh(); }
        };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
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
            constellation.SetHome(State.Home);
            renderedHome = State.Home;
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
            if (homeChanged || !ReferenceEquals(renderedPlace, place))
            {
                this.FindControl<PlaceDetail>("DesktopDetail")!.SetPlace(place, State.Home);
                this.FindControl<PlaceDetail>("PhoneDetail")!.SetPlace(place, State.Home);
                renderedPlace = place;
            }

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
        this.FindControl<ScrollViewer>("HomeScroll")!.IsVisible = IsExpanded || !State.IsDetailOpen;
        this.FindControl<ScrollViewer>("DetailScroll")!.IsVisible = !IsExpanded && State.IsDetailOpen;
        var popup = this.FindControl<Popup>("ActionPopup")!;
        this.FindControl<Border>("BloomShade")!.IsVisible = State.IsActionsOpen;
        if (!State.IsActionsOpen) { popup.IsOpen = false; }
        if (State.IsActionsOpen && !actionsWereOpen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (State.IsActionsOpen)
                {
                    popup.PlacementTarget = !IsExpanded && State.IsDetailOpen
                        ? this.FindControl<PlaceDetail>("PhoneDetail")!.GetPlaceAnchor()
                        : constellation.GetPlaceAnchor(State.SelectedPlace?.Id);
                    if (popup.PlacementTarget is not null && TopLevel.GetTopLevel(this) is not null) { popup.IsOpen = true; }
                }
            }, DispatcherPriority.Loaded);
        }
        else if (!IsExpanded && State.IsDetailOpen && !detailWasOpen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsExpanded && State.IsDetailOpen && !State.IsActionsOpen) { this.FindControl<Button>("BackToHome")!.Focus(); }
            }, DispatcherPriority.Loaded);
        }

        actionsWereOpen = State.IsActionsOpen;
        detailWasOpen = State.IsDetailOpen;
    }

    private void RefreshHeader()
    {
        var attention = State.AttentionPlaces;
        this.FindControl<TextBlock>("AttentionHeadline")!.Text = HomeAttention.Headline(attention.Count);
        this.FindControl<TextBlock>("AttentionSummary")!.Text = HomeAttention.Subline(attention.FirstOrDefault()?.Name, attention.Count, State.Home.Places.Count, State.IsDemo);
        var date = this.FindControl<TextBlock>("HomeDate")!;
        date.IsVisible = State.ReportTime is not null;
        date.Text = State.ReportTime is { } time ? time.UtcDateTime.ToString("dddd d MMM · HH:mm 'UTC'", CultureInfo.InvariantCulture) : null;
        var live = !State.IsDemo && State.ReportTime is not null;
        var label = this.FindControl<TextBlock>("HomeModeLabel")!;
        label.Text = State.IsDemo ? "Offline demo" : live ? "Live" : "Connected";
        label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(State.IsDemo ? "AccentBrush" : live ? "SageBrush" : "MutedBrush");
        this.FindControl<Ellipse>("LiveDot")!.IsVisible = live;
        AutomationProperties.SetName(this.FindControl<Border>("DemoBadge")!, State.IsDemo ? "Offline demo, synthetic readings" : live ? $"Live reports, read {date.Text}" : "Connected, no reports yet");
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
        var notice = this.FindControl<TextBlock>("SourceNotice")!;
        var divider = this.FindControl<Border>("HeaderDivider")!;
        var title = this.FindControl<TextBlock>("AttentionHeadline")!;
        var expanded = IsExpanded;
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
        var stackBadge = Bounds.Width < 600 && name.FontSize > 32;
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
        Control[] mainOrder = IsExpanded ? [sections[0], secondaryMain] : sections;
        Control[] sideOrder = IsExpanded ? [sections.Single(section => !mainOrder.Contains(section)), detail] : [detail];
        if (main.Children.SequenceEqual(mainOrder) && side.Children.SequenceEqual(sideOrder)) { return; }
        main.Children.Clear();
        side.Children.Clear();
        main.Children.AddRange(mainOrder);
        side.Children.AddRange(sideOrder);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
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
        if (!IsExpanded && State.IsDetailOpen) { ReturnHome(); return true; }
        var customize = this.FindControl<Expander>("CustomizeHome")!;
        if (customize.IsExpanded) { customize.IsExpanded = false; customize.Focus(); return true; }
        return false;
    }

    private void CloseActions()
    {
        State.CloseActions();
        if (!IsExpanded && State.IsDetailOpen) { this.FindControl<PlaceDetail>("PhoneDetail")!.FocusActions(); }
        else { this.FindControl<HomeConstellation>("Constellation")!.FocusPlace(State.SelectedPlace?.Id); }
    }

    private void ReturnHome()
    {
        State.CloseDetail();
        this.FindControl<HomeConstellation>("Constellation")!.FocusPlace(State.SelectedPlace?.Id);
    }
}
