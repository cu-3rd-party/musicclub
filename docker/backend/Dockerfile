# syntax=docker/dockerfile:1.7

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for dependency restoration
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/Application/Application.csproj src/Application/
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Web/Web.csproj src/Web/
COPY *.slnx ./

# Restore dependencies (cached unless project files change)
RUN dotnet restore src/Web/Web.csproj

# Copy only backend source (exclude frontend/node_modules)
COPY src/Application/ src/Application/
COPY src/Domain/ src/Domain/
COPY src/Infrastructure/ src/Infrastructure/
COPY src/Shared/ src/Shared/
COPY src/Web/Backfill/ src/Web/Backfill/
COPY src/Web/Bot/ src/Web/Bot/
COPY src/Web/Endpoints/ src/Web/Endpoints/
COPY src/Web/Infrastructure/ src/Web/Infrastructure/
COPY src/Web/Properties/ src/Web/Properties/
COPY src/Web/wwwroot/ src/Web/wwwroot/
COPY src/Web/appsettings*.json src/Web/
COPY src/Web/DependencyInjection.cs src/Web/
COPY src/Web/GlobalUsings.cs src/Web/
COPY src/Web/Program.cs src/Web/

# Build and publish
# OpenApiGenerateDocuments=false: document generation runs the whole app (including DB init),
# which must not happen during an image build; docs are served live via MapOpenApi at runtime
WORKDIR /src/src/Web
RUN dotnet publish Web.csproj -c Release -o /app/publish /p:UseAppHost=false /p:PublishRunWebpack=false /p:OpenApiGenerateDocuments=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Install wget for health checks (not included in slim runtime images)
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/*

# Expose the port the app runs on
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "CuMusicClub.Web.dll"]
