using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

public sealed class CompanionGrantTests
{
    [Fact]
    public void GrantAllowsOnlyItsExactHomePlaceAndFields()
    {
        var grant = new CompanionGrant("home-a", "member-a", [new("living", [CompanionField.Temperature])]);
        Assert.True(grant.Allows("home-a", "living", CompanionField.Temperature));
        Assert.False(grant.Allows("home-b", "living", CompanionField.Temperature));
        Assert.False(grant.Allows("HOME-A", "living", CompanionField.Temperature));
        Assert.False(grant.Allows("home-a", "bedroom", CompanionField.Temperature));
        Assert.False(grant.Allows("home-a", "living", CompanionField.Contact));
        Assert.False(grant.Allows("home-a", "living", (CompanionField)999));
        Assert.False(grant.CanReadActivity);
        Assert.False(grant.CanReadHistory);
        Assert.False(grant.CanReadEngine);
    }

    [Fact]
    public void GrantsSnapshotInputsAndRejectWildcardDuplicateOrUnknownAuthority()
    {
        var fields = new List<CompanionField> { CompanionField.Temperature };
        var places = new List<CompanionPlaceGrant> { new("living", fields) };
        var grant = new CompanionGrant("home-a", "member-a", places);
        fields.Add(CompanionField.Contact);
        places.Clear();
        Assert.True(grant.Allows("home-a", "living", CompanionField.Temperature));
        Assert.False(grant.Allows("home-a", "living", CompanionField.Contact));
        Assert.Throws<ArgumentException>(() => new CompanionGrant("*", "member-a", []));
        Assert.Throws<ArgumentException>(() => new CompanionPlaceGrant("living", [(CompanionField)999]));
        Assert.Throws<ArgumentException>(() => new CompanionPlaceGrant("living", [CompanionField.Light, CompanionField.Light]));
        Assert.Throws<ArgumentException>(() => new CompanionGrant("home-a", "member-a", [new("living", []), new("living", [])]));
    }
}
