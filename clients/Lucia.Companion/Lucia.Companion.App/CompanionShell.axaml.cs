using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;

namespace Lucia.Companion.App;

public sealed partial class CompanionShell : UserControl
{
    private readonly HomeScreen home;
    private readonly HomeProfileSession profileSession;
    private ControlWorkbench? workbench;
    private StoryScreen? story;
    private Core.HomeTopology? storyHome;
    private readonly MenuItem controlLab = new() { Header = "Control lab" };
    private readonly MenuItem customizeHome = new() { Header = "Customize Home" };
    private TopLevel? attachedTopLevel;
    private HomeCatalog? importCatalog;
    private bool refreshing;
    private readonly Func<Func<Core.HomeTopology>, SharedLayoutAdoptionController>? sharedLayoutFactory;
    public HomeProfileSession ProfileSession => profileSession;
    public event Action? ConnectionRequested;
    public event Action? RefreshRequested;
    public void RequestConnection() => ConnectionRequested?.Invoke();
    public void BindActivity(ConnectedActivityController controller) => home.FindControl<ConnectedLuciaNow>("ConnectedLuciaNotice")!.SetController(controller, RequestConnection);

    public CompanionShell() : this(new HomeViewModel(SampleHome.Create(), SampleObservationSources.Create()), new LocalHomeStore(LocalHomeStore.DefaultPath)) { }

    public CompanionShell(HomeViewModel state, LocalHomeStore? store, ICompanionConnection? connection = null, ConnectionSession? identity = null)
    {
        home = new HomeScreen(state);
        profileSession = new(state, store);
        if (!state.IsDemo && connection is not null && identity is not null)
        { sharedLayoutFactory = draft => new(connection, identity, draft); }
        AvaloniaXamlLoader.Load(this);
        this.FindControl<ContentControl>("ScreenHost")!.Content = home;
        var appearance = this.FindControl<Button>("AppearanceButton")!;
        var connectionItem = new MenuItem { Header = "Connection" };
        connectionItem.Click += (_, _) => RequestConnection();
        var menu = new List<MenuItem> { new MenuItem { Header = "Appearance", ItemsSource = new[]
            {
                ThemeItem("System", ThemeVariant.Default), ThemeItem("Light", ThemeVariant.Light), ThemeItem("Dark", ThemeVariant.Dark)
            }}, customizeHome, connectionItem };
        if (state.IsDemo) { menu.Add(controlLab); }
        appearance.ContextMenu = new ContextMenu { ItemsSource = menu };
        this.FindControl<Button>("StoryButton")!.IsVisible = state.IsDemo;
        this.FindControl<Button>("RefreshHomeReports")!.IsVisible = !state.IsDemo;
        this.FindControl<Button>("RefreshHomeReports")!.Click += (_, _) => RefreshRequested?.Invoke();
        appearance.Click += (_, _) => appearance.ContextMenu.Open(appearance);
        this.FindControl<Button>("BuildHomeButton")!.Click += (_, _) => OpenEditor();
        this.FindControl<Button>("RetrySaveButton")!.Click += (_, _) => profileSession.RetrySave();
        this.FindControl<Button>("ReviewSaveConflict")!.Click += async (_, _) => await OpenSaveConflictAsync();
        controlLab.Click += (_, _) =>
        {
            this.FindControl<ContentControl>("ScreenHost")!.Content = workbench ??= new ControlWorkbench();
            this.FindControl<Button>("StoryButton")!.Content = "Story";
        };
        customizeHome.Click += (_, _) => { ShowHome(); home.OpenCustomization(); };
        this.FindControl<Button>("StoryButton")!.Click += (_, _) => SwitchStory();
        profileSession.Changed += RefreshStorageState;
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
            appearance.ContextMenu.Close();
        };
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape && !args.Handled) { args.Handled = NavigateBack(); }
        };
        RefreshStorageState();
    }

    private void OnBackRequested(object? sender, RoutedEventArgs args)
    {
        if (!IsEffectivelyVisible) { return; }
        if (!args.Handled) { args.Handled = NavigateBack(); }
    }

    private bool NavigateBack()
    {
        var content = this.FindControl<ContentControl>("ScreenHost")!.Content;
        if (content is HomeEditorScreen editor) { editor.RequestBack(); return true; }
        if (ReferenceEquals(content, home)) { return home.TryNavigateBack(); }
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
        this.FindControl<ContentControl>("ScreenHost")!.Content = editor;
        this.FindControl<Button>("BuildHomeButton")!.IsEnabled = false;
        this.FindControl<Button>("StoryButton")!.IsEnabled = false;
        controlLab.IsEnabled = false;
        customizeHome.IsEnabled = false;
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
        this.FindControl<ContentControl>("ScreenHost")!.Content = review;
        RefreshStorageState();
        await profileSession.ReviewConflictAsync();
        review.FindControl<TextBlock>("ReviewTitle")!.Focus();
    }

    private void ShowHome()
    {
        this.FindControl<ContentControl>("ScreenHost")!.Content = home;
        this.FindControl<Button>("BuildHomeButton")!.IsEnabled = true;
        this.FindControl<Button>("StoryButton")!.IsEnabled = true;
        this.FindControl<Button>("StoryButton")!.Content = "Story";
        controlLab.IsEnabled = true;
        customizeHome.IsEnabled = true;
        RefreshStorageState();
    }

    private void SwitchStory()
    {
        if (!home.State.IsDemo) { return; }
        var host = this.FindControl<ContentControl>("ScreenHost")!;
        if (host.Content is StoryScreen) { ShowHome(); return; }
        if (!ReferenceEquals(storyHome, home.State.Home))
        {
            storyHome = home.State.Home;
            story = new StoryScreen(new StoryViewModel(SampleStory.Create(storyHome)));
        }
        host.Content = story;
        this.FindControl<Button>("StoryButton")!.Content = "Home";
    }

    private void RefreshStorageState()
    {
        var content = this.FindControl<ContentControl>("ScreenHost")!.Content;
        var reviewing = content is HomeSaveConflictScreen;
        if (content is HomeSaveConflictScreen review)
        {
            review.Refresh(Core.HomeProfile.From(home.State.Home, home.State.PinnedPlaceIds, home.State.SectionOrder), profileSession);
        }
        this.FindControl<StackPanel>("StorageFooter")!.IsVisible = !reviewing;
        this.FindControl<TextBlock>("StorageStatus")!.Text = profileSession.StatusText;
        this.FindControl<Button>("RetrySaveButton")!.IsVisible = profileSession.CanRetry;
        this.FindControl<Button>("ReviewSaveConflict")!.IsVisible = profileSession.HasConflict;
        this.FindControl<Button>("ReviewSaveConflict")!.IsEnabled = content is not HomeEditorScreen && !profileSession.IsLoading;
        this.FindControl<ContentControl>("ScreenHost")!.IsEnabled = !profileSession.IsLoading;
        this.FindControl<Button>("BuildHomeButton")!.IsEnabled = !profileSession.IsLoading && !reviewing &&
            (!profileSession.HasUnreadableProfile || profileSession.CanReplaceUnreadableProfile) && content is not HomeEditorScreen;
        this.FindControl<Button>("StoryButton")!.IsEnabled = !profileSession.IsLoading && !reviewing && content is not HomeEditorScreen;
        controlLab.IsEnabled = this.FindControl<Button>("StoryButton")!.IsEnabled;
        customizeHome.IsEnabled = controlLab.IsEnabled;
        this.FindControl<Button>("RefreshHomeReports")!.IsEnabled = !refreshing && !profileSession.IsLoading && content is not HomeEditorScreen && !reviewing;
    }

    public void SetImportCatalog(HomeCatalog? catalog) => importCatalog = catalog;
    public void InvalidateSharedLayout()
    {
        if (this.FindControl<ContentControl>("ScreenHost")!.Content is HomeEditorScreen editor) { editor.InvalidateSharedLayout(); }
    }
    public void SetRefreshing(bool value) { refreshing = value; RefreshStorageState(); }

    private static MenuItem ThemeItem(string label, ThemeVariant theme)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => Application.Current!.RequestedThemeVariant = theme;
        return item;
    }
}
