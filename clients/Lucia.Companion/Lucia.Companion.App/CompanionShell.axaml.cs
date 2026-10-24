using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;

namespace Lucia.Companion.App;

public sealed partial class CompanionShell : UserControl
{
    private readonly HomeScreen home;
    private readonly HomeProfileSession profileSession;
    private ControlWorkbench? workbench;

    public CompanionShell() : this(new HomeViewModel(SampleHome.Create()), new LocalHomeStore(LocalHomeStore.DefaultPath)) { }

    public CompanionShell(HomeViewModel state, LocalHomeStore? store)
    {
        home = new HomeScreen(state);
        profileSession = new(state, store);
        AvaloniaXamlLoader.Load(this);
        this.FindControl<ContentControl>("ScreenHost")!.Content = home;
        var appearance = this.FindControl<Button>("AppearanceButton")!;
        appearance.ContextMenu = new ContextMenu
        {
            ItemsSource = new[] { ThemeItem("System", ThemeVariant.Default), ThemeItem("Light", ThemeVariant.Light), ThemeItem("Dark", ThemeVariant.Dark) }
        };
        appearance.Click += (_, _) => appearance.ContextMenu.Open(appearance);
        this.FindControl<Button>("BuildHomeButton")!.Click += (_, _) => OpenEditor();
        this.FindControl<Button>("RetrySaveButton")!.Click += (_, _) => profileSession.RetrySave();
        this.FindControl<Button>("LabButton")!.Click += (_, _) =>
        {
            var host = this.FindControl<ContentControl>("ScreenHost")!;
            var showLab = ReferenceEquals(host.Content, home);
            host.Content = showLab ? workbench ??= new ControlWorkbench() : home;
            this.FindControl<Button>("LabButton")!.Content = showLab ? "Home" : "Controls";
        };
        profileSession.Changed += RefreshStorageState;
        AttachedToVisualTree += async (_, _) => await profileSession.InitializeAsync();
        RefreshStorageState();
    }

    private void OpenEditor()
    {
        var editor = new HomeEditorScreen(home.State.Home);
        if (profileSession.HasUnreadableProfile) { editor.WarnAboutReplacement(); }
        editor.CancelRequested += ShowHome;
        editor.SaveRequested += updated =>
        {
            profileSession.ApplySetup(updated, replaceUnreadableProfile: profileSession.HasUnreadableProfile);
            ShowHome();
        };
        this.FindControl<ContentControl>("ScreenHost")!.Content = editor;
        this.FindControl<Button>("BuildHomeButton")!.IsEnabled = false;
        this.FindControl<Button>("LabButton")!.IsEnabled = false;
    }

    private void ShowHome()
    {
        this.FindControl<ContentControl>("ScreenHost")!.Content = home;
        this.FindControl<Button>("BuildHomeButton")!.IsEnabled = true;
        this.FindControl<Button>("LabButton")!.IsEnabled = true;
        this.FindControl<Button>("LabButton")!.Content = "Controls";
    }

    private void RefreshStorageState()
    {
        this.FindControl<TextBlock>("StorageStatus")!.Text = profileSession.StatusText;
        this.FindControl<Button>("RetrySaveButton")!.IsVisible = profileSession.CanRetry;
        this.FindControl<ContentControl>("ScreenHost")!.IsEnabled = !profileSession.IsLoading;
        this.FindControl<Button>("BuildHomeButton")!.IsEnabled = !profileSession.IsLoading &&
            this.FindControl<ContentControl>("ScreenHost")!.Content is not HomeEditorScreen;
    }

    private static MenuItem ThemeItem(string label, ThemeVariant theme)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => Application.Current!.RequestedThemeVariant = theme;
        return item;
    }
}
