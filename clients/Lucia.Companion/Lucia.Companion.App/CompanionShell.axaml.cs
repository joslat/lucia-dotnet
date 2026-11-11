using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;

namespace Lucia.Companion.App;

/// <summary>
/// The app frame: four destinations (Home, Story, Lucia, More), the local save status and the screen they show.
/// Setup and save-conflict review are full-screen tasks, so the navigation steps aside while they are open.
/// </summary>
public sealed partial class CompanionShell : UserControl
{
    /// <summary>Minimum width for considering a rail; its measured height must also fit.</summary>
    public const double RailWidthThreshold = 600;

    private readonly HomeScreen home;
    private readonly HomeProfileSession profileSession;
    private ControlWorkbench? workbench;
    private StoryScreen? story;
    private Core.HomeTopology? storyHome;
    private MoreScreen? more;
    private LuciaScreen? lucia;
    private HistoryPendingScreen? historyPending;
    private ConnectedActivityController? activity;
    private TopLevel? attachedTopLevel;
    private HomeCatalog? importCatalog;
    private bool refreshing;
    private Control? setupInvoker;
    private long observedConfirmation;
    private bool navigationLayoutQueued;
    private int navigationVersion;
    private static readonly TimeSpan SaveNoticeDelay = TimeSpan.FromMilliseconds(150);
    private readonly DispatcherTimer saveNoticeTimer = new();
    private readonly System.Diagnostics.Stopwatch saveNoticeClock = new();
    private bool wasSaving;
    private bool saveNoticeReady;
    private readonly CompanionCrossFade pageTransition = new(TimeSpan.FromMilliseconds(180));
    private readonly Func<Func<Core.HomeTopology>, SharedLayoutAdoptionController>? sharedLayoutFactory;
    public HomeProfileSession ProfileSession => profileSession;
    public ShellNavigation Navigation => this.FindControl<ShellNavigation>("NavigationBar")!;
    public object? CurrentScreen => Host.Content;
    private ContentControl Host => this.FindControl<ContentControl>("ScreenHost")!;
    public event Action? ConnectionRequested;
    public event Action? RefreshRequested;
    public void RequestConnection() => ConnectionRequested?.Invoke();

    public void BindActivity(ConnectedActivityController controller)
    {
        activity = controller;
        home.FindControl<ConnectedLuciaNow>("ConnectedLuciaNotice")!.SetController(controller, RequestConnection);
        lucia?.SetController(controller, RequestConnection);
    }

    public CompanionShell() : this(new HomeViewModel(SampleHome.Create(), SampleObservationSources.Create()), new LocalHomeStore(LocalHomeStore.DefaultPath)) { }

    public CompanionShell(HomeViewModel state, LocalHomeStore? store, ICompanionConnection? connection = null, ConnectionSession? identity = null)
    {
        home = new HomeScreen(state);
        profileSession = new(state, store);
        if (!state.IsDemo && connection is not null && identity is not null)
        { sharedLayoutFactory = draft => new(connection, identity, draft); }
        AvaloniaXamlLoader.Load(this);
        Host.PropertyChanged += (_, args) => { if (args.Property == ContentControl.ContentProperty) { navigationVersion++; } };
        Host.Content = home;
        Navigation.DestinationRequested += Navigate;
        Navigation.TextMetricsChanged += RequestNavigationLayout;
        Navigation.PropertyChanged += (_, args) => { if (args.Property == IsVisibleProperty) { RequestNavigationLayout(); } };
        home.StoryRequested += OpenStoryFor;
        home.SetupRequested += OpenEditor;
        this.FindControl<Button>("RetrySaveButton")!.Click += (_, _) => profileSession.RetrySave();
        this.FindControl<Button>("ReviewSaveConflict")!.Click += async (_, _) => await OpenSaveConflictAsync();
        this.FindControl<Button>("ReviewLocalSetup")!.Click += (_, _) => OpenEditor();
        this.FindControl<ToggleButton>("StorageDetailsToggle")!.IsCheckedChanged += (_, _) =>
            SetStorageDetails(this.FindControl<ToggleButton>("StorageDetailsToggle")!.IsChecked == true);
        saveNoticeTimer.Tick += (_, _) => RefreshStorageState();
        profileSession.Changed += RefreshStorageState;
        SizeChanged += (_, _) => { ArrangeNavigation(); RefreshStorageState(); };
        ApplyMotion();
        AttachedToVisualTree += async (_, _) =>
        {
            attachedTopLevel = TopLevel.GetTopLevel(this);
            if (attachedTopLevel is not null) { attachedTopLevel.BackRequested += OnBackRequested; }
            MotionSettings.Current.Changed += ApplyMotion;
            ApplyMotion();
            ArrangeNavigation();
            await profileSession.InitializeAsync();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (attachedTopLevel is not null) { attachedTopLevel.BackRequested -= OnBackRequested; }
            attachedTopLevel = null;
            MotionSettings.Current.Changed -= ApplyMotion;
            pageTransition.Stop();
            saveNoticeTimer.Stop();
        };
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape && !args.Handled) { args.Handled = NavigateBack(); }
        };
        ArrangeNavigation();
        RefreshStorageState();
        _ = new PresentationVisibility(this, () => { RefreshStorageState(); RequestNavigationLayout(); });
    }

    public void Navigate(ShellDestination destination)
    {
        navigationVersion++;
        SetStorageDetails(false);
        home.SetSaveFeedback(null);
        switch (destination)
        {
            case ShellDestination.Home: ShowHome(); return;
            case ShellDestination.Story: ShowStory(); break;
            case ShellDestination.Lucia: Host.Content = LuciaDestination(); break;
            case ShellDestination.More: Host.Content = MoreDestination(); break;
        }
        Navigation.Select(destination);
        RefreshStorageState();
    }

    /// <summary>Destinations cross-fade in 180 ms; with reduced motion or in the background they switch at once.</summary>
    private void ApplyMotion()
    {
        if (!MotionSettings.Current.IsEnabled) { pageTransition.Stop(); }
        ((TransitioningContentControl)Host).PageTransition = MotionSettings.Current.IsEnabled ? pageTransition : null;
        RefreshStorageState();
    }

    private void RequestNavigationLayout()
    {
        if (navigationLayoutQueued) { return; }
        navigationLayoutQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            navigationLayoutQueued = false;
            if (TopLevel.GetTopLevel(this) is not null && IsEffectivelyVisible) { ArrangeNavigation(); }
        }, DispatcherPriority.Loaded);
    }

    private void ArrangeNavigation()
    {
        if (!Navigation.IsVisible) { return; }
        var rail = Bounds.Width >= RailWidthThreshold;
        if (rail)
        {
            Navigation.SetRail(true);
            Navigation.Measure(new Size(Bounds.Width, double.PositiveInfinity));
            rail = Navigation.DesiredSize.Height <= Bounds.Height;
        }
        DockPanel.SetDock(Navigation, rail ? Dock.Left : Dock.Bottom);
        Navigation.SetRail(rail);
    }

    private void OnBackRequested(object? sender, RoutedEventArgs args)
    {
        if (!IsEffectivelyVisible) { return; }
        if (!args.Handled) { args.Handled = NavigateBack(); }
    }

    private bool NavigateBack()
    {
        if (this.FindControl<ToggleButton>("StorageDetailsToggle")!.IsChecked == true) { SetStorageDetails(false); return true; }
        var content = Host.Content;
        if (content is HomeEditorScreen editor) { editor.RequestBack(); return true; }
        if (ReferenceEquals(content, home)) { return home.TryNavigateBack(); }
        if (content is ControlWorkbench) { Navigate(ShellDestination.More); return true; }
        ShowHome();
        return true;
    }

    private void OpenEditor()
    {
        if (profileSession.HasUnreadableProfile && !profileSession.CanReplaceUnreadableProfile) { return; }
        SetStorageDetails(false);
        setupInvoker = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var editor = new HomeEditorScreen(home.State.Home, home.State.SourceCatalog, importCatalog, sharedLayoutFactory);
        editor.ConnectionRequested += RequestConnection;
        if (profileSession.HasUnreadableProfile) { editor.WarnAboutReplacement(); }
        editor.CancelRequested += ShowHome;
        editor.SaveRequested += updated =>
        {
            var previousConfirmation = profileSession.ConfirmationVersion;
            var selectedId = home.State.SelectedPlace?.Id;
            profileSession.ApplySetup(updated, replaceUnreadableProfile: profileSession.HasUnreadableProfile);
            ShowHome(revealOverview: selectedId is not null && updated.Places.All(place => place.Id != selectedId));
            if (!profileSession.IsSaving && profileSession.ConfirmationVersion > previousConfirmation)
            { home.SetSaveFeedback(profileSession.ConfirmationText); }
        };
        Host.Content = editor;
        RefreshStorageState();
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(Host.Content, editor) && editor.IsEffectivelyVisible) { editor.FindControl<TextBox>("HomeNameInput")!.Focus(); }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private async Task OpenSaveConflictAsync()
    {
        var review = new HomeSaveConflictScreen();
        review.FindControl<Button>("BackWithoutChoosing")!.Click += (_, _) => ShowHome();
        review.FindControl<Button>("ReadSavedAgain")!.Click += async (_, _) => await profileSession.ReviewConflictAsync();
        review.FindControl<Button>("KeepSessionChanges")!.Click += async (_, _) =>
        {
            if (await profileSession.ResolveConflictAsync(keepSessionChanges: true) && ReferenceEquals(Host.Content, review))
            { ShowHome(); home.SetSaveFeedback(profileSession.ConfirmationText); }
        };
        review.FindControl<Button>("UseSavedSetup")!.Click += async (_, _) =>
        {
            if (await profileSession.ResolveConflictAsync(keepSessionChanges: false) && ReferenceEquals(Host.Content, review))
            { ShowHome(); home.SetSaveFeedback(profileSession.ConfirmationText); }
        };
        Host.Content = review;
        RefreshStorageState();
        await profileSession.ReviewConflictAsync();
        review.FindControl<TextBlock>("ReviewTitle")!.Focus();
    }

    private void ShowHome() => ShowHome(revealOverview: false);

    private void ShowHome(bool revealOverview)
    {
        SetStorageDetails(false);
        home.SetSaveFeedback(null);
        var returningFromSetup = Host.Content is HomeEditorScreen;
        Host.Content = home;
        Navigation.Select(ShellDestination.Home);
        RefreshStorageState();
        if (returningFromSetup)
        {
            var expectedFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (!ReferenceEquals(Host.Content, home)) { return; }
                if (revealOverview)
                {
                    var current = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                    if (ReferenceEquals(current, expectedFocus) || current is not Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
                    { home.RevealOverviewAfterRemoval(); }
                    setupInvoker = null;
                    return;
                }
                if (setupInvoker is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true }) { setupInvoker.Focus(); }
                else { Navigation.ButtonFor(ShellDestination.Home).Focus(); }
                setupInvoker = null;
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private void ShowStory()
    {
        if (!home.State.IsDemo)
        {
            Host.Content = historyPending ??= new HistoryPendingScreen();
            return;
        }
        if (!ReferenceEquals(storyHome, home.State.Home))
        {
            storyHome = home.State.Home;
            var screen = new StoryScreen(SampleStory.Navigator(storyHome));
            story = screen;
            void InterruptEntry() { if (ReferenceEquals(Host.Content, screen)) { navigationVersion++; } }
            screen.DetachedFromVisualTree += (_, _) => InterruptEntry();
            screen.AddHandler(PointerPressedEvent, (_, _) => InterruptEntry(), RoutingStrategies.Tunnel, handledEventsToo: true);
            screen.AddHandler(PointerWheelChangedEvent, (_, _) => InterruptEntry(), RoutingStrategies.Tunnel, handledEventsToo: true);
            screen.AddHandler(KeyDownEvent, (_, _) => InterruptEntry(), RoutingStrategies.Tunnel, handledEventsToo: true);
            _ = new PresentationVisibility(screen, () => { if (!screen.IsEffectivelyVisible) { InterruptEntry(); } });
        }
        Host.Content = story;
    }

    /// <summary>
    /// Opens Story on today, filtered to one place or, with null, the whole home. Connected Story has no history yet,
    /// so it shows that state instead.
    /// </summary>
    public void OpenStoryFor(string? placeId)
    {
        Navigate(ShellDestination.Story);
        if (Host.Content is not StoryScreen screen) { return; }
        screen.ShowToday();
        if (placeId is null || screen.State.Day.Places.Any(place => place.Id == placeId)) { screen.State.SelectPlace(placeId); }
        var version = navigationVersion;
        var model = screen.State;
        var filter = model.SelectedPlaceId;
        var instant = model.SelectedInstant;
        var chapter = model.SelectedChapterId;
        var root = TopLevel.GetTopLevel(screen);
        var expectedFocus = root?.FocusManager?.GetFocusedElement();
        Dispatcher.UIThread.Post(() =>
        {
            if (version != navigationVersion || !ReferenceEquals(Host.Content, screen) || !screen.IsEffectivelyVisible
                || !ReferenceEquals(screen.State, model) || model.SelectedPlaceId != filter || model.SelectedInstant != instant
                || model.SelectedChapterId != chapter || screen.FindControl<RadioButton>("DayMode")!.IsChecked != true
                || root is null || !ReferenceEquals(root, TopLevel.GetTopLevel(screen))) { return; }
            var current = root.FocusManager?.GetFocusedElement();
            if (!ReferenceEquals(current, expectedFocus) && current is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true }) { return; }
            screen.RevealHistoryContext();
        }, DispatcherPriority.Loaded);
    }

    private LuciaScreen LuciaDestination()
    {
        if (lucia is not null) { return lucia; }
        lucia = new LuciaScreen(home.State.IsDemo);
        if (activity is not null) { lucia.SetController(activity, RequestConnection); }
        return lucia;
    }

    private MoreScreen MoreDestination()
    {
        if (more is not null) { return more; }
        more = new MoreScreen(home.State.IsDemo);
        more.CustomizeRequested += () => { ShowHome(); home.OpenCustomization(); };
        more.SetupRequested += OpenEditor;
        more.ConnectionRequested += RequestConnection;
        more.RefreshRequested += () => RefreshRequested?.Invoke();
        more.ControlLabRequested += () =>
        {
            Host.Content = workbench ??= new ControlWorkbench();
            Navigation.Select(ShellDestination.More);
            RefreshStorageState();
        };
        return more;
    }

    private void RefreshStorageState()
    {
        var content = Host.Content;
        var reviewing = content is HomeSaveConflictScreen;
        var editing = content is HomeEditorScreen;
        if (content is HomeSaveConflictScreen review)
        {
            review.Refresh(Core.HomeProfile.From(home.State.Home, home.State.PinnedPlaceIds, home.State.SectionOrder), profileSession);
        }
        var footer = this.FindControl<StackPanel>("StorageFooter")!;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var footerHadFocus = focused is not null && footer.IsVisualAncestorOf(focused);
        var warning = profileSession.CanRetry || profileSession.HasConflict || profileSession.HasUnreadableProfile;
        this.FindControl<WrapPanel>("StorageActions")!.IsVisible = warning;
        var immediate = profileSession.IsLoading || warning;
        UpdateSaveNotice(immediate, footer.IsVisible);
        footer.IsVisible = !reviewing && (immediate || profileSession.IsSaving && saveNoticeReady);
        var status = this.FindControl<TextBlock>("StorageStatus")!;
        status.Text = StorageSummary();
        status[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(warning ? "ProblemBrush" : "MutedBrush");
        AutomationProperties.SetHelpText(status, warning ? profileSession.StatusText : null);
        this.FindControl<TextBlock>("StorageDetailsText")!.Text = profileSession.StatusText;
        var details = this.FindControl<ToggleButton>("StorageDetailsToggle")!;
        details.IsVisible = footer.IsVisible && warning && !profileSession.IsLoading && !profileSession.IsSaving;
        if (!details.IsVisible) { SetStorageDetails(false); }
        this.FindControl<ScrollViewer>("StorageDetailsScroll")!.MaxHeight = Math.Max(56, Math.Min(160, Bounds.Height * .25));
        this.FindControl<OperationProgress>("LocalProfileOperationProgress")!.IsBusy = (profileSession.IsLoading || profileSession.IsSaving) && !reviewing;
        this.FindControl<Button>("RetrySaveButton")!.IsVisible = profileSession.CanRetry;
        this.FindControl<Button>("RetrySaveButton")!.IsEnabled = !profileSession.IsLoading && !profileSession.IsSaving;
        this.FindControl<Button>("ReviewSaveConflict")!.IsVisible = profileSession.HasConflict;
        this.FindControl<Button>("ReviewSaveConflict")!.IsEnabled = !editing && !profileSession.IsLoading && !profileSession.IsSaving;
        this.FindControl<Button>("ReviewLocalSetup")!.IsVisible = profileSession.CanReplaceUnreadableProfile;
        this.FindControl<Button>("ReviewLocalSetup")!.IsEnabled = !editing && !profileSession.IsLoading && !profileSession.IsSaving;
        Host.IsEnabled = !profileSession.IsLoading;
        Navigation.IsVisible = !editing && !reviewing;
        Navigation.IsEnabled = !profileSession.IsLoading;
        more?.SetStorageContext(profileSession.StatusText);
        if (profileSession.IsLoading || profileSession.IsSaving) { home.SetSaveFeedback(null); }
        else if (profileSession.ConfirmationVersion != observedConfirmation)
        {
            observedConfirmation = profileSession.ConfirmationVersion;
            if (ReferenceEquals(content, home)) { home.SetSaveFeedback(profileSession.ConfirmationText); }
        }
        if (footerHadFocus && (!footer.IsVisible || !focused!.IsEffectivelyVisible || !focused.IsEffectivelyEnabled))
        {
            if (footer.IsVisible) { this.FindControl<TextBlock>("StorageStatus")!.Focus(); }
            else if (content is HomeEditorScreen editor) { editor.FindControl<TextBox>("HomeNameInput")!.Focus(); }
            else if (!reviewing) { Navigation.ButtonFor(Navigation.Selected).Focus(); }
        }
        home.SetSetupAvailable(!profileSession.IsLoading && (!profileSession.HasUnreadableProfile || profileSession.CanReplaceUnreadableProfile));
        more?.SetAvailability(
            canCustomize: !profileSession.IsLoading,
            canSetup: !profileSession.IsLoading && (!profileSession.HasUnreadableProfile || profileSession.CanReplaceUnreadableProfile),
            canRefresh: !refreshing && !profileSession.IsLoading);
    }

    private string StorageSummary()
    {
        if (profileSession.IsLoading) { return profileSession.StatusText; }
        if (profileSession.CanRetry) { return "Couldn't save. Changes still work in this session."; }
        if (profileSession.HasConflict) { return "Save conflict. Changes still work in this session."; }
        if (profileSession.HasUnreadableProfile)
        {
            return profileSession.CanReplaceUnreadableProfile
                ? "Saved setup couldn't be read. Saving is off."
                : "Saved setup is unavailable. Saving is off.";
        }
        return profileSession.StatusText;
    }

    private void UpdateSaveNotice(bool immediate, bool alreadyVisible)
    {
        saveNoticeTimer.Stop();
        if (!profileSession.IsSaving)
        {
            wasSaving = false; saveNoticeReady = false; saveNoticeClock.Reset(); return;
        }
        if (!wasSaving)
        {
            wasSaving = true; saveNoticeClock.Restart(); saveNoticeReady = immediate || alreadyVisible;
        }
        if (immediate) { saveNoticeReady = true; }
        if (saveNoticeReady || TopLevel.GetTopLevel(this) is null || !IsEffectivelyVisible || MotionSettings.Current.IsPaused) { return; }
        var remaining = SaveNoticeDelay - saveNoticeClock.Elapsed;
        if (remaining <= TimeSpan.Zero) { saveNoticeReady = true; return; }
        saveNoticeTimer.Interval = remaining;
        saveNoticeTimer.Start();
    }

    private void SetStorageDetails(bool show)
    {
        var scroll = this.FindControl<ScrollViewer>("StorageDetailsScroll")!;
        var toggle = this.FindControl<ToggleButton>("StorageDetailsToggle")!;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var detailsHadFocus = focused is not null && scroll.IsVisualAncestorOf(focused);
        show &= toggle.IsVisible;
        scroll.IsVisible = show;
        toggle.Content = show ? "Hide details" : "Details";
        AutomationProperties.SetName(toggle, show ? "Hide local save details" : "Show local save details");
        toggle.IsChecked = show;
        if (!show && detailsHadFocus && toggle.IsEffectivelyVisible) { toggle.Focus(); }
    }

    public void SetImportCatalog(HomeCatalog? catalog) => importCatalog = catalog;
    public void InvalidateSharedLayout()
    {
        if (Host.Content is HomeEditorScreen editor) { editor.InvalidateSharedLayout(); }
    }
    public void SetRefreshing(bool value) { refreshing = value; RefreshStorageState(); }
}
