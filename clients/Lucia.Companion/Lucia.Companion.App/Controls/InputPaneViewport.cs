using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Lucia.Companion.App.Controls;

/// <summary>Reserves only the part of a docked keyboard that overlaps the application's safe-area viewport.</summary>
public sealed class InputPaneViewport : Border
{
    private TopLevel? topLevel;
    private IInputPane? inputPane;
    private Rect occlusion;
    private int revision;

    public InputPaneViewport()
    {
        AttachedToVisualTree += (_, _) =>
        {
            topLevel = TopLevel.GetTopLevel(this);
            inputPane = topLevel?.InputPane;
            if (inputPane is not null) { inputPane.StateChanged += OnPaneChanged; }
            SetOcclusion(inputPane?.State == InputPaneState.Open ? inputPane.OccludedRect : default);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (inputPane is not null) { inputPane.StateChanged -= OnPaneChanged; }
            inputPane = null; topLevel = null; occlusion = default; Padding = default; revision++;
        };
        SizeChanged += (_, _) =>
        {
            if (inputPane is not null) { occlusion = inputPane.State == InputPaneState.Open ? inputPane.OccludedRect : default; }
            ApplyOcclusion();
        };
    }

    private void OnPaneChanged(object? sender, InputPaneStateEventArgs args) =>
        SetOcclusion(args.NewState == InputPaneState.Open ? args.EndRect : default);

    /// <summary>The keyboard rectangle uses top-level client coordinates, including any existing safe-area offsets.</summary>
    public void SetOcclusion(Rect rectangle) { occlusion = rectangle; ApplyOcclusion(); }

    private void ApplyOcclusion()
    {
        var current = ++revision;
        var origin = topLevel is not null ? this.TranslatePoint(default, topLevel) : null;
        var area = origin is { } point ? new Rect(point, Bounds.Size) : default;
        var docked = occlusion.Width > 0 && occlusion.Height > 0 && occlusion.Intersects(area)
            && occlusion.Bottom >= area.Bottom - 1;
        Padding = new Thickness(0, 0, 0, docked ? Math.Clamp(area.Bottom - occlusion.Top, 0, area.Height) : 0);
        if (!docked || topLevel?.FocusManager?.GetFocusedElement() is not Control focused) { return; }
        Dispatcher.UIThread.Post(() =>
        {
            if (revision == current && topLevel is not null && ReferenceEquals(topLevel.FocusManager?.GetFocusedElement(), focused)
                && focused.IsEffectivelyVisible && this.IsVisualAncestorOf(focused)) { focused.BringIntoView(); }
        }, DispatcherPriority.Loaded);
    }
}
