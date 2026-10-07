# syntax=docker/dockerfile:1.7

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install system dependencies and tools early (cached until apt-cache updates)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        wget \
        libkrb5-3 \
        libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Layer 1: Copy only props and project files (minimal cache invalidation)
COPY Directory.Build.props Directory.Packages.props *.slnx ./
COPY src/Application/Application.csproj src/Application/
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Web/Web.csproj src/Web/

# Layer 2: Restore dependencies (cached unless .csproj or .props files change)
# Use cache mount for NuGet packages to speed up subsequent builds
RUN --mount=type=cache,id=nuget-build,target=/root/.nuget/packages \
    dotnet restore src/Web/Web.csproj

# Layer 3: Copy source code (invalidates cache only when source changes)
COPY src/ src/

# Layer 4: Build and publish
# OpenApiGenerateDocuments=false: document generation runs the whole app (including DB init),
# which must not happen during an image build; docs are served live via MapOpenApi at runtime
WORKDIR /src/src/Web
RUN --mount=type=cache,id=nuget-build,target=/root/.nuget/packages \
    dotnet publish Web.csproj -c Release -o /app/publish /p:UseAppHost=false /p:PublishRunWebpack=false /p:OpenApiGenerateDocuments=false

# Layer 5: Download Chromium for Playwright (in build stage)
# Only the browser binaries are copied to runtime; its system libraries are installed there
WORKDIR /app/publish
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN ./.playwright/node/linux-x64/node ./.playwright/package/cli.js install chromium

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install system dependencies for Chromium to run and health check
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        wget \
        libkrb5-3 \
        libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Set environment variables before copying (so they're cached)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

# Install Chromium's shared libraries (libglib, libnss, ...) via the bundled Playwright CLI;
# copied separately so this layer stays cached across app code changes
COPY --from=build /app/publish/.playwright ./.playwright
RUN ./.playwright/node/linux-x64/node ./.playwright/package/cli.js install-deps chromium \
    && rm -rf /var/lib/apt/lists/*

# Copy published application and Chromium (both from build stage cache)
COPY --from=build /app/publish .
COPY --from=build /ms-playwright /ms-playwright

# Prepare directories for Yandex browser data (mounted as volume)
RUN mkdir -p /app/data/yandex

# Expose the port the app runs on
EXPOSE 8080

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "CuMusicClub.Web.dll"]
