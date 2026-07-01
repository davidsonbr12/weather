using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace QuickstartWeatherServer;

/// <summary>
/// Registers the named <see cref="HttpClient"/> instances used by the weather/climate tools.
/// Centralized here so the server host and the test project configure identical clients.
/// All data sources are free and require no API key.
/// </summary>
public static class WeatherClients
{
    public static IServiceCollection AddWeatherHttpClients(this IServiceCollection services)
    {
        // US National Weather Service — requires a descriptive User-Agent.
        services.AddHttpClient("nws", client =>
        {
            client.BaseAddress = new Uri("https://api.weather.gov");
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("weather-climate-mcp", "0.1"));
        });

        // Open-Meteo services (global).
        services.AddHttpClient("open-meteo", c => c.BaseAddress = new Uri("https://api.open-meteo.com"));
        services.AddHttpClient("open-meteo-geocoding", c => c.BaseAddress = new Uri("https://geocoding-api.open-meteo.com"));
        services.AddHttpClient("open-meteo-archive", c => c.BaseAddress = new Uri("https://archive-api.open-meteo.com"));
        services.AddHttpClient("open-meteo-climate", c => c.BaseAddress = new Uri("https://climate-api.open-meteo.com"));

        // global-warming.org — global climate-change indicators.
        services.AddHttpClient("global-warming", c => c.BaseAddress = new Uri("https://global-warming.org"));

        return services;
    }
}
