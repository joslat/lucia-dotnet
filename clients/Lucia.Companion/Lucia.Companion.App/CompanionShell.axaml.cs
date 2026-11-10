using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
    /// <summary>At this width the bottom bar becomes a left rail.</summary>
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
        Host.Content = home;
        Navigation.DestinationRequested += Navigate;
        this.FindControl<Button>("RetrySaveButton")!.Click += (_, _) => profileSession.RetrySave();
        this.FindControl<Button>("ReviewSaveConflict")!.Click += async (_, _) => await OpenSaveConflictAsync();
        profileSession.Changed += RefreshStorageState;
        SizeChanged += (_, _) => ArrangeNavigation();
        AttachedToVisualTree += async (_, _) =>
        {
            attachedTopLevel = TopLevel.GetTopLevel(this);
            if (attachedTopLevel is not null) { attachedTopLevel.BackRequested += OnBackRequested; }
            await profileSession.InitializeAsync();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (attachedTopLevel is not null) { attachedTopLevel.BackRequested -= OnBackRequested; }
            attachedTopLevel = null;
        };
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape && !args.Handled) { args.Handled = NavigateBack(); }
        };
        ArrangeNavigation();
        RefreshStorageState();
    }

    public void Navigate(ShellDestination destination)
    {
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

    private void ArrangeNavigation()
    {
        var rail = Bounds.Width >= RailWidthThreshold;
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
        var editor = new HomeEditorScreen(home.State.Home, home.State.SourceCatalog, importCatalog, sharedLayoutFactory);
        editor.ConnectionRequested += RequestConnection;
        if (profileSession.HasUnreadableProfile) { editor.WarnAboutReplacement(); }
        editor.CancelRequested += ShowHome;
        editor.SaveRequested += updated =>
        {
            profileSession.ApplySetup(updated, replaceUnreadableProfile: profileSession.HasUnreadableProfile);
            ShowHome();
        };
        Host.Content = editor;
        RefreshStorageState();
    }

    private async Task OpenSaveConflictAsync()
    {
        var review = new HomeSaveConflictScreen();
        review.FindControl<Button>("BackWithoutChoosing")!.Click += (_, _) => ShowHome();
        review.FindControl<Button>("ReadSavedAgain")!.Click += async (_, _) => await profileSession.ReviewConflictAsync();
        review.FindControl<Button>("KeepSessionChanges")!.Click += async (_, _) =>
        {
            if (await profileSession.ResolveConflictAsync(keepSessionChanges: true)) { ShowHome(); }
        };
        review.FindControl<Button>("UseSavedSetup")!.Click += async (_, _) =>
        {
            if (await profileSession.ResolveConflictAsync(keepSessionChanges: false)) { ShowHome(); }
        };
        Host.Content = review;
        RefreshStorageState();
        await profileSession.ReviewConflictAsync();
        review.FindControl<TextBlock>("ReviewTitle")!.Focus();
    }

    private void ShowHome()
    {
        Host.Content = home;
        Navigation.Select(ShellDestination.Home);
        RefreshStorageState();
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
            story = new StoryScreen(new StoryViewModel(SampleStory.Create(storyHome)));
        }
        Host.Content = story;
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
        this.FindControl<StackPanel>("StorageFooter")!.IsVisible = !reviewing;
        this.FindControl<TextBlock>("StorageStatus")!.Text = profileSession.StatusText;
        this.FindControl<Button>("RetrySaveButton")!.IsVisible = profileSession.CanRetry;
        this.FindControl<Button>("ReviewSaveConflict")!.IsVisible = profileSession.HasConflict;
        this.FindControl<Button>("ReviewSaveConflict")!.IsEnabled = !editing && !profileSession.IsLoading;
        Host.IsEnabled = !profileSession.IsLoading;
        Navigation.IsVisible = !editing && !reviewing;
        Navigation.IsEnabled = !profileSession.IsLoading;
        more?.SetAvailability(
            canCustomize: !profileSession.IsLoading,
            canSetup: !profileSession.IsLoading && (!profileSession.HasUnreadableProfile || profileSession.CanReplaceUnreadableProfile),
            canRefresh: !refreshing && !profileSession.IsLoading);
    }

    public void SetImportCatalog(HomeCatalog? catalog) => importCatalog = catalog;
    public void InvalidateSharedLayout()
    {
        if (Host.Content is HomeEditorScreen editor) { editor.InvalidateSharedLayout(); }
    }
    public void SetRefreshing(bool value) { refreshing = value; RefreshStorageState(); }
}
