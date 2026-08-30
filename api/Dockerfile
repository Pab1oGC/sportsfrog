# =============================================================================
# API image, in three stages:
#
#   build       compiles and publishes. Base for the other two.
#   migrations  applies migrations and exits. Used from docker-compose so
#               whoever doesn't have the SDK can prepare the database.
#   runtime     runs the API. The default stage.
#
# The build context is this folder (api/):
#   docker build -t sportfrog-api .
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The same SDK version the team uses. If the image ships one that doesn't
# match, the build fails right here with an explicit message instead of
# compiling with a different one.
COPY global.json ./

# Project files first, so the restore layer gets reused while dependencies
# don't change.
COPY Directory.Build.props ./
COPY src/SportFrog.Api/SportFrog.Api.csproj      src/SportFrog.Api/
COPY src/SportFrog.Domain/SportFrog.Domain.csproj src/SportFrog.Domain/
RUN dotnet restore src/SportFrog.Api/SportFrog.Api.csproj

COPY src/ src/
RUN dotnet publish src/SportFrog.Api/SportFrog.Api.csproj \
        --no-restore \
        --configuration Release \
        --output /app

# -----------------------------------------------------------------------------
# Applies migrations and exits. Runs once, before the API.
#
# Connects as the schema owner, not as the application: it's the only role
# that can modify objects. The connection string arrives via
# SPORTFROG_MIGRATIONS_CONNECTION, which is where the design-time factory
# reads it from.
# -----------------------------------------------------------------------------
FROM build AS migrations
WORKDIR /src

# Pinned to the same version as the project's Design package.
RUN dotnet tool install --global dotnet-ef --version 10.0.4
ENV PATH="${PATH}:/root/.dotnet/tools"

ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/SportFrog.Api"]

# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SkiaSharp's native library (ImageNormalizer: every photo, crest and piece
# of credential artwork goes through it) dynamically loads libfontconfig at
# runtime and isn't self-contained — the aspnet base image doesn't carry it,
# and without this the first upload of any kind fails with
# DllNotFoundException, not at build time. Installed as root, before
# dropping to the unprivileged user below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

# Unprivileged user, provided by the base image.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app ./
ENTRYPOINT ["dotnet", "SportFrog.Api.dll"]
