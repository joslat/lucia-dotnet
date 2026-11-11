using Android.Views;
using AndroidX.Core.View;
using Avalonia.Android;

namespace Lucia.Companion.Android;

/// <summary>Owns the public Android delegate adapter for this activity's Avalonia view.</summary>
internal sealed class AndroidTextReplacement : IDisposable
{
    private readonly View view;
    private readonly AccessibilityDelegateCompat original;
    private readonly NativeTextFields fields;
    private readonly TextReplacementProvider provider;
    private readonly TextReplacementDelegate replacement;

    private AndroidTextReplacement(AvaloniaView view, AccessibilityDelegateCompat original, Avalonia.Controls.Control root)
    {
        this.view = view;
        this.original = original;
        fields = new(root);
        provider = new(original.GetAccessibilityNodeProvider(view)!, fields);
        replacement = new(original, provider);
        ViewCompat.SetAccessibilityDelegate(view, replacement);
    }

    public static AndroidTextReplacement? Attach(View? view, Avalonia.Controls.Control root)
    {
        if (view is AvaloniaView avalonia && ViewCompat.GetAccessibilityDelegate(view) is { } original
            && original.GetAccessibilityNodeProvider(view) is not null) { return new(avalonia, original, root); }
        if (view is ViewGroup group)
        {
            for (var index = 0; index < group.ChildCount; index++)
            {
                if (Attach(group.GetChildAt(index), root) is { } adapter) { return adapter; }
            }
        }
        return null;
    }

    public void Dispose()
    {
        if (ViewCompat.GetAccessibilityDelegate(view)?.Equals(replacement) == true) { ViewCompat.SetAccessibilityDelegate(view, original); }
        fields.Dispose();
        replacement.Dispose();
        provider.Dispose();
    }
}
