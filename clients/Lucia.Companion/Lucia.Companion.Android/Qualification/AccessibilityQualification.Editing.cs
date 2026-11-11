using Android.OS;
using Android.Views.Accessibility;
using Avalonia.Controls;
using Avalonia.VisualTree;
using NodeAction = Android.Views.Accessibility.Action;

namespace Lucia.Companion.Android.Qualification;

/// <summary>Native replacement regressions. Reports booleans only, including for the secure-field fixture.</summary>
public sealed partial class AccessibilityQualification
{
    private readonly List<object> editingChecks = [];

    private void QualifyEditing()
    {
        Click("NavMore");
        Click("OpenHomeSetup");
        TextBox field = null!;
        RunOnMainSync(() => field = ((Lucia.Companion.App.App)global::Avalonia.Application.Current!).CreateActivityView()
            .GetVisualDescendants().OfType<Lucia.Companion.App.Screens.HomeEditorScreen>().Single().FindControl<TextBox>("HomeNameInput")!);
        var failed = false;
        foreach (var (name, requested) in new[] { ("empty-clear", ""), ("replace", "Replacement"), ("unicode", "Café 東京 🌿") })
        {
            stage = name;
            RunOnMainSync(() => field.Text = "Previous value");
            Thread.Sleep(120);
            using var node = WaitFor(item => IsId(item, "HomeNameInput"));
            var accepted = SetNativeText(node, requested);
            Thread.Sleep(120);
            var replaced = false;
            RunOnMainSync(() => replaced = field.Text == requested);
            editingChecks.Add(new { name, accepted, replaced });
            failed |= !accepted || !replaced;
        }
        if (failed) { throw new InvalidOperationException("Native ACTION_SET_TEXT must replace the complete value, including empty and Unicode input."); }
        using var current = WaitFor(item => IsId(item, "HomeNameInput"));
        var focusAccepted = current.PerformAction(NodeAction.AccessibilityFocus);
        SetNativeText(current, "Focus retained");
        Thread.Sleep(120);
        using (var focused = automation.FindFocus(NodeFocus.Accessibility))
        {
            Check("focus-preserved", focusAccepted && focused?.Equals(current) == true);
            Check("focus-returned-node-edits", focused is not null && SetNativeText(focused, "Focus node replacement"));
        }
        var caretSettled = false;
        RunOnMainSync(() => caretSettled = field.CaretIndex == field.Text!.Length && field.SelectionStart == field.SelectionEnd);
        Check("caret-at-end", caretSettled);

        foreach (var mode in new[] { "disabled", "readonly", "hidden", "secure" })
        {
            stage = "reject-" + mode;
            RunOnMainSync(() =>
            {
                field.Text = "Unchanged";
                if (mode == "disabled") { field.IsEnabled = false; }
                if (mode == "readonly") { field.IsReadOnly = true; }
                if (mode == "hidden") { field.IsVisible = false; }
                if (mode == "secure") { field.PasswordChar = '*'; }
            });
            try
            {
                var accepted = SetNativeText(current, "Rejected replacement");
                var unchanged = false;
                RunOnMainSync(() => unchanged = field.Text == "Unchanged");
                Check(mode + "-rejected", !accepted && unchanged);
                if (mode is "disabled" or "hidden") { Check(mode + "-focus-rejected", !current.PerformAction(NodeAction.Focus)); }
            }
            finally
            {
                RunOnMainSync(() => { field.IsEnabled = true; field.IsReadOnly = false; field.IsVisible = true; field.PasswordChar = default; });
            }
        }

        using (var editable = WaitFor(item => IsId(item, "HomeNameInput")))
        {
            Check("missing-argument-rejected", !editable.PerformAction(NodeAction.SetText));
            SetNativeText(editable, "");
            stage = "ordinary-key-input";
            Check("ordinary-input-focus", editable.PerformAction(NodeAction.Focus));
            using (WaitFor(item => IsId(item, "HomeNameInput") && item.Focused)) { }
            var actualFocus = false;
            RunOnMainSync(() => actualFocus = field.IsFocused);
            Check("native-focus-reaches-field", actualFocus);
            // Input focus and the Android input connection are established on separate UI turns.
            Thread.Sleep(200);
            SendStringSync("Key");
            Thread.Sleep(200);
            var entered = false;
            RunOnMainSync(() =>
            {
                entered = field.Text == "Key";
                editingChecks.Add(new { name = "key-input-diagnostics", field.IsFocused, length = field.Text?.Length ?? 0,
                    exactExpected = entered, empty = string.IsNullOrEmpty(field.Text), lowercaseOnly = field.Text == "key" });
            });
            Check("ordinary-key-input", entered);
            RunOnMainSync(() => { field.IsReadOnly = true; TopLevel.GetTopLevel(field)!.FocusManager!.Focus(null); });
            Check("readonly-focus-accepted", editable.PerformAction(NodeAction.Focus));
            RunOnMainSync(() => { actualFocus = field.IsFocused; field.IsReadOnly = false; });
            Check("readonly-focus-reaches-field", actualFocus);
        }
        using var retired = WaitFor(item => IsId(item, "HomeNameInput"));
        using (var cancel = WaitFor(item => IsId(item, "CancelEdit")))
        {
            if (!cancel.PerformAction(NodeAction.Click)) { throw new InvalidOperationException("Immediate retirement precondition failed."); }
            Check("outgoing-node-rejected-immediately", !SetNativeText(retired, "Rejected during exit"));
        }
        Thread.Sleep(300);
        Click("NavMore");
        Click("OpenHomeSetup");
        using var replacementField = WaitFor(item => IsId(item, "HomeNameInput"));
        Check("replacement-field-native-focus", replacementField.PerformAction(NodeAction.Focus));
        Check("retired-node-rejected", !SetNativeText(retired, "Rejected stale input"));
        Check("retired-node-focus-rejected", !retired.PerformAction(NodeAction.Focus));
        using (var nativeInput = automation.FindFocus(NodeFocus.Input))
        {
            Check("retired-focus-preserves-current-native", nativeInput?.Equals(replacementField) == true);
        }
        var currentFieldFocused = false;
        RunOnMainSync(() => currentFieldFocused = ((Lucia.Companion.App.App)global::Avalonia.Application.Current!).CreateActivityView()
            .GetVisualDescendants().OfType<Lucia.Companion.App.Screens.HomeEditorScreen>().Single().FindControl<TextBox>("HomeNameInput")!.IsFocused);
        Check("retired-focus-preserves-current-field", currentFieldFocused);
        Click("CancelEdit");
        QualifyRepeatedImportFields();
        if (editingChecks.Count < 20) { throw new InvalidOperationException("Editing qualification did not execute all cases."); }
    }

    private void Check(string name, bool passed)
    {
        editingChecks.Add(new { name, passed });
        if (!passed) { throw new InvalidOperationException("Editing qualification failed: " + name); }
    }

    private void QualifyRepeatedImportFields()
    {
        RunOnMainSync(() =>
        {
            var root = (UserControl)((Lucia.Companion.App.App)global::Avalonia.Application.Current!).CreateActivityView();
            var previous = root.Content;
            restoreFixture = () => root.Content = previous;
            var first = new Lucia.Companion.App.Controls.HomeImportPlaceRow(new Lucia.Companion.Connection.HomePlaceCatalog("native-first", "First synthetic place", []), null);
            var second = new Lucia.Companion.App.Controls.HomeImportPlaceRow(new Lucia.Companion.Connection.HomePlaceCatalog("native-second", "Second synthetic place", []), null);
            first.FindControl<global::Avalonia.Controls.CheckBox>("SelectImportPlace")!.IsChecked = true;
            second.FindControl<global::Avalonia.Controls.CheckBox>("SelectImportPlace")!.IsChecked = true;
            root.Content = new StackPanel { Children = { new TextBox { Name = "ForeignText", Text = "Unregistered" }, first, second } };
        });
        using (var unregistered = WaitFor(item => IsId(item, "ForeignText")))
        {
            Check("unregistered-field-rejected", !SetNativeText(unregistered, "Rejected input"));
            using (WaitFor(item => IsId(item, "ForeignText") && item.Text == "Unregistered")) { }
            Check("unregistered-field-unchanged", true);
        }
        using var firstNode = WaitFor(item => IsId(item, "ImportPlaceName") && item.Text == "First synthetic place");
        using var secondNode = WaitFor(item => IsId(item, "ImportPlaceName") && item.Text == "Second synthetic place");
        Check("repeated-field-identities-distinct", firstNode.ViewIdResourceName != secondNode.ViewIdResourceName);
        Check("first-import-replaced", SetNativeText(firstNode, "First replacement"));
        using (WaitFor(item => IsId(item, "ImportPlaceName") && item.Text == "First replacement")) { }
        using (WaitFor(item => IsId(item, "ImportPlaceName") && item.Text == "Second synthetic place")) { }
        Check("second-import-preserved", true);
        Check("second-import-cleared", SetNativeText(secondNode, ""));
        using (WaitFor(item => IsId(item, "ImportPlaceName") && string.IsNullOrEmpty(item.Text))) { }
        Check("empty-import-verified", true);
    }

    private static bool SetNativeText(AccessibilityNodeInfo node, string requested)
    {
        using var arguments = new Bundle();
        using var characters = new Java.Lang.String(requested);
        arguments.PutCharSequence("ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE", characters);
        return node.PerformAction(NodeAction.SetText, arguments);
    }

    private void ReplaceNativeField(string id, string requested)
    {
        using var node = WaitFor(item => IsId(item, id));
        if (!SetNativeText(node, requested)) { throw new InvalidOperationException("Native replacement rejected for " + id); }
        using (WaitFor(item => IsId(item, id) && (item.Text ?? "") == requested)) { }
    }
}
