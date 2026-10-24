using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

public sealed class CompanionLayoutTests
{
    [Fact]
    public void LayoutCopiesMetadataAndKeepsConnectionsSeparateFromBindings()
    {
        var places = new List<CompanionLayoutPlace> { new("front", "Front step", "Door", "porch"), new("yard", "Garden", "Garden", "garden") };
        var edges = new List<CompanionLayoutConnection> { new("front", "yard") };
        var bindings = new List<CompanionLayoutBinding> { new("front", "porch", "sensor.porch", CompanionField.Temperature) };
        var layout = new CompanionLayout("home-a", "http://synthetic-ha.invalid", "Our home", places, edges, bindings);
        places.Clear(); edges.Clear(); bindings.Clear();
        Assert.Equal(2, layout.Places.Count); Assert.Single(layout.Connections); Assert.Single(layout.Bindings);
        Assert.Equal("http://synthetic-ha.invalid/", layout.SourceBaseUrl);
        Assert.Throws<NotSupportedException>(() => ((IList<CompanionLayoutPlace>)layout.Places).Clear());
    }

    [Theory]
    [InlineData("duplicate-place")]
    [InlineData("bad-icon")]
    [InlineData("bad-label")]
    [InlineData("invalid-unicode")]
    [InlineData("orphan-edge")]
    [InlineData("self-edge")]
    [InlineData("reverse-duplicate-edge")]
    [InlineData("duplicate-source-role")]
    [InlineData("orphan-binding")]
    [InlineData("wrong-source-place")]
    [InlineData("search-term-as-source")]
    [InlineData("bad-field")]
    [InlineData("too-many-places")]
    [InlineData("too-many-edges")]
    [InlineData("too-many-bindings")]
    public void InvalidMetadataCannotBecomeASharedLayout(string defect)
    {
        var places = new List<CompanionLayoutPlace> { new("front", "Front step", "Door", "porch"), new("yard", "Garden", "Garden", "garden") };
        var edges = new List<CompanionLayoutConnection>();
        var bindings = new List<CompanionLayoutBinding>();
        switch (defect)
        {
            case "duplicate-place": places.Add(places[0]); break;
            case "bad-icon": places[0] = places[0] with { Icon = "<script>" }; break;
            case "bad-label": places[0] = places[0] with { Name = "Front\u202eroom" }; break;
            case "invalid-unicode": places[0] = places[0] with { Name = "Front\ud800room" }; break;
            case "orphan-edge": edges.Add(new("front", "absent")); break;
            case "self-edge": edges.Add(new("front", "front")); break;
            case "reverse-duplicate-edge": edges.Add(new("front", "yard")); edges.Add(new("yard", "front")); break;
            case "duplicate-source-role": bindings.Add(new("front", "porch", "sensor.porch", CompanionField.Temperature)); bindings.Add(bindings[0]); break;
            case "orphan-binding": bindings.Add(new("absent", "porch", "sensor.porch", CompanionField.Temperature)); break;
            case "wrong-source-place": bindings.Add(new("front", "garden", "sensor.porch", CompanionField.Temperature)); break;
            case "search-term-as-source": bindings.Add(new("front", "porch", "front room", CompanionField.Temperature)); break;
            case "bad-field": bindings.Add(new("front", "porch", "sensor.porch", (CompanionField)999)); break;
            case "too-many-places": places.AddRange(Enumerable.Range(0, 201).Select(i => new CompanionLayoutPlace($"p{i}", "Place", "Home", $"area{i}"))); break;
            case "too-many-edges": edges.AddRange(Enumerable.Repeat(new CompanionLayoutConnection("front", "yard"), 1001)); break;
            case "too-many-bindings": bindings.AddRange(Enumerable.Range(0, 2001).Select(i => new CompanionLayoutBinding("front", "porch", $"sensor.s{i}", CompanionField.Temperature))); break;
        }
        Assert.ThrowsAny<ArgumentException>(() => new CompanionLayout("home-a", "http://synthetic-ha.invalid", "Home", places, edges, bindings));
    }

    [Theory]
    [InlineData("home a", "http://synthetic-ha.invalid")]
    [InlineData("home-a", "http://name:secret@synthetic-ha.invalid")]
    [InlineData("home-a", "http://synthetic-ha.invalid/another")]
    public void HomeAndSourceIdentityAreExplicitAndCredentialFree(string home, string source) =>
        Assert.ThrowsAny<ArgumentException>(() => new CompanionLayout(home, source, "Home", [], [], []));
}
