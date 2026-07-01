using System.Text.Json;
using ModelContextProtocol;

internal static class HttpClientExt
{
    public static async Task<JsonDocument> ReadJsonDocumentAsync(this HttpClient client, string requestUri)
    {
        using var response = await client.GetAsync(requestUri);
        if (!response.IsSuccessStatusCode)
        {
            // Many JSON APIs (e.g. Open-Meteo) return a helpful {"reason": "..."} body on error.
            var body = await response.Content.ReadAsStringAsync();
            string? reason = null;
            try
            {
                using var errorDoc = JsonDocument.Parse(body);
                if (errorDoc.RootElement.ValueKind == JsonValueKind.Object &&
                    errorDoc.RootElement.TryGetProperty("reason", out var reasonEl))
                {
                    reason = reasonEl.GetString();
                }
            }
            catch (JsonException)
            {
                // Body was not JSON; fall back to the status code below.
            }

            throw new McpException(
                $"Request to '{requestUri}' failed: {(int)response.StatusCode} {response.ReasonPhrase}"
                + (string.IsNullOrEmpty(reason) ? "" : $" - {reason}"));
        }

        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
