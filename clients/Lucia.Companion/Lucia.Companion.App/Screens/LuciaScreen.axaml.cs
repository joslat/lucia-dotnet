using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Controls;

namespace Lucia.Companion.App.Screens;

/// <summary>The member's Lucia requests. The demo shows the labelled sample; connected mode never does.</summary>
public sealed partial class LuciaScreen : UserControl
{
    public LuciaScreen() : this(isDemo: true) { }

    public LuciaScreen(bool isDemo)
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<LuciaNow>("DemoActivity")!.IsVisible = isDemo;
        this.FindControl<ConnectedLuciaNow>("ConnectedActivity")!.IsVisible = !isDemo;
    }

    public void SetController(ConnectedActivityController controller, Action reviewConnection) =>
        this.FindControl<ConnectedLuciaNow>("ConnectedActivity")!.SetController(controller, reviewConnection);
}
