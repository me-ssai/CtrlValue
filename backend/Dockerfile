# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies (layer-cached until .csproj files change)
COPY ["src/ProjectZ.Api/ProjectZ.Api.csproj",             "src/ProjectZ.Api/"]
COPY ["src/ProjectZ.Application/ProjectZ.Application.csproj", "src/ProjectZ.Application/"]
COPY ["src/ProjectZ.Infrastructure/ProjectZ.Infrastructure.csproj", "src/ProjectZ.Infrastructure/"]
COPY ["src/ProjectZ.Domain/ProjectZ.Domain.csproj",       "src/ProjectZ.Domain/"]
RUN dotnet restore "src/ProjectZ.Api/ProjectZ.Api.csproj"

# Copy source and publish
COPY . .
RUN dotnet publish "src/ProjectZ.Api/ProjectZ.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Default to port 5000 (override via ASPNETCORE_URLS env var)
ENV ASPNETCORE_URLS=http://+:5000

EXPOSE 5000
ENTRYPOINT ["dotnet", "ProjectZ.Api.dll"]
