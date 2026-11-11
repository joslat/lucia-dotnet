using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;

namespace Lucia.Companion.Android;

/// <summary>Preserves the existing delegate's events, focus and ordinary actions.</summary>
internal sealed class TextReplacementDelegate(AccessibilityDelegateCompat original, AccessibilityNodeProviderCompat provider) : AccessibilityDelegateCompat
{
    public override AccessibilityNodeProviderCompat? GetAccessibilityNodeProvider(View? host) => provider;
    public override bool DispatchPopulateAccessibilityEvent(View? host, AccessibilityEvent? e) => original.DispatchPopulateAccessibilityEvent(host, e);
    public override void OnInitializeAccessibilityEvent(View? host, AccessibilityEvent? e) => original.OnInitializeAccessibilityEvent(host, e);
    public override void OnInitializeAccessibilityNodeInfo(View? host, AccessibilityNodeInfoCompat? info) => original.OnInitializeAccessibilityNodeInfo(host, info);
    public override void OnPopulateAccessibilityEvent(View? host, AccessibilityEvent? e) => original.OnPopulateAccessibilityEvent(host, e);
    public override bool OnRequestSendAccessibilityEvent(ViewGroup? host, View? child, AccessibilityEvent? e) => original.OnRequestSendAccessibilityEvent(host, child, e);
    public override bool PerformAccessibilityAction(View? host, int action, Bundle? args) => original.PerformAccessibilityAction(host, action, args);
    public override void SendAccessibilityEvent(View? host, int eventType) => original.SendAccessibilityEvent(host, eventType);
    public override void SendAccessibilityEventUnchecked(View? host, AccessibilityEvent? e) => original.SendAccessibilityEventUnchecked(host, e);
}
