using Microsoft.Extensions.Configuration;
using TodoApp.Application;

namespace TodoApp.Tests;

public sealed class SeoTests
{
    [Fact]
    public void Robots_BlocksPrivateAreas_AndPointsToSitemap()
    {
        var robots = SeoDocuments.Robots("https://example.com");

        Assert.Contains("Disallow: /list/", robots);
        Assert.Contains("Disallow: /api/", robots);
        Assert.Contains("Sitemap: https://example.com/sitemap.xml", robots);
    }

    [Fact]
    public void Sitemap_ListsOnlyPublicPages()
    {
        var sitemap = SeoDocuments.Sitemap("https://example.com");

        Assert.Contains("<loc>https://example.com/</loc>", sitemap);
        Assert.Contains("<loc>https://example.com/faq</loc>", sitemap);
        Assert.Contains("<loc>https://example.com/privacy</loc>", sitemap);
        Assert.DoesNotContain("/list/", sitemap);
        Assert.DoesNotContain("/api/", sitemap);
    }

    [Fact]
    public void AppInfo_UsesDefaults_WhenNothingIsConfigured()
    {
        var info = new AppInfo(Config([]));

        Assert.Equal("TodoApp", info.Name);
        Assert.False(info.LegalConfigured);
        Assert.Null(info.Analytics);
    }

    [Theory]
    [InlineData("Plausible", "data-domain", "https://plausible.io/js/script.js")]
    [InlineData("umami", "data-website-id", "https://cloud.umami.is/script.js")]
    public void AppInfo_BuildsAnalyticsScript_ForSupportedProviders(string provider, string attribute, string src)
    {
        var info = new AppInfo(Config(new() { ["Analytics:Provider"] = provider, ["Analytics:SiteId"] = "abc" }));

        var script = Assert.IsType<AnalyticsScript>(info.Analytics);
        Assert.Equal(attribute, script.IdAttribute);
        Assert.Equal(src, script.Src);
        Assert.Equal("abc", script.Id);
    }

    [Fact]
    public void AppInfo_IgnoresAnalytics_ForUnknownProviderOrMissingId()
    {
        Assert.Null(new AppInfo(Config(new() { ["Analytics:Provider"] = "other", ["Analytics:SiteId"] = "abc" })).Analytics);
        Assert.Null(new AppInfo(Config(new() { ["Analytics:Provider"] = "plausible", ["Analytics:SiteId"] = " " })).Analytics);
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
