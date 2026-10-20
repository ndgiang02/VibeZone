# syntax=docker/dockerfile:1
# Build context: repository root.
#   docker build -f src/VibeZone.Worker/Dockerfile -t vibezone-worker .

FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/VibeZone.SharedKernel/VibeZone.SharedKernel.csproj src/BuildingBlocks/VibeZone.SharedKernel/
COPY src/BuildingBlocks/VibeZone.Infrastructure/VibeZone.Infrastructure.csproj src/BuildingBlocks/VibeZone.Infrastructure/
COPY src/Modules/Catalog/VibeZone.Modules.Catalog/VibeZone.Modules.Catalog.csproj src/Modules/Catalog/VibeZone.Modules.Catalog/
COPY src/Modules/Catalog/VibeZone.Modules.Catalog.Contracts/VibeZone.Modules.Catalog.Contracts.csproj src/Modules/Catalog/VibeZone.Modules.Catalog.Contracts/
COPY src/Modules/Identity/VibeZone.Modules.Identity/VibeZone.Modules.Identity.csproj src/Modules/Identity/VibeZone.Modules.Identity/
COPY src/Modules/Identity/VibeZone.Modules.Identity.Contracts/VibeZone.Modules.Identity.Contracts.csproj src/Modules/Identity/VibeZone.Modules.Identity.Contracts/
COPY src/Modules/Media/VibeZone.Modules.Media/VibeZone.Modules.Media.csproj src/Modules/Media/VibeZone.Modules.Media/
COPY src/Modules/Playlists/VibeZone.Modules.Playlists/VibeZone.Modules.Playlists.csproj src/Modules/Playlists/VibeZone.Modules.Playlists/
COPY src/Modules/Playlists/VibeZone.Modules.Playlists.Contracts/VibeZone.Modules.Playlists.Contracts.csproj src/Modules/Playlists/VibeZone.Modules.Playlists.Contracts/
COPY src/VibeZone.Migrations/VibeZone.Migrations.csproj src/VibeZone.Migrations/
COPY src/VibeZone.Worker/VibeZone.Worker.csproj src/VibeZone.Worker/
RUN dotnet restore src/VibeZone.Worker/VibeZone.Worker.csproj

COPY src/ src/
RUN dotnet publish src/VibeZone.Worker/VibeZone.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS runtime
# FFmpeg/ffprobe from the distribution (Ubuntu 24.04). Untrusted media is parsed here, so this image runs
# as non-root, with no-new-privileges and resource limits set by the orchestrator (security.md).
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    Media__TempDirectory=/tmp/vibezone-media \
    Media__HeartbeatFile=/tmp/worker-heartbeat
COPY --from=build /app .
USER $APP_UID
# Healthy while the claim loop keeps touching the heartbeat file.
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
  CMD test -n "$(find /tmp/worker-heartbeat -mmin -1 2>/dev/null)" || exit 1
ENTRYPOINT ["dotnet", "VibeZone.Worker.dll"]
