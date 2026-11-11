using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace Lucia.Companion.App.Controls;

/// <summary>Unknown-duration status has no numeric range; its bar and dots are visual paint only.</summary>
internal sealed class OperationProgressAutomationPeer(OperationProgress owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];
}
