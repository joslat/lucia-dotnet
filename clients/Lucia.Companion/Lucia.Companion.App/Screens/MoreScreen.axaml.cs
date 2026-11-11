using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Lucia.Companion.App.Screens;

/// <summary>Appearance, Home customization and setup, connection and, in the demo, the control lab.</summary>
public sealed partial class MoreScreen : UserControl
{
    private bool syncingTheme;
    private readonly MotionSettings motion;

    public event Action? CustomizeRequested;
    public event Action? SetupRequested;
    public event Action? ConnectionRequested;
    public event Action? RefreshRequested;
    public event Action? ControlLabRequested;

    public MoreScreen() : this(isDemo: true) { }

    public MoreScreen(bool isDemo, MotionSettings? motion = null)
    {
        this.motion = motion ?? MotionSettings.Current;
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Border>("DemoTools")!.IsVisible = isDemo;
        this.FindControl<Button>("RefreshHomeReports")!.IsVisible = !isDemo;
        this.FindControl<TextBlock>("ConnectionHint")!.Text = isDemo
            ? "Connect to your Lucia host. The demo stays separate."
            : "Check this device's access, or disconnect.";
        this.FindControl<Button>("CustomizeHome")!.Click += (_, _) => CustomizeRequested?.Invoke();
        this.FindControl<Button>("OpenHomeSetup")!.Click += (_, _) => SetupRequested?.Invoke();
        this.FindControl<Button>("OpenConnection")!.Click += (_, _) => ConnectionRequested?.Invoke();
        this.FindControl<Button>("RefreshHomeReports")!.Click += (_, _) => RefreshRequested?.Invoke();
        this.FindControl<Button>("OpenControlLab")!.Click += (_, _) => ControlLabRequested?.Invoke();
        foreach (var (name, theme) in new[] { ("ThemeSystem", ThemeVariant.Default), ("ThemeLight", ThemeVariant.Light), ("ThemeDark", ThemeVariant.Dark) })
        {
            this.FindControl<RadioButton>(name)!.IsCheckedChanged += (sender, _) =>
            {
                if (!syncingTheme && sender is RadioButton { IsChecked: true } && Application.Current is { } app) { app.RequestedThemeVariant = theme; }
            };
        }
        var reduce = this.FindControl<CheckBox>("ReduceMotion")!;
        reduce.IsCheckedChanged += (_, _) => { if (!syncingTheme) { this.motion.ReduceMotion = reduce.IsChecked == true; } };
        AttachedToVisualTree += (_, _) => { this.motion.Changed += SyncTheme; SyncTheme(); };
        DetachedFromVisualTree += (_, _) => this.motion.Changed -= SyncTheme;
        SyncTheme();
    }

    public void SetAvailability(bool canCustomize, bool canSetup, bool canRefresh)
    {
        this.FindControl<Button>("CustomizeHome")!.IsEnabled = canCustomize;
        this.FindControl<Button>("OpenHomeSetup")!.IsEnabled = canSetup;
        this.FindControl<Button>("OpenControlLab")!.IsEnabled = canCustomize;
        this.FindControl<Button>("RefreshHomeReports")!.IsEnabled = canRefresh;
    }

    private void SyncTheme()
    {
        var current = Application.Current?.RequestedThemeVariant ?? ThemeVariant.Default;
        syncingTheme = true;
        try
        {
            this.FindControl<RadioButton>("ThemeSystem")!.IsChecked = current == ThemeVariant.Default;
            this.FindControl<RadioButton>("ThemeLight")!.IsChecked = current == ThemeVariant.Light;
            this.FindControl<RadioButton>("ThemeDark")!.IsChecked = current == ThemeVariant.Dark;
            this.FindControl<CheckBox>("ReduceMotion")!.IsChecked = motion.ReduceMotion;
            this.FindControl<TextBlock>("SystemMotionStatus")!.IsVisible = motion.SystemReduceMotion;
            this.FindControl<TextBlock>("MotionStorageStatus")!.Text = motion.StorageStatus;
        }
        finally { syncingTheme = false; }
    }
}
