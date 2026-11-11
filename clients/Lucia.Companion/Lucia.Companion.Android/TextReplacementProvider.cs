using Android.OS;
using AndroidX.Core.View.Accessibility;

namespace Lucia.Companion.Android;

/// <summary>Forwards the platform provider and repairs registered text replacement and input focus.</summary>
internal sealed class TextReplacementProvider(AccessibilityNodeProviderCompat original, NativeTextFields fields) : AccessibilityNodeProviderCompat
{
    public override AccessibilityNodeInfoCompat? CreateAccessibilityNodeInfo(int virtualViewId) => original.CreateAccessibilityNodeInfo(virtualViewId);

    public override bool PerformAction(int virtualViewId, int action, Bundle? arguments)
    {
        if (action == AccessibilityNodeInfoCompat.ActionFocus)
        {
            using var target = original.CreateAccessibilityNodeInfo(virtualViewId);
            // A retired peer has empty/disabled metadata even after its instance ID is released.
            // The helper can otherwise accept an unknown virtual keyboard-focus target.
            if (target is not { Enabled: true, Focusable: true }) { return false; }
            if (target?.ViewIdResourceName is { } fieldId && fieldId.StartsWith(NativeTextFields.IdentityPrefix, StringComparison.Ordinal))
            {
                // The helper tracks virtual keyboard focus separately from Avalonia's actual text-input focus.
                // Acquire actual focus first so a failed request cannot leave a new virtual-only success.
                if (!fields.Focus(fieldId)) { return false; }
                original.PerformAction(virtualViewId, action, arguments);
                return true;
            }
        }
        if (action != AccessibilityNodeInfoCompat.ActionSetText) { return original.PerformAction(virtualViewId, action, arguments); }
        if (arguments?.GetCharSequence(AccessibilityNodeInfoCompat.ActionArgumentSetTextCharsequence) is not { } text) { return false; }
        // Resolve fresh identity metadata, including nodes obtained via focus or text search.
        // Text, password values and provider.Value are never inspected by the adapter.
        using var node = original.CreateAccessibilityNodeInfo(virtualViewId);
        return node?.ViewIdResourceName is { } id && fields.Replace(id, text);
    }

    public override AccessibilityNodeInfoCompat? FindFocus(int focus) => original.FindFocus(focus);
    public override IList<AccessibilityNodeInfoCompat>? FindAccessibilityNodeInfosByText(string? text, int virtualViewId)
        => original.FindAccessibilityNodeInfosByText(text, virtualViewId);
    public override void AddExtraDataToAccessibilityNodeInfo(int virtualViewId, AccessibilityNodeInfoCompat? info, string? extraDataKey, Bundle? arguments)
        => original.AddExtraDataToAccessibilityNodeInfo(virtualViewId, info, extraDataKey, arguments);
}
