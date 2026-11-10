using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lucia.Companion.App.Screens;

/// <summary>Connected Story until household history is available. Never falls back to the sample day.</summary>
public sealed partial class HistoryPendingScreen : UserControl
{
    public HistoryPendingScreen() => AvaloniaXamlLoader.Load(this);
}
