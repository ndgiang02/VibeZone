# syntax=docker/dockerfile:1
# Build context: repository root.
#   docker build -f src/VibeZone.Api/Dockerfile -t vibezone-api .                         # API (default)
#   docker build -f src/VibeZone.Api/Dockerfile --target migrator -t vibezone-migrator .   # migrations

FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src

# Restore in its own layer: only re-runs when project files or package versions change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/VibeZone.SharedKernel/VibeZone.SharedKernel.csproj src/BuildingBlocks/VibeZone.SharedKernel/
COPY src/BuildingBlocks/VibeZone.Infrastructure/VibeZone.Infrastructure.csproj src/BuildingBlocks/VibeZone.Infrastructure/
COPY src/Modules/Catalog/VibeZone.Modules.Catalog/VibeZone.Modules.Catalog.csproj src/Modules/Catalog/VibeZone.Modules.Catalog/
COPY src/Modules/Catalog/VibeZone.Modules.Catalog.Contracts/VibeZone.Modules.Catalog.Contracts.csproj src/Modules/Catalog/VibeZone.Modules.Catalog.Contracts/
COPY src/Modules/Identity/VibeZone.Modules.Identity/VibeZone.Modules.Identity.csproj src/Modules/Identity/VibeZone.Modules.Identity/
COPY src/Modules/Identity/VibeZone.Modules.Identity.Contracts/VibeZone.Modules.Identity.Contracts.csproj src/Modules/Identity/VibeZone.Modules.Identity.Contracts/
COPY src/Modules/Media/VibeZone.Modules.Media/VibeZone.Modules.Media.csproj src/Modules/Media/VibeZone.Modules.Media/
COPY src/VibeZone.Migrations/VibeZone.Migrations.csproj src/VibeZone.Migrations/
COPY src/VibeZone.Api/VibeZone.Api.csproj src/VibeZone.Api/
RUN dotnet restore src/VibeZone.Api/VibeZone.Api.csproj

COPY src/ src/
# The OpenAPI document is generated and committed by regular builds; skip it here.
RUN dotnet publish src/VibeZone.Api/VibeZone.Api.csproj -c Release -o /app --no-restore \
    -p:OpenApiGenerateDocuments=false

# EF Core migration bundle: a single executable that applies pending migrations.
# Reads ConnectionStrings__Database (DesignTimeDbContextFactory); idempotent, takes a migration lock.
FROM build AS bundle
COPY dotnet-tools.json ./
RUN dotnet tool restore \
 && dotnet ef migrations bundle --project src/VibeZone.Migrations --startup-project src/VibeZone.Migrations \
    --configuration Release --output /efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS migrator
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0
COPY --from=bundle /efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# Default (last) stage: the API.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
COPY --from=build /app .
# Non-root user shipped with the official image (UID 1654).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "VibeZone.Api.dll"]
