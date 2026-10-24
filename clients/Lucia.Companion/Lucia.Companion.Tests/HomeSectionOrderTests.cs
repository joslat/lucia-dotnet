using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeSectionOrderTests
{
    [Fact]
    public void SectionOrderIsAnExplicitPermutationAndDoesNotChangePlaceOrder()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var places = state.Home.Places.Select(place => place.Id).ToArray();
        var saved = 0;
        state.PreferencesChanged += () => saved++;
        HomeSection[] requested = [HomeSection.LuciaNow, HomeSection.Places, HomeSection.Attention];
        state.SetSectionOrder(requested);
        requested[0] = HomeSection.Attention;
        Assert.Equal(HomeSection.LuciaNow, state.SectionOrder[0]);
        Assert.Equal(places, state.Home.Places.Select(place => place.Id));
        Assert.Equal(1, saved);
        state.SetSectionOrder(state.SectionOrder);
        Assert.Equal(1, saved);
        Assert.Throws<ArgumentException>(() => state.SetSectionOrder([HomeSection.Places, HomeSection.Places, HomeSection.Attention]));
        Assert.Throws<ArgumentException>(() => state.SetSectionOrder([HomeSection.Places]));
        Assert.Throws<ArgumentException>(() => state.SetSectionOrder([HomeSection.Places, HomeSection.Attention, (HomeSection)99]));
        Assert.Equal(HomeSection.LuciaNow, state.SectionOrder[0]);
        state.SetSectionOrder(HomeSections.DefaultOrder);
        Assert.Equal(2, saved);
    }

    [Fact]
    public async Task LocalPreferenceSurvivesRestartAndLegacyFilesDefaultWithoutAutosaving()
    {
        var file = Path.Combine(Path.GetTempPath(), $"lucia-sections-{Guid.NewGuid():N}.json");
        try
        {
            var store = new LocalHomeStore(file);
            await store.SaveAsync(HomeProfile.From(SampleHome.Create(), []), null, TestContext.Current.CancellationToken);
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken))!.AsObject();
            legacy.Remove("SectionOrder");
            await File.WriteAllTextAsync(file, legacy.ToJsonString(), TestContext.Current.CancellationToken);
            var state = new HomeViewModel(SampleHome.Create());
            var session = new HomeProfileSession(state, store);
            await session.InitializeAsync(TestContext.Current.CancellationToken);
            Assert.Equal(HomeSections.DefaultOrder, state.SectionOrder);
            Assert.Equal(Task.CompletedTask, session.PendingSave);
            state.SetSectionOrder([HomeSection.Attention, HomeSection.Places, HomeSection.LuciaNow]);
            await session.PendingSave;
            var restored = new HomeViewModel(SampleHome.Create());
            await new HomeProfileSession(restored, store).InitializeAsync(TestContext.Current.CancellationToken);
            Assert.Equal(state.SectionOrder, restored.SectionOrder);
            var profile = Assert.IsType<HomeProfile>(await store.LoadAsync(TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentException>(() => (profile with { SectionOrder = [HomeSection.Places] }).Validate());
        }
        finally { if (File.Exists(file)) { File.Delete(file); } if (File.Exists(file + ".lock")) { File.Delete(file + ".lock"); } }
    }
}
