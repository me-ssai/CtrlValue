# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies (layer-cached until .csproj files change)
COPY NuGet.Config .
COPY ["src/CtrlValue.Api/CtrlValue.Api.csproj",             "src/CtrlValue.Api/"]
COPY ["src/CtrlValue.Application/CtrlValue.Application.csproj", "src/CtrlValue.Application/"]
COPY ["src/CtrlValue.Infrastructure/CtrlValue.Infrastructure.csproj", "src/CtrlValue.Infrastructure/"]
COPY ["src/CtrlValue.Domain/CtrlValue.Domain.csproj",       "src/CtrlValue.Domain/"]
RUN dotnet restore "src/CtrlValue.Api/CtrlValue.Api.csproj"

# Copy source and publish
COPY . .
RUN dotnet publish "src/CtrlValue.Api/CtrlValue.Api.csproj" \
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
ENTRYPOINT ["dotnet", "CtrlValue.Api.dll"]
