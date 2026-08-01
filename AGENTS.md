# Project Quick Start

## Architecture
.NET 10 Web API (Clean Architecture) + Angular 22 SPA.
5 projects: **Domain**, **Application**, **Infrastructure**, **Shared**, **Web**.

## Key Conventions

### Endpoints (Minimal API)
- Static class implementing `IEndpointGroup` interface with `Map(RouteGroupBuilder)`.
- Each file in `src/Web/Endpoints/` handles one route group.
- Handlers are static methods returning `TypedResults.Ok/Created/NoContent/...`.
- Request DTOs are `record` types at the bottom of the file.
- Register in `src/Web/Infrastructure/WebApplicationExtensions.cs`.
- Auth at group level: `group.RequireAuthorization()`.

### ORM / DbContext
- EF Core 10 + Npgsql (PostgreSQL), Identity tables in same DB.
- `ApplicationDbContext` in `src/Infrastructure/Data/`.
- Interface `IApplicationDbContext` in `src/Application/Common/Interfaces/`.
- **Add a new DbSet**: entity → interface `IQueryable<T>` → `ApplicationDbContext` `DbSet<T>` + explicit interface impl.
- Fluent configs in `src/Infrastructure/Data/Configurations/` via `ApplyConfigurationsFromAssembly`.
- Interceptors: `AuditableEntityInterceptor` (auto-sets Created/CreatedBy/LastModified/LastModifiedBy on `IAuditableEntity`), `DispatchDomainEventsInterceptor`.
- Dev DB init uses `EnsureDeletedAsync` + `EnsureCreatedAsync` (no migrations applied).
- To add migration: `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Web --output-dir Data/Migrations`

### Domain
- Entities in `src/Domain/Entities/`. Value objects in `src/Domain/ValueObjects/` (sealed records). Enums in `src/Domain/Enums/`.
- `IAuditableEntity` (`Created`, `CreatedBy`, `LastModified`, `LastModifiedBy`).

### Application
- Service interfaces in `src/Application/<Feature>/` with implementations in `src/Infrastructure/Services/`.
- DTOs with `{ get; init; }` properties.
- DI registration happens in each project's `DependencyInjection.cs` as extension methods on `IHostApplicationBuilder`.

### Identity / Auth
- Cookie-based Identity (IdentityConstants.ApplicationScheme).
- `ApplicationUser` extends `IdentityUser` (empty, in `src/Infrastructure/Identity/`).
- Login/register via `MapIdentityApi<ApplicationUser>()` at `/api/Users`.
- Default admin: `administrator@localhost` / `Administrator1!`.

### Testing
- NUnit + Shouldly + Moq.
- Functional tests use `WebApplicationFactory<Program>`, Respawn for DB reset.
- `TestBase` calls `TestApp.ResetState()` before each test.
- `TestApp` provides `RunAsDefaultUserAsync()`, `RunAsAdministratorAsync()`.

### Angular
- `src/Web/ClientApp/`, Angular 22, standalone components, SSR, Tailwind 4, pnpm.
- No feature modules yet — scaffold only.

## Common Tasks

```
# Build
dotnet build src/Web/Web.csproj

# Add migration
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Web --output-dir Data/Migrations

# Run
dotnet run --project src/Web
```

## Url Shortener Feature (showcase)
- `POST /api/ShortenedUrls` (auth) — `{ "url": "..." }` → returns short code (201)
- `GET /{code}` (public) — redirects to original URL (301) or 404
- Entity: `ShortenedUrl` (`Guid Id`, `string OriginalUrl`, `string ShortCode`, `DateTimeOffset Created`, `string? CreatedBy`)
- Unique index on `ShortCode` (max 8 chars, alphanumeric)
