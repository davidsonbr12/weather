using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace QuickstartWeatherServer.Tools;

[McpServerToolType]
public sealed class ClimateChangeTools(IHttpClientFactory httpClientFactory)
{
    [McpServerTool, Description(
        "Get the trend in global average temperature anomaly in degrees Celsius (relative to the " +
        "1951-1980 average), summarized by decade. Based on NASA GISTEMP land-ocean data via " +
        "global-warming.org. Answers questions about how much the planet has warmed.")]
    public async Task<string> GetGlobalTemperatureAnomaly(
        [Description("Optional: only include data from this year onward (e.g. 1950). Omit for the full record since 1880.")] int? sinceYear = null)
    {
        var client = httpClientFactory.CreateClient("global-warming");
        using var doc = await client.ReadJsonDocumentAsync("/api/temperature-api");

        var points = doc.RootElement.GetProperty("result").EnumerateArray()
            .Select(e => (
                year: OpenMeteo.ParseDouble(e.TryGetProperty("time", out var t) ? t.GetString() : null),
                anomaly: OpenMeteo.ParseDouble(e.TryGetProperty("land", out var l) ? l.GetString() : null)))
            .Where(x => x.year.HasValue && x.anomaly.HasValue)
            .Select(x => (year: x.year!.Value, anomaly: x.anomaly!.Value))
            .ToList();

        if (sinceYear.HasValue)
        {
            points = points.Where(x => x.year >= sinceYear.Value).ToList();
        }

        if (points.Count == 0)
        {
            return "No temperature anomaly data available for the requested range.";
        }

        var first = points[0];
        var last = points[^1];

        var sb = new StringBuilder();
        sb.AppendLine("Global temperature anomaly (\u00b0C vs. the 1951-1980 average), NASA GISTEMP land-ocean index:");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Record: {first.year:F0} to {last.year:F0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Earliest ({first.year:F0}): {first.anomaly:+0.00;-0.00}\u00b0C");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Latest ({last.year:F0}): {last.anomaly:+0.00;-0.00}\u00b0C");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Net change: {(last.anomaly - first.anomaly):+0.00;-0.00}\u00b0C");
        sb.AppendLine();
        sb.AppendLine("Average anomaly by decade:");
        foreach (var g in points.GroupBy(p => (int)Math.Floor(p.year / 10) * 10).OrderBy(g => g.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {g.Key}s: {g.Average(p => p.anomaly):+0.00;-0.00}\u00b0C");
        }

        return sb.ToString().TrimEnd();
    }

    [McpServerTool, Description(
        "Get the latest atmospheric CO2 concentration in parts per million (ppm) and its recent trend. " +
        "Based on NOAA Mauna Loa data via global-warming.org.")]
    public async Task<string> GetAtmosphericCo2()
    {
        var client = httpClientFactory.CreateClient("global-warming");
        using var doc = await client.ReadJsonDocumentAsync("/api/co2-api");

        var entries = doc.RootElement.GetProperty("co2").EnumerateArray().ToList();
        if (entries.Count == 0)
        {
            return "No atmospheric CO2 data available.";
        }

        double? Trend(JsonElement e) => OpenMeteo.ParseDouble(e.TryGetProperty("trend", out var t) ? t.GetString() : null);
        double? Cycle(JsonElement e) => OpenMeteo.ParseDouble(e.TryGetProperty("cycle", out var c) ? c.GetString() : null);
        string Date(JsonElement e)
        {
            string P(string k) => e.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
            return $"{P("year")}-{P("month").PadLeft(2, '0')}-{P("day").PadLeft(2, '0')}";
        }

        var latest = entries[^1];
        var latestTrend = Trend(latest);
        var latestCycle = Cycle(latest);

        // The series is roughly daily, so ~365 entries back approximates one year ago.
        var yearAgo = entries.Count > 365 ? entries[^366] : entries[0];
        var yearAgoTrend = Trend(yearAgo);

        const double preIndustrial = 280.0;

        var sb = new StringBuilder();
        sb.AppendLine("Atmospheric CO2 (NOAA, via global-warming.org):");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Latest reading ({Date(latest)}):");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Trend (de-seasonalized): {OpenMeteo.Fmt(latestTrend, "0.00")} ppm");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Seasonal value: {OpenMeteo.Fmt(latestCycle, "0.00")} ppm");
        if (latestTrend.HasValue && yearAgoTrend.HasValue)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Change over ~1 year: {(latestTrend.Value - yearAgoTrend.Value):+0.00;-0.00} ppm");
        }
        if (latestTrend.HasValue)
        {
            var pct = (latestTrend.Value - preIndustrial) / preIndustrial * 100.0;
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Vs. pre-industrial (~280 ppm): +{latestTrend.Value - preIndustrial:0.0} ppm (+{pct:0.0}%)");
        }

        return sb.ToString().TrimEnd();
    }

    [McpServerTool, Description(
        "Get a location's projected temperature trend under climate change, summarized by decade. Uses " +
        "downscaled CMIP6 climate-model data (Open-Meteo, model MRI_AGCM3_2_S) for years 1950-2050. " +
        "Answers questions about future local climate.")]
    public async Task<string> GetClimateProjection(
        [Description("Latitude of the location.")] double latitude,
        [Description("Longitude of the location.")] double longitude,
        [Description("Start year (1950-2050).")] int startYear = 1990,
        [Description("End year (1950-2050).")] int endYear = 2050)
    {
        startYear = Math.Clamp(startYear, 1950, 2050);
        endYear = Math.Clamp(endYear, startYear, 2050);

        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        var client = httpClientFactory.CreateClient("open-meteo-climate");
        var url =
            $"/v1/climate?latitude={lat}&longitude={lon}&start_date={startYear}-01-01&end_date={endYear}-12-31" +
            "&models=MRI_AGCM3_2_S&daily=temperature_2m_max,temperature_2m_min";
        using var doc = await client.ReadJsonDocumentAsync(url);
        var root = doc.RootElement;
        var daily = root.GetProperty("daily");
        var units = root.GetProperty("daily_units");

        var dates = OpenMeteo.ReadStrings(daily, "time");
        var tMax = OpenMeteo.ReadNullableDoubles(daily, "temperature_2m_max");
        var tMin = OpenMeteo.ReadNullableDoubles(daily, "temperature_2m_min");

        if (dates.Count == 0)
        {
            return "No climate projection data available for that location.";
        }

        var tUnit = OpenMeteo.Unit(units, "temperature_2m_max");

        // Aggregate daily mean temperature ((max + min) / 2) into annual means.
        var annual = new SortedDictionary<int, (double sum, int count)>();
        for (int i = 0; i < dates.Count; i++)
        {
            if (dates[i].Length < 4 || !int.TryParse(dates[i].AsSpan(0, 4), out var year)) continue;
            var mx = tMax.ElementAtOrDefault(i);
            var mn = tMin.ElementAtOrDefault(i);
            if (!mx.HasValue || !mn.HasValue) continue;
            var mean = (mx.Value + mn.Value) / 2.0;
            var cur = annual.TryGetValue(year, out var v) ? v : (sum: 0.0, count: 0);
            annual[year] = (cur.sum + mean, cur.count + 1);
        }

        // Keep only near-complete years so partial years don't skew the trend.
        var annualMeans = annual
            .Where(kv => kv.Value.count >= 300)
            .ToDictionary(kv => kv.Key, kv => kv.Value.sum / kv.Value.count);

        if (annualMeans.Count == 0)
        {
            return "No complete annual climate data available for that location and range.";
        }

        var firstYear = annualMeans.Keys.Min();
        var lastYear = annualMeans.Keys.Max();

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Projected annual mean temperature for {lat},{lon} (CMIP6 MRI_AGCM3_2_S):");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Range: {firstYear} to {lastYear}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{firstYear}: {annualMeans[firstYear]:F1}{tUnit}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{lastYear}: {annualMeans[lastYear]:F1}{tUnit}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Projected change: {(annualMeans[lastYear] - annualMeans[firstYear]):+0.0;-0.0}{tUnit}");
        sb.AppendLine();
        sb.AppendLine("Average by decade:");
        foreach (var g in annualMeans.GroupBy(kv => (kv.Key / 10) * 10).OrderBy(g => g.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {g.Key}s: {g.Average(kv => kv.Value):F1}{tUnit}");
        }

        return sb.ToString().TrimEnd();
    }
}
