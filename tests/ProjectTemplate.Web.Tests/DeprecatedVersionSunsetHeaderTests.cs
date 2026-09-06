using System.Globalization;
using System.Net;
using ProjectTemplate.Web.Options;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the configurable <c>Sunset</c> header advertised for a deprecated API version.
/// </summary>
/// <remarks>
/// A sunset date commits to the callers of a specific deployment about when a version stops being served, so the
/// template ships none. A hard-coded date is arbitrary while it is in the future and misleading once it passes,
/// because a generated application would advertise a removal that already happened.
/// </remarks>
public sealed class DeprecatedVersionSunsetHeaderTests
{
    /// <summary>
    /// Verifies that no sunset date is advertised when the application has not configured one.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DeprecatedVersion_OmitsSunsetHeaderWhenNoDateIsConfigured()
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>());
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/v0.9/application-information", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.False(response.Headers.Contains("Sunset"));
    }

    /// <summary>
    /// Verifies that a configured sunset date is advertised in RFC 1123 form.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DeprecatedVersion_AdvertisesConfiguredSunsetDate()
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>
        {
            [$"{ApplicationApiVersioningOptions.SectionName}:DeprecatedVersionSunset"] = "2099-06-30T23:59:59+00:00"
        });
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/v0.9/application-information", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Sunset"));

        string sunset = Assert.Single(response.Headers.GetValues("Sunset"));
        DateTimeOffset expected = new(2099, 6, 30, 23, 59, 59, TimeSpan.Zero);

        Assert.Equal(expected.ToString("R", CultureInfo.InvariantCulture), sunset);
    }

    /// <summary>
    /// Verifies that the current API version advertises neither deprecation nor a sunset date.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CurrentVersion_AdvertisesNoDeprecationOrSunsetEvenWhenConfigured()
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>
        {
            [$"{ApplicationApiVersioningOptions.SectionName}:DeprecatedVersionSunset"] = "2099-06-30T23:59:59+00:00"
        });
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/v1/application-information", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Deprecation"));
        Assert.False(response.Headers.Contains("Sunset"));
    }

    /// <summary>
    /// Verifies that the shipped default leaves the sunset date unset.
    /// </summary>
    [Fact]
    public void OptionsDefaultLeavesSunsetUnset()
    {
        var options = new ApplicationApiVersioningOptions();

        Assert.Null(options.DeprecatedVersionSunset);
    }
}
