using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lucia.Companion.Connection;

namespace Lucia.Companion.App;

public sealed partial class App : Application
{
    private Control? activityView;
    public static Func<ICompanionConnection>? ConnectionFactory { get; set; }
    /// <summary>One in-process view for the single Android main activity. Process death revalidates protected access before any demo entry.</summary>
    public Control CreateActivityView() => activityView ??= CreateRoot();
    public void SuspendConnectionPolling() { if (activityView is CompanionRoot root) { root.SuspendConnectionPolling(); } }
    public void VerifyConnectionForeground() { if (activityView is CompanionRoot root) { _ = root.VerifyForegroundAsync(); } }
    private static CompanionRoot CreateRoot()
    {
        ICompanionConnection? connection;
        try { connection = ConnectionFactory?.Invoke(); }
        catch (Exception) { connection = null; } // Unavailable protected storage never becomes plaintext or automatic demo.
        return new CompanionRoot(connection);
    }
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "Lucia · Companion",
                Width = 1100,
                Height = 850,
                MinWidth = 360,
                MinHeight = 500,
                Content = CreateRoot()
            };
            desktop.MainWindow.Deactivated += (_, _) => ((CompanionRoot)desktop.MainWindow.Content!).SuspendConnectionPolling();
            desktop.MainWindow.Activated += async (_, _) => await ((CompanionRoot)desktop.MainWindow.Content!).VerifyForegroundAsync();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = CreateActivityView;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = CreateRoot();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
