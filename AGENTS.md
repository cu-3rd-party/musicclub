# Project Quick Start

## Architecture
.NET 10 Web API (Clean Architecture) + Angular 22 SPA.
5 projects: **Domain**, **Application**, **Infrastructure**, **Shared**, **Web** (plus a Telegram bot hosted inside `Web`).

- Routes are grouped under `/api/v1` (`src/Web/Infrastructure/WebApplicationExtensions.cs`).
- OpenAPI + Scalar UI at `/scalar` in dev.
- Packages are versioned centrally in `Directory.Packages.props`.

## Key Conventions

### Endpoints (Minimal API)
- Static class with `Map(RouteGroupBuilder)` extension point, registered in `WebApplicationExtensions`. No shared interface.
- Each file in `src/Web/Endpoints/` handles one route group (`Auth.cs`, `Songs.cs`).
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
- Interceptors: `AuditableEntityInterceptor` (auto-sets Created/CreatedBy/LastModified/LastModifiedBy on `IAuditableEntity`).
- Postgres enums mapped via `MapEnum<SongLinkType>()`.
- Dev DB init uses `EnsureDeletedAsync` + `EnsureCreatedAsync` (no migrations applied).
- To add migration: `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Web --output-dir Data/Migrations`

### Domain
- Entities in `src/Domain/Entities/`. Value objects in `src/Domain/ValueObjects/` (sealed records). Enums in `src/Domain/Enums/`.
- `IAuditableEntity` (`Created`, `CreatedBy`, `LastModified`, `LastModifiedBy`).

### Application
- Service interfaces in `src/Application/<Feature>/` with implementations in `src/Infrastructure/Services/`.
- DTOs are `record` types with positional params (see `src/Application/Songs/SongDtos.cs`, `src/Application/Auth/AuthDtos.cs`).
- DI registration happens in each project's `DependencyInjection.cs` as extension methods on `IHostApplicationBuilder`.

### Identity / Auth (main app)
- ASP.NET Identity is the single user model. `ApplicationUser` (in `src/Domain/Entities/`, extends `IdentityUser<Guid>` with `TgUserId`, `IsChatMember`, `DisplayName`, `AvatarUrl`, `CreatedAt`/`UpdatedAt`) maps to `AspNetUsers` with a `Guid` PK. No legacy `app_user`/`user_permissions` tables.
- Legacy cookie Identity (`MapIdentityApi<ApplicationUser>()` at `/api/Users`) remains, default admin `administrator@localhost` / `Administrator1!`. **Not used by the app UI.**
- Real auth = Telegram WebApp initData:
  - `POST /api/v1/auth/telegram` (public) — body `{ "initData": "..." }` → `AuthSessionDto { accessToken, refreshToken, expiresAt, accessTokenAcquiredAt, user }`.
  - `POST /api/v1/auth/refresh` (public) — body `{ "refreshToken": "..." }` → `TokenPairDto`.
  - `TelegramAuthService` (`src/Infrastructure/Services/`): HMAC-SHA256 initData validation (bot token = `TelegramOptions.BotToken`), optional chat-membership gate (`TelegramOptions.SkipChatMembershipCheck`), upserts `ApplicationUser` via `UserManager` (linked by `TgUserId`), issues DataProtection-protected bearer tokens, single-use rotating refresh tokens.
  - Other endpoints use `Authorization: Bearer` (IdentityConstants.BearerScheme). Get the app user id via `context.User.GetUserId()` (`src/Application/Common/Auth/ClaimsPrincipalExtensions.cs`, reads `ClaimTypes.NameIdentifier`, throws 401 if missing).

### Permissions (claims-based)
- **Granular permissions are `permission` claims** (`src/Application/Common/Auth/Permissions.cs`): claim type `PermissionClaimTypes.Permission = "permission"`, values `participation.edit_own`, `participation.edit_any`, `songs.edit_own`, `songs.edit_any`, `songs.edit_featured`, `events.edit`, `tracklists.edit`; `Permissions.All` lists them all.
- Permissions can live on a user (`AspNetUserClaims`) or on a role (`AspNetRoleClaims`); the Identity claims factory merges both. Check via `principal.HasPermission(...)`.
- New users get default claims `songs.edit_own` + `participation.edit_own`. The seeder's `Administrator` role is granted all `Permissions.All` claims, and admin membership is the role.
- Service methods take the `ClaimsPrincipal` (not a `Guid`); `SongService.PermissionsFrom` maps permission claims to the existing `PermissionsDto` shape. `ClaimsPrincipalExtensions.GetUserId()` parses `ClaimTypes.NameIdentifier` into a `Guid`.

### Songs feature
- Entities: `Song`, `SongRole` (PK `SongId`+`Role`), `SongRoleAssignment` (FK `song_role_exists` requires the role to exist on the song).
- `ISongService` (`src/Application/Songs/`) → `SongService` (`src/Infrastructure/Services/SongService.cs`).
- Endpoints under `/api/v1/songs` (all require auth):
  - `GET /` `?query&pageSize&pageToken` — ILIKE search on title/artist, featured-first then newest, offset pagination (default 20, max 100, token = offset int).
  - `GET /{songId:guid}` — song + roles + assignments + permissions.
  - `POST /` — create; needs `songs.edit_own`/`songs.edit_any`; featured needs `songs.edit_featured`.
  - `PUT /{songId:guid}` — update; owner or `songs.edit_any`; replaces roles.
  - `DELETE /{songId:guid}` — owner or `songs.edit_any`; cascades roles/assignments.
  - `POST /{songId:guid}/join` / `POST /{songId:guid}/leave` — body `{ "role": "..." }`; needs `participation.edit_own`/`participation.edit_any`.
- **Roles are normalized (trim → drop empty → dedupe → sort) on both create and update.**
- YouTube links auto-extract a thumbnail (`SongThumbnail.Normalize`), custom `thumbnailUrl` wins.
- `Link.Kind` values: `youtube`, `yandex_music`, `soundcloud`; anything else → `ValidationException`.
- HTTP errors are mapped to Problem Details in `src/Web/Infrastructure/ProblemDetailsExceptionHandler.cs` (`NotFoundException`→404, `ForbiddenAccessException`→403, `ValidationException`→400).

### Telegram Bot (in-process, polling)
- Lives in `src/Web/Bot/` and runs inside `Web` (not a separate process):
  - `TelegramBotHostedService` — `BackgroundService`; **skips startup entirely if `BotToken` is empty or `"0000"`**; deletes webhook (polling mode), subscribes `TelegramBotClient` `OnUpdate`/`OnError` events, resolves WebApp URL via `GetChatMenuButton`.
  - `BotUpdateHandler` — scoped per update; port of the Python bot handlers.
  - `BotTexts` — gettext-style i18n; ru if locale starts with `ru`, else en.
  - `BotOptions` (`"Bot"` section): `BotToken` (default `"0000"` = disabled), `DefaultWebAppUrl` (default `http://localhost:5173`), `EmailDomain`.
- Flow: `/start` → WebApp button; `/start auth_<uuid>` → confirms `tg_auth_user` linking + grants the user's `permission` claims; `/help`; `calendar_attach`/`email_confirm_*` callbacks + email/ICS URL state machine.
- Uses `Telegram.Bot 22.10.2` (added to `Directory.Packages.props`).
- `appsettings.json` + `appsettings.Development.json` both set `Bot:BotToken` to `"0000"`.

### Testing
- NUnit + Shouldly + Moq. Functional tests in `tests/Application.FunctionalTests`.
- `FunctionalTestSetup` (`[SetUpFixture]`) builds a `WebApiFactory` (`WebApplicationFactory<Program>`), exposes `ScopeFactory`, auto-creates the test DB (`EnsureCreatedAsync`), and sets up `DatabaseResetter` (Respawn).
- `TestBase.SetUp` → `TestApp.ResetState()` (truncates all tables) before every test.
- Test DB connection from `TEST_CONNECTION_STRING` env var (default: appsettings `ConnectionStrings:CuMusicClubDb` with the database swapped to `CuMusicClubTest`; fallback `Host=localhost;Database=CuMusicClubTest;...`).
- **Pattern for service tests** (`tests/Application.FunctionalTests/Songs/SongServiceTests.cs`): resolve `ISongService` from `FunctionalTestSetup.ScopeFactory`, seed `ApplicationUser` + a `ClaimsPrincipal` carrying `permission` claims (via `CreateUserAsync`) and `Song` via `TestApp.AddAsync`, assert DB state through a scope-resolved `ApplicationDbContext` helper.
- **Bot tests** (`tests/Application.FunctionalTests/Bot/`): use `FakeTelegramBotClient` (records `SendRequest` payloads, exposes `SentMessages`/`AnsweredCallbacks`); seed users via `UserManager<ApplicationUser>`.
- Unit tests in `tests/Application.UnitTests`.

### Angular
- `src/Web/ClientApp/`, Angular 22, standalone components, SSR, Tailwind 4, pnpm.
- No feature modules yet — scaffold only.

## AppSettings / Config
- `ConnectionStrings:CuMusicClubDb` — Postgres connection string.
- `Telegram` — `BotToken` (used by auth HMAC), `ChatId`, `SkipChatMembershipCheck`.
- `Bot` — `BotToken` (`"0000"` disables the bot), `DefaultWebAppUrl`, `EmailDomain`.

## Common Tasks

```
# Build
dotnet build src/Web/Web.csproj

# Add migration
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Web --output-dir Data/Migrations

# Run (backend; app serves the Angular build + Scalar UI)
dotnet run --project src/Web

# Run (frontend dev server)
cd src/Web/ClientApp && pnpm dev

# Functional tests (need Postgres; DB is auto-created by FunctionalTestSetup)
TEST_CONNECTION_STRING="Server=localhost;Port=5432;Database=musicclub_test;Username=admin;Password=password" dotnet test tests/Application.FunctionalTests

# Functional tests filtered to one feature
TEST_CONNECTION_STRING="..." dotnet test tests/Application.FunctionalTests --filter "FullyQualifiedName~Songs"

# Unit tests
dotnet test tests/Application.UnitTests
```

## Url Shortener Feature (showcase)
- `POST /api/ShortenedUrls` (auth) — `{ "url": "..." }` → returns short code (201)
- `GET /{code}` (public) — redirects to original URL (301) or 404
- Entity: `ShortenedUrl` (`Guid Id`, `string OriginalUrl`, `string ShortCode`, `DateTimeOffset Created`, `string? CreatedBy`)
- Unique index on `ShortCode` (max 8 chars, alphanumeric)
