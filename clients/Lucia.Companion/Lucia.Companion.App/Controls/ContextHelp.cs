using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lucia.Companion.App.Controls;

/// <summary>Supplementary pointer help, also readable by automation. Essential instructions stay in the screen.</summary>
public static class ContextHelp
{
    public static void Set(Control control, string text)
    {
        ToolTip.SetTip(control, new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 300 });
        ToolTip.SetShowDelay(control, 650);
        ToolTip.SetBetweenShowDelay(control, 350);
        AutomationProperties.SetHelpText(control, text);
    }
}
