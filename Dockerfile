# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached independently of source changes.
COPY weather.csproj ./
RUN dotnet restore weather.csproj

# Copy the rest of the source and publish the server.
COPY . ./
RUN dotnet publish weather.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# The MCP server speaks JSON-RPC over stdio, so the container must be run with
# stdin attached, e.g. `docker run --rm -i weather-climate-mcp`.
ENTRYPOINT ["dotnet", "weather.dll"]
