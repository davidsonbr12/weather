using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using QuickstartWeatherServer;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new Implementation
    {
        Name = "weather-climate",
        Version = "0.1.0"
    };
    options.ServerInstructions =
        "Answers basic questions about weather, weather patterns (historical), and climate change data. " +
        "Use geocode_location to turn a place name into latitude/longitude, then pass those coordinates to " +
        "the forecast, historical-weather, or climate-projection tools. get_forecast and get_alerts use the " +
        "US National Weather Service (United States only); get_global_forecast and the historical/climate tools " +
        "work worldwide. Global temperature anomaly and atmospheric CO2 are global indicators.";
})
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Register named HTTP clients for every data source (see WeatherClients.cs).
builder.Services.AddWeatherHttpClients();

var app = builder.Build();

await app.RunAsync();
