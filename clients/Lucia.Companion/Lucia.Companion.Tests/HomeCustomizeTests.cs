using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeCustomizeTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false, 1.0)]
    [InlineData(390, 844, true, 1.0)]
    [InlineData(1194, 834, false, 1.0)]
    [InlineData(1194, 834, true, 1.0)]
    [InlineData(390, 844, false, 2.0)]
    [InlineData(390, 844, true, 2.0)]
    [InlineData(1194, 834, false, 2.0)]
    [InlineData(1194, 834, true, 2.0)]
    public void NamedSectionMovesPersistOneOrderAcrossResponsiveComposition(int width, int height, bool dark, double scale)
    {
        var theme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var view = new HomeScreen();
        var window = new Window { Width = width, Height = height, Content = view };
        try
        {
            window.Show();
            Lucia.Companion.App.CompanionTypography.Apply(size => size * scale);
            view.OpenCustomization();
            Dispatcher.UIThread.RunJobs();
            var order = view.FindControl<HomeSectionOrder>("SectionOrderEditor")!;
            Assert.False(order.FindControl<Button>("MovePlacesEarlier")!.IsEnabled);
            order.FindControl<Button>("MoveLuciaNowEarlier")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            order.FindControl<Button>("MoveLuciaNowEarlier")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(HomeSection.LuciaNow, view.State.SectionOrder[0]);
            var main = view.FindControl<StackPanel>("MainPanel")!;
            var side = view.FindControl<StackPanel>("SidePanel")!;
            Assert.Equal("LuciaCard", main.Children[0].Name);
            Assert.Equal("PlacesCard", main.Children[1].Name);
            if (width >= 900) { Assert.Equal("AttentionCard", side.Children[0].Name); }
            Assert.Equal(width >= 900 ? 2 : 3, main.Children.Count);
            Dispatcher.UIThread.RunJobs();
            var focused = order.FindControl<Button>("MoveLuciaNowLater")!;
            var position = focused.TranslatePoint(new Point(0, focused.Bounds.Height), window);
            Assert.NotNull(position);
            Assert.InRange(position.Value.Y, 0, height);
            var resetCaption = Assert.IsType<TextBlock>(order.FindControl<Button>("RestoreSectionOrder")!.Content);
            Assert.Equal(Avalonia.Media.TextWrapping.Wrap, resetCaption.TextWrapping);
            NativeCapture.Save(window, $"customize-{width}-{(dark ? "dark" : "light")}-{scale:0.0}.png");
            order.FindControl<Button>("RestoreSectionOrder")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(HomeSections.DefaultOrder, view.State.SectionOrder);
            Assert.Equal("PlacesCard", main.Children[0].Name);
            Assert.True(view.TryNavigateBack());
            Assert.False(view.FindControl<Expander>("CustomizeHome")!.IsExpanded);
            Assert.False(view.TryNavigateBack());
            window.Width = width >= 900 ? 390 : 1194;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(HomeSections.DefaultOrder, view.State.SectionOrder);
            Assert.Equal("PlacesCard", main.Children[0].Name);
        }
        finally { Lucia.Companion.App.CompanionTypography.Apply(size => size); window.Close(); Application.Current.RequestedThemeVariant = theme; }
    }
}
