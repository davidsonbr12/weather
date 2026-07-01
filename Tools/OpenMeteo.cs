using System.Globalization;
using System.Text.Json;

namespace QuickstartWeatherServer.Tools;

/// <summary>
/// Shared helpers for parsing Open-Meteo style JSON responses (parallel "daily" arrays)
/// and for formatting numeric values consistently.
/// </summary>
internal static class OpenMeteo
{
    /// <summary>Reads a numeric JSON array into a list, mapping nulls/missing to null.</summary>
    public static List<double?> ReadNullableDoubles(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return new List<double?>();
        }

        return arr.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : (double?)null)
            .ToList();
    }

    /// <summary>Reads a string JSON array (e.g. the "time" axis) into a list.</summary>
    public static List<string> ReadStrings(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return arr.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList();
    }

    /// <summary>Reads a unit label from a "*_units" object.</summary>
    public static string Unit(JsonElement units, string property) =>
        units.TryGetProperty(property, out var u) ? u.GetString() ?? string.Empty : string.Empty;

    /// <summary>Formats a nullable double, or "n/a" when null.</summary>
    public static string Fmt(double? value, string format = "0.#") =>
        value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : "n/a";

    /// <summary>Parses a decimal string using invariant culture.</summary>
    public static double? ParseDouble(string? s) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>Maps a WMO weather interpretation code to a short human-readable description.</summary>
    public static string WeatherCodeDescription(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 => "Fog",
        48 => "Depositing rime fog",
        51 => "Light drizzle",
        53 => "Moderate drizzle",
        55 => "Dense drizzle",
        56 => "Light freezing drizzle",
        57 => "Dense freezing drizzle",
        61 => "Slight rain",
        63 => "Moderate rain",
        65 => "Heavy rain",
        66 => "Light freezing rain",
        67 => "Heavy freezing rain",
        71 => "Slight snowfall",
        73 => "Moderate snowfall",
        75 => "Heavy snowfall",
        77 => "Snow grains",
        80 => "Slight rain showers",
        81 => "Moderate rain showers",
        82 => "Violent rain showers",
        85 => "Slight snow showers",
        86 => "Heavy snow showers",
        95 => "Thunderstorm",
        96 => "Thunderstorm with slight hail",
        99 => "Thunderstorm with heavy hail",
        _ => code < 0 ? "Unknown" : $"Weather code {code}"
    };
}
