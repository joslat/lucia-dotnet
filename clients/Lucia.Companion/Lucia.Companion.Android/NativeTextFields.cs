using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;

namespace Lucia.Companion.Android;

/// <summary>Android-only identities for the app's explicitly allowed, nonsecret text fields.</summary>
internal sealed class NativeTextFields : IDisposable
{
    public const string IdentityPrefix = "lucia-text-";
    private readonly Control root;
    private readonly Dictionary<TextBox, (string Id, string? Previous)> fields = [];
    private long nextId;

    public NativeTextFields(Control root)
    {
        this.root = root;
        root.LayoutUpdated += LayoutUpdated;
        Refresh();
    }

    private static bool IsAllowed(TextBox field) => field.PasswordChar == default && field.GetVisualAncestors().Any(owner => owner switch
    {
        ConnectionScreen => field.Name is "HostAddress" or "DeviceName",
        HomeEditorScreen => field.Name is "HomeNameInput" or "PlaceNameInput",
        HomeImportPlaceRow => field.Name == "ImportPlaceName",
        _ => false,
    });

    private void LayoutUpdated(object? sender, EventArgs args) => Refresh();

    private void Refresh()
    {
        var current = root.GetVisualDescendants().OfType<TextBox>().Where(IsAllowed).ToHashSet();
        foreach (var field in fields.Keys.Where(field => !current.Contains(field)).ToArray()) { Release(field); }
        foreach (var field in current.Where(field => !fields.ContainsKey(field)))
        {
            // An instance identity prevents a stale node from resolving to a later field with the same XAML name.
            var id = $"{IdentityPrefix}{++nextId}/{field.Name}";
            fields.Add(field, (id, AutomationProperties.GetAutomationId(field)));
            AutomationProperties.SetAutomationId(field, id);
        }
    }

    public bool Replace(string id, string text)
    {
        var field = EligibleField(id);
        if (field is null || field.IsReadOnly) { return false; }
        var provider = ControlAutomationPeer.CreatePeerForElement(field)?.GetProvider<IValueProvider>();
        if (provider is null || provider.IsReadOnly) { return false; }
        provider.SetValue(text);
        field.CaretIndex = field.Text?.Length ?? 0;
        field.SelectionStart = field.SelectionEnd = field.CaretIndex;
        return true;
    }

    public bool Focus(string id)
    {
        var field = EligibleField(id);
        return field is { Focusable: true } && (field.IsFocused || field.Focus()) && field.IsFocused;
    }

    private TextBox? EligibleField(string id)
    {
        var field = fields.SingleOrDefault(entry => entry.Value.Id == id).Key;
        return field is not null && field.IsEffectivelyVisible && field.IsEffectivelyEnabled && field.PasswordChar == default
            && TopLevel.GetTopLevel(root) is not null && field.GetVisualAncestors().Contains(root) && IsAllowed(field)
            && !field.GetVisualAncestors().OfType<TransitioningContentControl>().Any(host => host.Content is not Control current
                || !field.GetVisualAncestors().Contains(current)) ? field : null;
    }

    private void Release(TextBox field)
    {
        var registration = fields[field];
        if (AutomationProperties.GetAutomationId(field) == registration.Id)
        {
            if (registration.Previous is null) { field.ClearValue(AutomationProperties.AutomationIdProperty); }
            else { AutomationProperties.SetAutomationId(field, registration.Previous); }
        }
        fields.Remove(field);
    }

    public void Dispose()
    {
        root.LayoutUpdated -= LayoutUpdated;
        foreach (var field in fields.Keys.ToArray()) { Release(field); }
    }
}
