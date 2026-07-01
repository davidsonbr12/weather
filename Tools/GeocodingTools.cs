using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace QuickstartWeatherServer.Tools;

[McpServerToolType]
public sealed class GeocodingTools(IHttpClientFactory httpClientFactory)
{
    [McpServerTool, Description(
        "Find geographic coordinates (latitude/longitude), country, timezone, and population for a " +
        "place name such as a city or town. Use the returned coordinates with GetGlobalForecast, " +
        "GetForecast, GetHistoricalWeather, or GetClimateProjection.")]
    public async Task<string> GeocodeLocation(
        [Description("Place name to search for, e.g. 'Seattle' or 'Paris'.")] string name,
        [Description("Maximum number of matches to return (1-10).")] int count = 5)
    {
        count = Math.Clamp(count, 1, 10);
        var client = httpClientFactory.CreateClient("open-meteo-geocoding");
        var url = $"/v1/search?name={Uri.EscapeDataString(name)}&count={count}&language=en&format=json";
        using var doc = await client.ReadJsonDocumentAsync(url);

        if (!doc.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
        {
            return $"No matching locations found for '{name}'.";
        }

        return string.Join("\n--\n", results.EnumerateArray().Select(r =>
        {
            var placeName = r.TryGetProperty("name", out var n) ? n.GetString() : null;
            var admin1 = r.TryGetProperty("admin1", out var a) ? a.GetString() : null;
            var country = r.TryGetProperty("country", out var c) ? c.GetString()
                : r.TryGetProperty("country_code", out var cc) ? cc.GetString() : null;
            var lat = r.GetProperty("latitude").GetDouble();
            var lon = r.GetProperty("longitude").GetDouble();
            var timezone = r.TryGetProperty("timezone", out var tz) ? tz.GetString() : null;
            long? population = r.TryGetProperty("population", out var pop) && pop.ValueKind == JsonValueKind.Number
                ? pop.GetInt64()
                : null;

            var label = string.Join(", ", new[] { placeName, admin1, country }
                .Where(s => !string.IsNullOrEmpty(s)));

            return string.Create(CultureInfo.InvariantCulture, $"""
                    {label}
                    Latitude: {lat}, Longitude: {lon}
                    Timezone: {timezone ?? "n/a"}
                    Population: {(population.HasValue ? population.Value.ToString("N0", CultureInfo.InvariantCulture) : "n/a")}
                    """);
        }));
    }
}
