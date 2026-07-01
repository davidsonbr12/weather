# Weather + Climate MCP Server (C#)

A proof-of-concept [Model Context Protocol](https://modelcontextprotocol.io/) server, written in C# on .NET 10, that answers basic questions about **weather**, **weather patterns** (historical), and **climate change data**.

It runs over **stdio** and exposes its capabilities as MCP tools that any MCP client (Claude Desktop, Claude Code, etc.) can call.

> **Status: proof of concept.** This project exists **strictly as a proof of concept for working with Warp / Oz** and is not intended for production use.

## Data sources

All data sources are free and require **no API key**:

- **US National Weather Service** (`api.weather.gov`) — US forecasts and active alerts.
- **Open-Meteo** (`open-meteo.com`) — global geocoding, current/forecast weather, historical archive (from 1940), and downscaled CMIP6 climate projections.
- **global-warming.org** — global temperature anomaly (NASA GISTEMP) and atmospheric CO₂ (NOAA).

## Tools

The C# method names (PascalCase) are exposed as snake_case MCP tool names, shown below.

### Location helper
- `geocode_location(name, count=5)` — resolve a place name to latitude/longitude, country, timezone, and population. Feed the coordinates into the tools below.

### Weather (current / forecast)
- `get_forecast(latitude, longitude)` — detailed multi-day forecast (US only, National Weather Service).
- `get_alerts(state)` — active weather alerts for a US state code (e.g. `WA`).
- `get_global_forecast(latitude, longitude, days=7)` — current conditions + daily forecast, worldwide (Open-Meteo).

### Weather patterns (historical)
- `get_historical_weather(latitude, longitude, startDate, endDate)` — daily high/low temperature and precipitation for a date range, with averages and extremes.

### Climate change data
- `get_global_temperature_anomaly(sinceYear?)` — global temperature anomaly trend (°C vs. 1951–1980), summarized by decade.
- `get_atmospheric_co2()` — latest atmospheric CO₂ concentration and recent trend.
- `get_climate_projection(latitude, longitude, startYear=1990, endYear=2050)` — projected local temperature trend by decade (CMIP6).

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later.

## Run

```bash
dotnet run
```

The server communicates over stdio, so it is normally launched by an MCP client rather than run interactively.

## Run with Docker

For portability the server can also run in a container. Build the image:

```bash
docker build -t weather-climate-mcp .
```

MCP communicates over stdio, so run the container with `-i` to keep stdin attached:

```bash
docker run --rm -i weather-climate-mcp
```

## Use with an MCP client

`.mcp.json` registers the server:

```json
{
  "mcpServers": {
    "weather": {
      "command": "dotnet",
      "args": ["run", "--project", "/Users/briandavidson/workspaces/weather"]
    }
  }
}
```

Or, using the Docker image:

```json
{
  "mcpServers": {
    "weather": {
      "command": "docker",
      "args": ["run", "--rm", "-i", "weather-climate-mcp"]
    }
  }
}
```

## Example questions

- "What's the forecast for Seattle this week?"
- "Are there any weather alerts in California right now?"
- "How hot did it get in Phoenix in July 2020?"
- "How has average rainfall in Seattle changed since 1950?"
- "What is the current atmospheric CO₂ level, and how much has it risen?"
- "How much has global temperature increased since 1900?"
- "What is the projected temperature trend for Paris through 2050?"

## Tests

Integration smoke tests exercise each tool against the live (free) APIs:

```bash
dotnet test tests/weather.Tests.csproj
```

> Note: the tests make real network requests, so they require internet access.

## Project layout

- `Program.cs` — MCP server host (stdio) and tool discovery.
- `WeatherClients.cs` — named `HttpClient` registrations (shared with tests).
- `Tools/` — tool implementations grouped by capability, plus JSON/formatting helpers.
- `tests/` — xUnit integration tests.
- `Dockerfile` / `.dockerignore` — containerized build and run.
