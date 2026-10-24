using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Screens;

/// <summary>Composes controls and adapts layout. Household selection and preferences belong to the shared view model.</summary>
public sealed partial class HomeScreen : UserControl
{
    private HomeTopology? renderedHome;
    private Place? renderedPlace;
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
        var constellation = this.FindControl<HomeConstellation>("Constellation")!;
        var homeChanged = !ReferenceEquals(renderedHome, State.Home);
        if (homeChanged)
        {
            this.FindControl<TextBlock>("HomeName")!.Text = State.Home.Name;
            this.FindControl<TextBlock>("AttentionSummary")!.Text = State.AttentionPlaces.Count == 0
                ? "No places need a look in this sample."
                : $"{State.AttentionPlaces.Count} places need a look · {State.AttentionPlaces[0].Name} first";
            constellation.SetHome(State.Home);
            this.FindControl<AttentionList>("Attention")!.SetPlaces(State.AttentionPlaces);
            renderedHome = State.Home;
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
        var attention = this.FindControl<Border>("AttentionCard")!;
        if (IsExpanded && !ReferenceEquals(attention.Parent, side)) { main.Children.Remove(attention); side.Children.Insert(0, attention); }
        else if (!IsExpanded && !ReferenceEquals(attention.Parent, main)) { side.Children.Remove(attention); main.Children.Insert(1, attention); }
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

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape) { return; }
        if (State.IsActionsOpen) { CloseActions(); args.Handled = true; }
        else if (!IsExpanded && State.IsDetailOpen) { ReturnHome(); args.Handled = true; }
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
