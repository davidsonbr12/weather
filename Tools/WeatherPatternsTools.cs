using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace QuickstartWeatherServer.Tools;

[McpServerToolType]
public sealed class WeatherPatternsTools(IHttpClientFactory httpClientFactory)
{
    [McpServerTool, Description(
        "Get current conditions and a multi-day daily forecast for any location worldwide (Open-Meteo). " +
        "Works outside the United States, unlike GetForecast.")]
    public async Task<string> GetGlobalForecast(
        [Description("Latitude of the location.")] double latitude,
        [Description("Longitude of the location.")] double longitude,
        [Description("Number of forecast days (1-16).")] int days = 7)
    {
        days = Math.Clamp(days, 1, 16);
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        var client = httpClientFactory.CreateClient("open-meteo");
        var url =
            $"/v1/forecast?latitude={lat}&longitude={lon}" +
            "&current=temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,weather_code,wind_speed_10m,wind_direction_10m" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max" +
            $"&timezone=auto&forecast_days={days}";
        using var doc = await client.ReadJsonDocumentAsync(url);
        var root = doc.RootElement;
        var sb = new StringBuilder();

        if (root.TryGetProperty("current", out var current) &&
            root.TryGetProperty("current_units", out var cu))
        {
            double N(string p) => current.TryGetProperty(p, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number
                ? v.GetDouble()
                : double.NaN;
            int code = current.TryGetProperty("weather_code", out var wc) && wc.ValueKind == System.Text.Json.JsonValueKind.Number
                ? wc.GetInt32()
                : -1;

            sb.AppendLine("Current conditions:");
            sb.AppendLine($"  {OpenMeteo.WeatherCodeDescription(code)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Temperature: {N("temperature_2m")}{OpenMeteo.Unit(cu, "temperature_2m")} (feels like {N("apparent_temperature")}{OpenMeteo.Unit(cu, "apparent_temperature")})");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Humidity: {N("relative_humidity_2m")}{OpenMeteo.Unit(cu, "relative_humidity_2m")}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Precipitation: {N("precipitation")}{OpenMeteo.Unit(cu, "precipitation")}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Wind: {N("wind_speed_10m")}{OpenMeteo.Unit(cu, "wind_speed_10m")} from {N("wind_direction_10m")}{OpenMeteo.Unit(cu, "wind_direction_10m")}");
            sb.AppendLine();
        }

        var daily = root.GetProperty("daily");
        var du = root.GetProperty("daily_units");
        var dates = OpenMeteo.ReadStrings(daily, "time");
        var codes = OpenMeteo.ReadNullableDoubles(daily, "weather_code");
        var tMax = OpenMeteo.ReadNullableDoubles(daily, "temperature_2m_max");
        var tMin = OpenMeteo.ReadNullableDoubles(daily, "temperature_2m_min");
        var precip = OpenMeteo.ReadNullableDoubles(daily, "precipitation_sum");
        var pprob = OpenMeteo.ReadNullableDoubles(daily, "precipitation_probability_max");
        var tUnit = OpenMeteo.Unit(du, "temperature_2m_max");
        var pUnit = OpenMeteo.Unit(du, "precipitation_sum");

        sb.AppendLine("Daily forecast:");
        for (int i = 0; i < dates.Count; i++)
        {
            int code = i < codes.Count && codes[i].HasValue ? (int)codes[i]!.Value : -1;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {dates[i]}: {OpenMeteo.WeatherCodeDescription(code)}; high {OpenMeteo.Fmt(tMax.ElementAtOrDefault(i))}{tUnit}, low {OpenMeteo.Fmt(tMin.ElementAtOrDefault(i))}{tUnit}, precip {OpenMeteo.Fmt(precip.ElementAtOrDefault(i))}{pUnit} ({OpenMeteo.Fmt(pprob.ElementAtOrDefault(i))}% chance)");
        }

        return sb.ToString().TrimEnd();
    }

    [McpServerTool, Description(
        "Get historical daily weather (max/min temperature and total precipitation) for a location and " +
        "date range, with a summary of averages and extremes. Useful for questions about past weather and " +
        "long-term patterns. Data is available from 1940 up to about 5 days ago, worldwide.")]
    public async Task<string> GetHistoricalWeather(
        [Description("Latitude of the location.")] double latitude,
        [Description("Longitude of the location.")] double longitude,
        [Description("Start date in YYYY-MM-DD format.")] string startDate,
        [Description("End date in YYYY-MM-DD format.")] string endDate)
    {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        var client = httpClientFactory.CreateClient("open-meteo-archive");
        var url =
            $"/v1/archive?latitude={lat}&longitude={lon}&start_date={startDate}&end_date={endDate}" +
            "&daily=temperature_2m_max,temperature_2m_min,precipitation_sum&timezone=auto";
        using var doc = await client.ReadJsonDocumentAsync(url);
        var root = doc.RootElement;
        var daily = root.GetProperty("daily");
        var units = root.GetProperty("daily_units");

        var dates = OpenMeteo.ReadStrings(daily, "time");
        var tMax = OpenMeteo.ReadNullableDoubles(daily, "temperature_2m_max");
        var tMin = OpenMeteo.ReadNullableDoubles(daily, "temperature_2m_min");
        var precip = OpenMeteo.ReadNullableDoubles(daily, "precipitation_sum");

        if (dates.Count == 0)
        {
            return "No historical data available for that location and date range.";
        }

        var tUnit = OpenMeteo.Unit(units, "temperature_2m_max");
        var pUnit = OpenMeteo.Unit(units, "precipitation_sum");

        var highs = tMax.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var lows = tMin.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var rain = precip.Where(v => v.HasValue).Select(v => v!.Value).ToList();

        (string date, double value)? Extreme(List<double?> values, bool max)
        {
            int bestIdx = -1;
            double best = max ? double.MinValue : double.MaxValue;
            for (int i = 0; i < values.Count && i < dates.Count; i++)
            {
                if (!values[i].HasValue) continue;
                var v = values[i]!.Value;
                if ((max && v > best) || (!max && v < best))
                {
                    best = v;
                    bestIdx = i;
                }
            }
            return bestIdx >= 0 ? (dates[bestIdx], best) : null;
        }

        var hottest = Extreme(tMax, max: true);
        var coldest = Extreme(tMin, max: false);
        var wettest = Extreme(precip, max: true);

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Historical weather for {lat},{lon} ({dates[0]} to {dates[^1]})");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Days with data: {dates.Count}");
        if (highs.Count > 0) sb.AppendLine(CultureInfo.InvariantCulture, $"Average daily high: {highs.Average():F1}{tUnit}");
        if (lows.Count > 0) sb.AppendLine(CultureInfo.InvariantCulture, $"Average daily low: {lows.Average():F1}{tUnit}");
        if (hottest is { } h) sb.AppendLine(CultureInfo.InvariantCulture, $"Hottest day: {h.value:F1}{tUnit} on {h.date}");
        if (coldest is { } c) sb.AppendLine(CultureInfo.InvariantCulture, $"Coldest day: {c.value:F1}{tUnit} on {c.date}");
        if (rain.Count > 0) sb.AppendLine(CultureInfo.InvariantCulture, $"Total precipitation: {rain.Sum():F1}{pUnit}");
        if (wettest is { } w) sb.AppendLine(CultureInfo.InvariantCulture, $"Wettest day: {w.value:F1}{pUnit} on {w.date}");

        // Include the day-by-day detail only for short ranges to keep output readable.
        if (dates.Count <= 31)
        {
            sb.AppendLine();
            sb.AppendLine("Daily detail:");
            for (int i = 0; i < dates.Count; i++)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"  {dates[i]}: high {OpenMeteo.Fmt(tMax.ElementAtOrDefault(i))}{tUnit}, low {OpenMeteo.Fmt(tMin.ElementAtOrDefault(i))}{tUnit}, precip {OpenMeteo.Fmt(precip.ElementAtOrDefault(i))}{pUnit}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
