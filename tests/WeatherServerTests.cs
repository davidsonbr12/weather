using Microsoft.Extensions.DependencyInjection;
using QuickstartWeatherServer;
using QuickstartWeatherServer.Tools;

namespace weather.Tests;

/// <summary>
/// Builds the same named HTTP clients the server uses, so tools can be exercised directly.
/// </summary>
public sealed class ToolFixture
{
    public IHttpClientFactory Factory { get; }

    public ToolFixture()
    {
        var services = new ServiceCollection();
        services.AddWeatherHttpClients();
        Factory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }
}

/// <summary>
/// Integration smoke tests: each test performs a live request against a free API and
/// asserts the tool returns well-formed output. Requires internet access.
/// </summary>
public sealed class WeatherServerTests(ToolFixture fixture) : IClassFixture<ToolFixture>
{
    private readonly IHttpClientFactory _factory = fixture.Factory;

    // Seattle, WA — used across several tests.
    private const double Lat = 47.6062;
    private const double Lon = -122.3321;

    [Fact]
    public async Task GeocodeLocation_ReturnsCoordinates()
    {
        var result = await new GeocodingTools(_factory).GeocodeLocation("Seattle", 1);
        Assert.Contains("Seattle", result);
        Assert.Contains("Latitude", result);
    }

    [Fact]
    public async Task GetGlobalForecast_ReturnsDailyForecast()
    {
        var result = await new WeatherPatternsTools(_factory).GetGlobalForecast(Lat, Lon, 3);
        Assert.Contains("Daily forecast", result);
    }

    [Fact]
    public async Task GetHistoricalWeather_ReturnsSummary()
    {
        var result = await new WeatherPatternsTools(_factory)
            .GetHistoricalWeather(Lat, Lon, "2020-07-01", "2020-07-07");
        Assert.Contains("Historical weather", result);
        Assert.Contains("Average daily high", result);
    }

    [Fact]
    public async Task GetAtmosphericCo2_ReturnsPpm()
    {
        var result = await new ClimateChangeTools(_factory).GetAtmosphericCo2();
        Assert.Contains("ppm", result);
    }

    [Fact]
    public async Task GetGlobalTemperatureAnomaly_ReturnsDecadeTrend()
    {
        var result = await new ClimateChangeTools(_factory).GetGlobalTemperatureAnomaly(1950);
        Assert.Contains("anomaly", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("decade", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetClimateProjection_ReturnsProjection()
    {
        var result = await new ClimateChangeTools(_factory).GetClimateProjection(Lat, Lon, 2020, 2025);
        Assert.Contains("Projected", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAlerts_Runs()
    {
        var result = await new WeatherTools(_factory).GetAlerts("WA");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public async Task GetForecast_Runs()
    {
        var result = await new WeatherTools(_factory).GetForecast(Lat, Lon);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
