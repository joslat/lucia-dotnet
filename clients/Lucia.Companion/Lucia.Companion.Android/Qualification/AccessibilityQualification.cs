using Android.App;
using Android.AccessibilityServices;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views.Accessibility;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.Text.Json;
using NodeAction = Android.Views.Accessibility.Action;

namespace Lucia.Companion.Android.Qualification;

/// <summary>Opt-in Debug instrumentation. Operates only on the synthetic demo and exercises an invalid draft.</summary>
[Instrumentation(Name = "org.lucia.companion.prototype.AccessibilityQualification", TargetPackage = "org.lucia.companion.prototype", FunctionalTest = true)]
public sealed partial class AccessibilityQualification : Instrumentation
{
    public AccessibilityQualification() { }
    public AccessibilityQualification(nint handle, JniHandleOwnership transfer) : base(handle, transfer) { }

    private readonly List<object> snapshots = [];
    private readonly List<object> events = [];
    private readonly List<object> focusChecks = [];
    private readonly List<object> validationFocusChecks = [];
    private readonly List<AccessibilityNodeInfo> retained = [];
    private readonly object eventGate = new();
    private UiAutomation automation = null!;
    private string stage = "launch";
    private string qualificationStage = "all";
    private AccessibilityNodeInfo? focusTarget;
    private AccessibilityServiceFlags? originalServiceFlags;
    private bool? originalTouchExploration;
    private bool? focusTouchExploration;
    private bool? restoredTouchExploration;
    private System.Action? restoreFixture;

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        qualificationStage = arguments?.GetString("stage") ?? "all";
        Start();
    }

    public override void OnStart()
    {
        base.OnStart();
        string? failure = null;
        try
        {
            automation = GetUiAutomation(UiAutomationFlags.DontSuppressAccessibilityServices)
                ?? throw new InvalidOperationException("UiAutomation unavailable.");
            automation.AccessibilityEvent += Observe;
            using (var manager = TargetContext!.GetSystemService(Context.AccessibilityService) as AccessibilityManager)
            using (var info = automation.ServiceInfo ?? throw new InvalidOperationException("No automation service info."))
            {
                originalTouchExploration = manager?.IsTouchExplorationEnabled;
                originalServiceFlags = info.Flags;
                if (qualificationStage != "motion") { info.Flags |= AccessibilityServiceFlags.RequestTouchExplorationMode; }
                automation.ServiceInfo = info;
                for (var i = 0; qualificationStage != "motion" && i < 20 && manager?.IsTouchExplorationEnabled != true; i++) { Thread.Sleep(50); }
                focusTouchExploration = manager?.IsTouchExplorationEnabled;
            }
            var launch = TargetContext!.PackageManager!.GetLaunchIntentForPackage(TargetContext.PackageName!)
                ?? throw new InvalidOperationException("No demo launcher.");
            launch.AddFlags(ActivityFlags.NewTask);
            StartActivitySync(launch);
            if (qualificationStage == "busy") { QualifyBusyControl(); return; }
            Click("ExploreDemo");
            if (qualificationStage == "motion") { QualifyMotion(); return; }
            ClickWhere(node => node.Clickable && node.ClassName == "PlaceOrb" && node.Text?.StartsWith("Porch", StringComparison.Ordinal) == true);
            Click("PageActionsButton");
            using (WaitFor(node => IsId(node, "OpenButton"))) { }
            Snapshot("bloom-first");
            foreach (var id in new[] { "OpenButton", "HistoryButton", "PinButton", "CloseButton" })
            {
                stage = "focus-" + id;
                using var node = WaitFor(item => IsId(item, id));
                lock (eventGate) { focusTarget = node; }
                var accepted = node.PerformAction(NodeAction.AccessibilityFocus);
                Thread.Sleep(120);
                using var actual = automation.FindFocus(NodeFocus.Accessibility);
                focusChecks.Add(new { id, accepted, sameNativeNode = actual?.Equals(node) == true,
                    actualId = actual?.ViewIdResourceName, actualName = actual?.Text });
                lock (eventGate) { focusTarget = null; }
            }
            Click("CloseButton");
            Click("PageActionsButton");
            using (WaitFor(node => IsId(node, "HistoryButton"))) { }
            Snapshot("bloom-reopened");
            Click("HistoryButton");
            using (WaitFor(node => IsId(node, "DayMode"))) { }
            Snapshot("story-porch");
            ClickWhere(node => node.Clickable && node.Text == "All places");
            Snapshot("story-all");
            Click("NavMore");
            Click("OpenHomeSetup");
            stage = "invalid-place";
            RunOnMainSync(() =>
            {
                var app = (Lucia.Companion.App.App)global::Avalonia.Application.Current!;
                var editor = app.CreateActivityView().GetVisualDescendants().OfType<Lucia.Companion.App.Screens.HomeEditorScreen>().Single();
                editor.FindControl<TextBox>("PlaceNameInput")!.Text = "";
            });
            using (WaitFor(node => IsId(node, "PlaceNameInput") && node.Text is { } value && string.IsNullOrWhiteSpace(value))) { }
            Snapshot("invalid-input-before-save");
            Click("SaveHome");
            using (WaitFor(node => IsId(node, "ErrorText") && node.Text?.Contains("Enter a place name", StringComparison.Ordinal) == true)) { }
            Thread.Sleep(200);
            Snapshot("invalid-place");
            using (WaitFor(node => IsId(node, "PlaceNameInput") && node.ContentDescription?.Contains("Enter a place name.", StringComparison.Ordinal) == true)) { }
            VerifyValidationFocus("PlaceNameInput", "Enter a place name.");
            RunOnMainSync(() =>
            {
                var app = (Lucia.Companion.App.App)global::Avalonia.Application.Current!;
                var editor = app.CreateActivityView().GetVisualDescendants().OfType<Lucia.Companion.App.Screens.HomeEditorScreen>().Single();
                editor.FindControl<TextBox>("PlaceNameInput")!.Text = "Living room";
            });
            Click("AddPlace");
            using (WaitFor(node => IsId(node, "PlaceNameInput") && node.Text == "New place"
                && node.ContentDescription is { } description && description.Contains("this place", StringComparison.Ordinal)
                && !description.Contains("Enter a place name.", StringComparison.Ordinal))) { }
            Snapshot("corrected-place");
            RunOnMainSync(() =>
            {
                var app = (Lucia.Companion.App.App)global::Avalonia.Application.Current!;
                var editor = app.CreateActivityView().GetVisualDescendants().OfType<Lucia.Companion.App.Screens.HomeEditorScreen>().Single();
                editor.FindControl<TextBox>("HomeNameInput")!.Text = "";
            });
            Click("SaveHome");
            using (WaitFor(node => IsId(node, "HomeNameInput") && node.ContentDescription?.Contains("Enter a home name.", StringComparison.Ordinal) == true)) { }
            Snapshot("invalid-home");
            VerifyValidationFocus("HomeNameInput", "Enter a home name.");
            Click("AddPlace");
            using (WaitFor(node => IsId(node, "HomeNameInput") && node.ContentDescription?.Contains("Enter a home name.", StringComparison.Ordinal) == true)) { }
            Snapshot("invalid-home-after-place-edit");
            Click("CancelEdit");
            QualifyBusyControl();
        }
        catch (Exception error)
        {
            failure = error.ToString();
            if (automation is not null) { try { Snapshot("failed-" + stage); } catch (Exception snapshotError) { failure += "\nSnapshot: " + snapshotError.Message; } }
        }
        finally
        {
            if (automation is not null) { automation.AccessibilityEvent -= Observe; }
            if (restoreFixture is not null) { RunOnMainSync(restoreFixture); }
            using var manager = TargetContext?.GetSystemService(Context.AccessibilityService) as AccessibilityManager;
            if (automation is not null && originalServiceFlags is { } flags)
            {
                using var info = automation.ServiceInfo!;
                info.Flags = flags;
                automation.ServiceInfo = info;
                for (var i = 0; i < 20 && manager?.IsTouchExplorationEnabled != originalTouchExploration; i++) { Thread.Sleep(50); }
                restoredTouchExploration = manager?.IsTouchExplorationEnabled;
            }
            object[] observed;
            lock (eventGate) { observed = events.ToArray(); }
            var report = JsonSerializer.Serialize(new { schema = 1, qualificationStage, failure, completed = failure is null,
                scope = "Installed API36 software emulator; Android accessibility actions, not TalkBack traversal or speech",
                dontSuppressAccessibilityServices = true, accessibilityEnabled = manager?.IsEnabled,
                invalidDraftPreparation = qualificationStage == "all" ? "Debug-only UI-thread field seed, verified in the native tree before native Save click. This is not a native text-editing claim." : "Not used in this stage.",
                originalTouchExploration, focusTouchExploration, restoredTouchExploration,
                touchExplorationRestored = restoredTouchExploration == originalTouchExploration,
                snapshots, focusChecks, validationFocusChecks, events = observed, motionSamples, motionMarkers }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(TargetContext!.FilesDir!.AbsolutePath, "accessibility-qualification.json"), report);
            foreach (var node in retained) { node.Dispose(); }
            using var result = new Bundle();
            result.PutString("report", "files/accessibility-qualification.json");
            result.PutString("failure", failure ?? "none");
            Finish(failure is null ? Result.Ok : Result.Canceled, result);
        }
    }

    private void VerifyValidationFocus(string id, string message)
    {
        using var field = WaitFor(node => IsId(node, id));
        var accepted = field.PerformAction(NodeAction.AccessibilityFocus);
        Thread.Sleep(120);
        using var actual = automation.FindFocus(NodeFocus.Accessibility);
        var same = actual?.Equals(field) == true;
        var described = actual?.ContentDescription?.Contains(message, StringComparison.Ordinal) == true;
        validationFocusChecks.Add(new { id, accepted, sameNativeNode = same, errorDescribedOnFocusedNode = described });
        if (!accepted || !same || !described) { throw new InvalidOperationException("Field validation is not available on its native focused node: " + id); }
    }

    private void QualifyBusyControl()
    {
        Lucia.Companion.App.Controls.OperationProgress? progress = null;
        RunOnMainSync(() =>
        {
            var app = (Lucia.Companion.App.App)global::Avalonia.Application.Current!;
            var root = (global::Avalonia.Controls.UserControl)app.CreateActivityView();
            var previous = root.Content;
            restoreFixture = () => root.Content = previous;
            progress = new Lucia.Companion.App.Controls.OperationProgress { IsBusy = true, Name = "QualificationProgress" };
            root.Content = new global::Avalonia.Controls.StackPanel { Margin = new global::Avalonia.Thickness(20), Children =
            { new global::Avalonia.Controls.TextBlock { Text = "Synthetic busy-control qualification" }, progress } };
        });
        using (WaitFor(node => IsId(node, "QualificationProgress"))) { }
        Snapshot("synthetic-operation-busy");
        RunOnMainSync(() => progress!.IsBusy = false);
        Thread.Sleep(150);
        Snapshot("synthetic-operation-complete");
    }

    private void Observe(object? sender, UiAutomation.AccessibilityEventEventArgs args)
    {
        var change = args.Event;
        if (change is null || change.PackageName != TargetContext?.PackageName) { return; }
        using var source = change.Source;
        lock (eventGate)
        {
            events.Add(new { stage, type = change.EventType.ToString(), change.EventTime,
                contentChanges = change.ContentChangeTypes.ToString(), sourceId = source?.ViewIdResourceName,
                sourceName = source?.Text, sourceDescription = source?.ContentDescription,
                sourceAccessibilityFocused = source?.AccessibilityFocused,
                sourceEqualsFocusTarget = focusTarget is not null && source?.Equals(focusTarget) == true,
                text = string.Join(" | ", change.Text ?? []), description = change.ContentDescription });
        }
    }

    private static bool IsId(AccessibilityNodeInfo node, string id) => node.ViewIdResourceName == id
        || node.ViewIdResourceName?.EndsWith("/" + id, StringComparison.Ordinal) == true;

    private void Click(string id) { stage = "locate-" + id; ClickWhere(node => IsId(node, id)); }

    private void ClickWhere(Func<AccessibilityNodeInfo, bool> predicate)
    {
        using var node = WaitFor(item => item.Enabled && predicate(item));
        stage = "click-" + (node.ViewIdResourceName ?? node.Text);
        if (!node.PerformAction(NodeAction.Click)) { throw new InvalidOperationException("Native click rejected: " + stage); }
        Thread.Sleep(300);
    }

    private AccessibilityNodeInfo WaitFor(Func<AccessibilityNodeInfo, bool> predicate)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var tree = ReadTree();
            var found = tree.FirstOrDefault(item => item.Node.VisibleToUser && predicate(item.Node));
            foreach (var item in tree) { if (!ReferenceEquals(item.Node, found.Node)) { item.Node.Dispose(); } }
            if (found.Node is not null) { return found.Node; }
            Thread.Sleep(100);
        }
        throw new InvalidOperationException("Timed out at " + stage);
    }

    private List<(AccessibilityNodeInfo Node, string Path)> ReadTree()
    {
        var tree = new List<(AccessibilityNodeInfo Node, string Path)>();
        if (automation.RootInActiveWindow is { } root) { Visit(root, "0", []); }
        return tree;

        void Visit(AccessibilityNodeInfo node, string path, List<AccessibilityNodeInfo> ancestors)
        {
            if (ancestors.Count > 48 || tree.Count > 2048 || ancestors.Any(parent => parent.Equals(node))) { node.Dispose(); return; }
            tree.Add((node, path));
            var next = new List<AccessibilityNodeInfo>(ancestors) { node };
            for (var i = 0; i < node.ChildCount; i++) { if (node.GetChild(i) is { } child) { Visit(child, path + "/" + i, next); } }
        }
    }

    private void Snapshot(string name)
    {
        stage = name;
        var tree = ReadTree();
        var identities = new List<AccessibilityNodeInfo>();
        var nodes = tree.Select(item =>
        {
            var identity = identities.FindIndex(node => node.Equals(item.Node));
            if (identity < 0) { identity = identities.Count; identities.Add(item.Node); }
            using var bounds = new global::Android.Graphics.Rect();
            item.Node.GetBoundsInScreen(bounds);
            var range = item.Node.GetRangeInfo();
            return new { path = item.Path, identity, id = item.Node.ViewIdResourceName,
                uniqueId = OperatingSystem.IsAndroidVersionAtLeast(33) ? item.Node.UniqueId : null,
                item.Node.WindowId, name = item.Node.Text, description = item.Node.ContentDescription,
                role = item.Node.ClassName, item.Node.Clickable, item.Node.Enabled, item.Node.VisibleToUser,
                item.Node.Selected, item.Node.Focused, item.Node.AccessibilityFocused, item.Node.HintText,
                item.Node.StateDescription, item.Node.Error, item.Node.ContentInvalid,
                range = range is null ? null : new { range.Min, range.Max, range.Current },
                liveRegion = item.Node.LiveRegion.ToString(), bounds = bounds.ToShortString() };
        }).ToArray();
        snapshots.Add(new { name, nodes });
        retained.AddRange(tree.Select(item => item.Node));
    }
}
