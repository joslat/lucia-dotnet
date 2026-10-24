using lucia.AgentHost.Companion;
using lucia.Agents.Companion;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace lucia.Tests.Auth;

public sealed class CompanionBoundaryTests
{
    [Theory]
    [InlineData(false, "https", 404)]
    [InlineData(true, "http", 400)]
    public async Task DisabledOrInsecureRequestsStopBeforeAuthentication(bool enabled, string scheme, int expected)
    {
        var called = false;
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/companion/homes/home-a/session";
        context.Request.Scheme = scheme;
        context.Response.Body = new MemoryStream();
        await new CompanionBoundaryMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context,
            Options.Create(new CompanionOptions { Enabled = enabled, HomeId = "home-a" }));
        Assert.False(called);
        Assert.Equal(expected, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task NonCompanionPathsAreUnchanged()
    {
        var called = false;
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/config";
        await new CompanionBoundaryMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, Options.Create(new CompanionOptions()));
        Assert.True(called);
        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }
}
