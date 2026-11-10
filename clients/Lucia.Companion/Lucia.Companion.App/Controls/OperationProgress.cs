using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Lucia.Companion.App.Controls;

/// <summary>Unknown-duration activity driven only by a real operation. Reduced motion uses static dots, never a percentage.</summary>
public sealed class OperationProgress : UserControl
{
    public static readonly StyledProperty<bool> IsBusyProperty = AvaloniaProperty.Register<OperationProgress, bool>(nameof(IsBusy));
    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }
    private readonly ProgressBar bar = new() { Height = 4, VerticalAlignment = VerticalAlignment.Center, ShowProgressText = false };
    private readonly StackPanel dots = new() { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private bool attached;

    public OperationProgress()
    {
        Height = 8;
        IsHitTestVisible = false;
        bar[!ProgressBar.ForegroundProperty] = new DynamicResourceExtension("AccentBrush");
        bar[!ProgressBar.BackgroundProperty] = new DynamicResourceExtension("GraphLineBrush");
        for (var index = 0; index < 3; index++)
        {
            dots.Children.Add(new Ellipse { Width = 4, Height = 4, [!Shape.FillProperty] = new DynamicResourceExtension("AccentBrush") });
        }
        Content = new Grid { Children = { bar, dots } };
        AutomationProperties.SetName(this, "Operation in progress. No percentage is available.");
        PropertyChanged += (_, args) => { if (args.Property == IsBusyProperty) { Update(); } };
        AttachedToVisualTree += (_, _) => { attached = true; MotionSettings.Current.Changed += Update; Update(); };
        DetachedFromVisualTree += (_, _) => { attached = false; MotionSettings.Current.Changed -= Update; bar.IsIndeterminate = false; };
        _ = new PresentationVisibility(this, Update);
        Update();
    }

    private void Update()
    {
        IsVisible = IsBusy;
        var animate = attached && IsBusy && IsEffectivelyVisible && MotionSettings.Current.IsEnabled;
        bar.IsIndeterminate = animate;
        bar.IsVisible = animate;
        dots.IsVisible = IsBusy && !animate;
    }
}
