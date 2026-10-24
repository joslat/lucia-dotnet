using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Lucia.Companion.App;

public sealed partial class App : Application
{
    private Control? activityView;
    /// <summary>One in-process view for the single Android main activity. Process death still restores saved metadata only.</summary>
    public Control CreateActivityView() => activityView ??= new CompanionShell();
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "Lucia · Offline companion",
                Width = 1100,
                Height = 850,
                MinWidth = 360,
                MinHeight = 500,
                Content = new CompanionShell()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = CreateActivityView;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new CompanionShell();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
