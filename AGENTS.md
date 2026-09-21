# Music Club ERP — NESSY.md

## Обзор проекта

**Music Club** — ERP-система для музыкального клуба ЦУ, построенная на .NET 10.0. Система управляет песнями, событиями,
участниками и интеграцией с Telegram (аутентификация через бота, уведомления о заполненных песнях, топики в форуме).

### Архитектура (Clean Architecture / DDD)

```
src/
├── Domain/          — Сущности предметной области, абстракции, константы, value objects
├── Application/     — Бизнес-логика (сервисы), DTO, валидация, опции, Common
├── Infrastructure/  — Данные (EF Core, репозитории, миграции) + инфраструктура (JWT, Telegram-клиент)
├── Shared/          — Общие константы и утилиты
└── Web/             — ASP.NET Core Web API + ClientApp (SPA, SvelteKit)
    └── ClientApp/   — Frontend (Node.js, pnpm)
```

Ключевой принцип: **интерфейсы в `Domain/Abstractions/`, реализации (EF Core) в `Infrastructure/Data/Repositories/`;
бизнес-логика в `Application/Services/`, данные — в `Infrastructure/`.** Зависимости направлены сверху вниз:
`Web → Infrastructure → Application → Domain`.

| Слой               | Содержимое                                                                                                                                                                                                                          |
|--------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Domain**         | Сущности (`Entities/`), интерфейсы репозиториев (`Abstractions/` — `IRepository<T>`, `ISongRepository`, `IUnitOfWork`, …), константы (`Constants/` — `Permission`, `Roles`), `Enums/`, `ValueObjects/` (`SongThumbnail`), `Common/` |
| **Application**    | Сервисы-реализации бизнес-логики (`Services/` — Song, Auth, DataEntry, Permission, Telegram), DTO, валидаторы FluentValidation, `Common/` (`Auth/`, `Exceptions/`, `Options/`), `GlobalUsings.cs`                                   |
| **Infrastructure** | `Data/` (DbContext, `Repositories/`, `Configurations/`, `Migrations/`, `Interceptors/`, `ApplicationDbContextInitialiser`), JWT, `ITelegramBotClient`, опции биндятся отсюда                                                        |
| **Web**            | Minimal API-эндпоинты (`Endpoints/v1/`), Telegram-бот (`Bot/`), backfill-сервисы (`Backfill/`), OpenAPI/Scalar, SPA                                                                                                                 |

### Слои репозиториев (паттерн)

- Общий контракт: `IRepository<TEntity>` в `Domain/Abstractions/` — `Query()`, `FindByIdAsync`, `AddAsync`, `Update`,
  `Remove`, `SaveChangesAsync`.
- Конкретные интерфейсы (`ISongRepository`, `IUserSessionRepository`, …) расширяют generic доменными запросами.
- Реализации: `Repository<TEntity>` + специфичные репозитории в `Infrastructure/Data/Repositories/`.
- Транзакции и сохранение: `IUnitOfWork`/`ITransaction` (в `Domain/Abstractions/`, реализация в
  `Infrastructure/Data/Repositories/UnitOfWork.cs`).
- В DI базовый `DbContext` маппится на конкретный `ApplicationDbContext`; репозитории регистрируются через
  `AddScoped(typeof(IRepository<>), typeof(Repository<>))` и по-отдельности.

## Технологический стек

- **Backend:** .NET 10.0, ASP.NET Core, Entity Framework Core 10, Npgsql
- **Frontend:** Node.js, pnpm, SvelteKit SPA (встроен в Web-проект)
- **База данных:** PostgreSQL 18 (в Docker), enum `song_link_type`
- **Аутентификация:** самописная модель пользователя (`app_user` + `user_permissions`) + JWT + Telegram Bot
- **Контейнеризация:** Docker, Docker Compose, Traefik (reverse-proxy)
- **CI/CD:** GitHub Actions (деплой на dev-VDS по SSH)
- **Тесты:** NUnit, Shouldly, Moq, Testcontainers (PostgreSQL), Respawn

### Ключевые пакеты (централизованное управление в `Directory.Packages.props`)

| Пакет                                          | Назначение                                |
|------------------------------------------------|-------------------------------------------|
| Npgsql.EntityFrameworkCore.PostgreSQL          | PostgreSQL-провайдер                      |
| Telegram.Bot                                   | Telegram API                              |
| Scalar.AspNetCore                              | OpenAPI-документация (UI на `/scalar/v1`) |
| FluentValidation.DependencyInjectionExtensions | Валидация                                 |
| Ardalis.GuardClauses                           | Guard-клаузы (`NotFoundException` и др.)  |
| Testcontainers.PostgreSql / Respawn            | Интеграционные тесты                      |

## Быстрый старт

### Предварительные требования

- .NET 10 SDK
- Node.js (для фронта), pnpm
- Docker & Docker Compose

### Запуск через Docker Compose (рекомендуется)

```bash
cp .env.example .env          # отредактировать при необходимости
docker compose up -d --build
docker compose ps             # статус сервисов
docker compose logs -f backend
```

**Сервисы:** `traefik` (порт `$NGINX_PORT`, по умолч. 80), `db` (PostgreSQL 18, `$DB_PORT`), `backend` (`:8080`, health
`/health`), `frontend` (`:5173`).

> Примечание: в `docker-compose.yml` для `backend` задан оверрайд `ConnectionStrings__CuMusicClubDb`, собираемый из
> `POSTGRES_*` переменных — отдельно задавать строку подключения не нужно. Для `db` используется `env_file: .env`.
> Остальной конфиг приходит из `.env` + дефолтов в приложении.

### Локальная разработка

```bash
# Backend
cd src/Web && dotnet run

# Frontend (отдельный терминал)
cd src/Web/ClientApp && pnpm install && pnpm run dev

# База данных (Docker)
docker compose up -d db
```

Есть готовые профили запуска в `.run/` (Web http/https, dev, db).

### Миграции БД

```bash
dotnet ef migrations add MigrationName --project src/Infrastructure --startup-project src/Web
dotnet ef database update --project src/Infrastructure --startup-project src/Web
```

> **Примечание:** в dev-режиме используется `EnsureCreatedAsync` (через `ApplicationDbContextInitialiser`) — миграции не
> применяются автоматически.

## Тестирование

```bash
dotnet test                      # все тесты
dotnet test tests/Application.UnitTests
dotnet test tests/Domain.UnitTests
dotnet test tests/Infrastructure.IntegrationTests   # требует Docker (Testcontainers)
```

**Структура и фреймворки:**

- **`Domain.UnitTests`** — сущности, константы, value objects (NUnit + Shouldly).
- **`Application.UnitTests`** — бизнес-логика сервисов с моками (NUnit + Shouldly + Moq).
- **`Infrastructure.IntegrationTests`** — e2e через `WebApplicationFactory<Program>` + Testcontainers PostgreSQL +
  Respawn для сброса БД (NUnit + Shouldly + Moq).

> **Известное ограничение:** интеграционные тесты, резолвящие `TelegramChatService`, падают в тестовом окружении без
> заданного `Telegram__BotToken` (создание `TelegramBotClient`). Чтобы они проходили, нужно передать тестовый
> Telegram-токен или замокать `ITelegramBotClient`.

## Структура базы данных

Подробная ER-схема и mermaid-диаграмма — в [`ER_SCHEME.md`](ER_SCHEME.md) (генерируется из флюент-конфигураций в
`Infrastructure/Data/Configurations/`).

### Основные таблицы

| Таблица                                            | Описание                                                                                           |
|----------------------------------------------------|----------------------------------------------------------------------------------------------------|
| `app_user`                                         | Пользователи (`ApplicationUser` + `TgUserId`, `DisplayName`, `AvatarUrl`, …; без ASP.NET Identity) |
| `user_permissions`                                 | Гранулярные права (строка на право: `songs.edit_own`, `events.edit`, …)                            |
| `song`                                             | Песни (title, artist, link_kind, link_url, is_featured, thumbnail)                                 |
| `song_role` / `song_role_assignment`               | Роли песни и назначения пользователей                                                              |
| `song_topic`                                       | Связь песни с Telegram-топиком                                                                     |
| `event` / `event_track_item` / `event_participant` | События и треклисты (Work In Progress)                                                             |
| `refresh_tokens` / `user_session`                  | JWT refresh-токены и сессии                                                                        |
| `tg_auth_link`                                     | Telegram-сессии авторизации                                                                        |
| `data_entry`                                       | Загруженные файлы (в т.ч. превью)                                                                  |
| `calendar` / `calendar_attach_state`               | ICS-календари (Work In Progress)                                                                   |
| `role_title` / `user_preferences`                  | Предустановленные роли и предпочтения                                                              |

### Система прав (Permissions)

Права — строковые значения, хранящиеся по одной строке на право в таблице `user_permissions(user_id, permission)` (см.
`Domain/Constants/Permission.cs`):

| Permission                                          | Описание                             |
|-----------------------------------------------------|--------------------------------------|
| `participation.edit_own` / `participation.edit_any` | Редактирование своих / любых участий |
| `songs.edit_own` / `songs.edit_any`                 | Редактирование своих / любых песен   |
| `songs.edit_featured`                               | Редактирование избранных песен       |
| `events.edit` / `tracklists.edit`                   | Редактирование событий / треклистов  |

- Новым пользователям выдаются: `songs.edit_own` + `participation.edit_own`.
- Роль `Administrator` получает все permissions; роли — лишь «сахар», материализующий набор прав в `user_permissions`
  (см. `Permission.ByRole`).

## CI/CD

- [`.github/workflows/deploy-dev.yml`](.github/workflows/deploy-dev.yml) — деплой на dev-VDS по SSH (rsync `src`,
  `docker`, `traefik`, `docker-compose.yml`, slnx, props; запись `.env` из секрета; `docker compose up -d --build`).
  Триггер: push в `master` или вручную. Секреты: `SSH_KEY`, `HOST`, `USERNAME`, `DEPLOY_PATH`, `ENV`.

## Переменные окружения

Источник истины — `.env` (см. [`.env.example`](.env.example)):

```ini
# Application
Logging__LogLevel__Default=Information
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080

# Backend / Telegram (пустой BotToken отключает Telegram-аутентификацию)
Telegram__BotToken=
Telegram__ChatId=
Telegram__BotUsername=
Telegram__SkipChatMembershipCheck=true
Telegram__WebAppUrl=http://localhost:80

# Security (мин. длина, иначе ошибка в рантайме)
Security__Secret=change-me-in-production

# Frontend
API_URL=http://localhost:80
API_SSR_URL=http://backend:8080

# PostgreSQL
POSTGRES_DB=db
POSTGRES_USER=admin
POSTGRES_PASSWORD=password
DB_PORT=5432

# Nginx / Traefik
SERVER_HOST=localhost
NGINX_PORT=80
```

> Опции приложения: `Application/Common/Options/` (`SecurityOptions`, `TelegramOptions`); в Web —
> `Web/Bot/BotOptions.cs`.

## Конвенции разработки

### C# стиль (`.editorconfig`)

- Отступы: 4 пробела; `csharp_indent_case_contents = true`.
- Фигурные скобки: `csharp_prefer_braces = false:warning` (разрешены однострочные).
- `var` предпочтителен (`csharp_style_var_for_built_in_types = true:suggestion`).
- Expression-bodied методы/свойства выключены (`= false:warning`).
- Null-коалесценция и null-пропагация: `= true:warning`.
- Сортировка using: `dotnet_sort_system_directives_first = true`.
- Разделители строк: CRLF, `insert_final_newline = true`.

### Архитектурные принципы

1. **DDD / Clean Architecture:** сущности и абстракции в `Domain/`, бизнес-логика в `Application/`, данные и
   инфраструктура в `Infrastructure/`.
2. **Репозитории:** интерфейсы в `Domain/Abstractions/`, реализации в `Infrastructure/Data/Repositories/`; общий
   `IRepository<T>` + специфичные; транзакции через `IUnitOfWork`.
3. **Разделение интерфейсов/реализаций:** сервисы зависят от абстракций репозиториев, а не от `DbContext` напрямую.
4. **Dependency Injection:** все зависимости через конструктор; регистрация — в `AddApplicationServices`/
   `AddInfrastructureServices`/`AddWebServices`.
5. **Guard Clauses:** `Ardalis.GuardClauses` для валидации аргументов.
6. **FluentValidation:** валидация моделей через отдельные валидаторы (сканируются из Application-сборки).
7. **EF Core:** Fluent API в `Infrastructure/Data/Configurations/`.

### Именование

- **Пространства имён:** `CuMusicClub.{Layer}` (например, `CuMusicClub.Domain.Abstractions`,
  `CuMusicClub.Application.Services.Song`).
- **Сущности:** единственное число (`Song`, `Event`, `ApplicationUser`).
- **Таблицы БД:** snake_case, единственное число (`song`, `event`, `song_role`).
- **DTO:** суффикс `Dto` или `Request`/`Response`.
- **Репозитории:** интерфейс `I{Entity}Repository`, реализация `{Entity}Repository`.

## Полезные команды

```bash
# Проверка здоровья
docker compose ps
docker compose logs -f backend / frontend

# Пересборка backend
docker compose up -d --build backend

# Очистка (данные в volume сохранятся)
docker compose down
# Полная очистка (включая БД)
docker compose down -v

# OpenAPI/Scalar UI
# /scalar/v1
```

## Документация и ресурсы

- **Схема БД (текст+mermaid):** [`ER_SCHEME.md`](ER_SCHEME.md)
- **Схема БД (визуальная):** [`docs/musicclub_v2.png`](docs/musicclub_v2.png)
- **Миграционные скрипты:** [`docs/migration.sql`](docs/migration.sql)
- **Ориентир архитектуры:** репозиторий `~/dev/Namekni` (эталон схемы generic repository / разделения слоёв)
- **Правки AGENTS.md:** файл защищён от автоматической модификации — изменения требуют явного запроса пользователя.
