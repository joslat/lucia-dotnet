using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Lucia.Companion.App.Controls;

/// <summary>Observes actual presentation visibility, including an ancestor hiding an otherwise attached view.</summary>
public sealed class PresentationVisibility : IDisposable
{
    private readonly Control owner;
    private readonly Action changed;
    private Visual[] watched = [];

    public PresentationVisibility(Control owner, Action changed)
    {
        this.owner = owner; this.changed = changed;
        owner.AttachedToVisualTree += Attached;
        owner.DetachedFromVisualTree += Detached;
        if (TopLevel.GetTopLevel(owner) is not null) { Watch(); }
    }

    private void Attached(object? sender, VisualTreeAttachmentEventArgs args) => Watch();
    private void Detached(object? sender, VisualTreeAttachmentEventArgs args) { Unwatch(); changed(); }
    private void Watch()
    {
        Unwatch();
        watched = owner.GetVisualAncestors().Prepend(owner).ToArray();
        foreach (var visual in watched) { visual.PropertyChanged += VisibilityChanged; }
        changed();
    }
    private void VisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == Visual.IsVisibleProperty) { changed(); }
    }
    private void Unwatch()
    {
        foreach (var visual in watched) { visual.PropertyChanged -= VisibilityChanged; }
        watched = [];
    }
    public void Dispose()
    {
        owner.AttachedToVisualTree -= Attached;
        owner.DetachedFromVisualTree -= Detached;
        Unwatch();
    }
}
