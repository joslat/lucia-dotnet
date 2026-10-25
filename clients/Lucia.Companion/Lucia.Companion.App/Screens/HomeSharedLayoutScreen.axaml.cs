using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Controls;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Screens;

/// <summary>Native comparison inside the setup editor. The controller rechecks, the parent stages, and Save remains separate.</summary>
public sealed partial class HomeSharedLayoutScreen : UserControl, IDisposable
{
    private bool initialized;
    private bool wasBusy;
    public SharedLayoutAdoptionController Controller { get; }
    public event Action<HomeTopology>? AddRequested;
    public event Action? CancelRequested;
    public event Action? ConnectionRequested;

    public HomeSharedLayoutScreen() => throw new InvalidOperationException("A protected shared-layout controller is required.");
    public HomeSharedLayoutScreen(SharedLayoutAdoptionController controller)
    {
        Controller = controller;
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Button>("ReadSharedSetup")!.Click += async (_, _) => await Controller.ReadAsync();
        this.FindControl<Button>("AddSharedSetupToDraft")!.Click += async (_, _) =>
        { if (await Controller.AddToDraftAsync() is { } accepted) { AddRequested?.Invoke(accepted); } };
        this.FindControl<Button>("KeepSetupDraft")!.Click += (_, _) => RequestBack();
        this.FindControl<Button>("SharedConnection")!.Click += (_, _) => ConnectionRequested?.Invoke();
        Controller.Changed += Update;
        AttachedToVisualTree += async (_, _) => { if (!initialized) { initialized = true; await Controller.ReadAsync(); } };
        DetachedFromVisualTree += (_, _) => Controller.Invalidate();
        KeyDown += (_, args) => { if (args.Key == Key.Escape && !args.Handled) { RequestBack(); args.Handled = true; } };
        SizeChanged += (_, _) => Adapt();
        this.FindControl<TextBlock>("SharedReviewStatus")!.PropertyChanged += (_, change) => { if (change.Property == TextBlock.FontSizeProperty) { Adapt(); } };
        Update();
    }
    public void RequestBack() => CancelRequested?.Invoke();
    private void Update()
    {
        this.FindControl<HomeLayoutSummary>("LocalLayoutSummary")!.SetHome(Controller.Draft);
        var plan = Controller.Plan;
        this.FindControl<Border>("SharedAlternative")!.IsVisible = plan is not null;
        this.FindControl<StackPanel>("SharedConsequences")!.IsVisible = plan is not null;
        if (plan is not null)
        {
            this.FindControl<HomeLayoutSummary>("SharedLayoutSummary")!.SetHome(plan.Preview);
            var removed = Controller.Draft.Places.Where(place => !plan.Preview.Places.Any(shared => shared.Id == place.Id)).Select(place => place.Name).ToArray();
            this.FindControl<TextBlock>("SharedReplacementNotice")!.Text = removed.Length == 0
                ? "Adding replaces this draft's names, icons, order, connections and bindings with the reviewed shared metadata."
                : "Adding replaces this draft's names, icons, order, connections and bindings. Places not in the shared setup will leave this draft: " + string.Join(", ", removed) + ".";
        }
        else { this.FindControl<HomeLayoutSummary>("SharedLayoutSummary")!.Content = null; }
        this.FindControl<TextBlock>("SharedReviewStatus")!.Text = Controller.StatusText;
        this.FindControl<Button>("ReadSharedSetup")!.IsEnabled = Controller.CanRead;
        this.FindControl<Button>("AddSharedSetupToDraft")!.IsEnabled = plan is not null && !Controller.IsBusy;
        this.FindControl<Button>("SharedConnection")!.IsVisible = Controller.RequiresConnectionReview;
        if (wasBusy && !Controller.IsBusy)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var root = TopLevel.GetTopLevel(this);
                if (root is null) { return; }
                if (root.FocusManager?.GetFocusedElement() is Control focused && !ReferenceEquals(focused, this) && !focused.GetVisualAncestors().Contains(this)) { return; }
                this.FindControl<TextBlock>(plan is null ? "SharedReviewStatus" : "SharedReviewTitle")!.Focus();
            }, DispatcherPriority.Loaded);
        }
        wasBusy = Controller.IsBusy;
        Adapt();
    }
    private void Adapt()
    {
        var expanded = Bounds.Width >= 900 && this.FindControl<TextBlock>("SharedReviewStatus")!.FontSize <= 20;
        var grid = this.FindControl<Grid>("SharedComparison")!;
        grid.ColumnDefinitions[1].Width = expanded ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        grid.ColumnSpacing = expanded ? 20 : 0;
        var shared = this.FindControl<Border>("SharedAlternative")!;
        Grid.SetColumn(shared, expanded ? 1 : 0); Grid.SetRow(shared, expanded ? 0 : 1);
        foreach (var name in new[] { "ReadSharedSetup", "AddSharedSetupToDraft", "KeepSetupDraft", "SharedConnection" })
        { this.FindControl<Button>(name)!.MaxWidth = Math.Max(100, Bounds.Width - 32); }
    }
    public void Dispose() { Controller.Changed -= Update; Controller.Dispose(); }
}
