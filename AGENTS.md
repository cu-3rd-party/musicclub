# Music Club ERP — NESSY.md

## Обзор проекта

**Music Club** — ERP-система для музыкального клуба ЦУ, построенная на .NET 10.0. Система управляет песнями, событиями, участниками и интеграцией с Telegram.

### Архитектура

```
src/
├── Domain/          — Сущности предметной области (ApplicationUser, Song, Event, etc.)
├── Application/     — Бизнес-логика, валидация, JWT, Telegram-бот
├── Infrastructure/  — EF Core, Identity, репозитории, миграции
├── Shared/          — Общие утилиты и константы
└── Web/             — ASP.NET Core Web API + ClientApp (SPA)
    └── ClientApp/   — Frontend (Node.js 22, pnpm)
```

### Технологический стек

- **Backend:** .NET 10.0, ASP.NET Core, Entity Framework Core 10, Npgsql
- **Frontend:** Node.js 22, pnpm, SPA (встроен в Web-проект)
- **База данных:** PostgreSQL 18
- **Аутентификация:** ASP.NET Identity + JWT + Telegram Bot
- **Контейнеризация:** Docker, Docker Compose
- **CI/CD:** GitHub Actions (Docker Hub, VDS-деплой)

### Ключевые зависимости

| Пакет | Версия | Назначение |
|-------|--------|------------|
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.1 | PostgreSQL-провайдер |
| Telegram.Bot | 22.10.2 | Telegram API |
| Scalar.AspNetCore | 2.13.13 | OpenAPI-документация |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | Валидация |
| Testcontainers.PostgreSql | 4.13.0 | Интеграционные тесты |

## Быстрый старт

### Предварительные требования

- .NET 10 SDK
- Node.js 22+ с pnpm (`corepack enable`)
- Docker & Docker Compose
- PostgreSQL (локально или в Docker)

### Запуск через Docker Compose (рекомендуется)

```bash
# 1. Скопируйте .env.example в .env
cp .env.example .env

# 2. Отредактируйте .env при необходимости (порты, пароли)

# 3. Запустите все сервисы
docker compose up -d --build

# 4. Проверьте статус
docker compose ps

# 5. Логи
docker compose logs -f backend
docker compose logs -f frontend
```

**Сервисы:**
- `traefik` — порт 80 (прокси)
- `backend` — порт 8080 (внутри сети), health: `/health`
- `frontend` — порт 5173
- `db` — порт 5432

### Локальная разработка

```bash
# Backend
cd src/Web
dotnet restore
dotnet run

# Frontend (отдельный терминал)
cd src/Web/ClientApp
pnpm install
pnpm run dev

# База данных (Docker)
docker compose up -d db
```

### Миграции БД

```bash
# Добавить новую миграцию
dotnet ef migrations add MigrationName --project src/Infrastructure --startup-project src/Web

# Применить миграции
dotnet ef database update --project src/Infrastructure --startup-project src/Web
```

> **Примечание:** В dev-режиме используется `EnsureDeletedAsync` + `EnsureCreatedAsync` — миграции не применяются автоматически.

## Тестирование

```bash
# Все тесты
dotnet test

# Unit-тесты домена
dotnet test tests/Domain.UnitTests

# Unit-тесты приложения
dotnet test tests/Application.UnitTests

# Интеграционные тесты (требуют Docker)
dotnet test tests/Infrastructure.IntegrationTests
```

**Фреймворки:**
- NUnit 4.5.1
- Moq 4.20.72
- Shouldly 4.3.0
- Testcontainers 4.13.0 (PostgreSQL для интеграционных тестов)

## Структура базы данных

### Основные таблицы

| Таблица | Описание |
|---------|----------|
| `AspNetUsers` | Пользователи (ApplicationUser: Id, UserName, Email, TgUserId, DisplayName, AvatarUrl) |
| `AspNetRoles` | Роли |
| `AspNetUserClaims` / `AspNetRoleClaims` | Гранулярные права (claim_type = "permission") |
| `song` | Песни (title, artist, link_kind, link_url, is_featured) |
| `song_role` | Роли песни (song_id, role) |
| `song_role_assignment` | Назначения пользователей на роли |
| `event` | События (title, start_at, location, notify_*) |
| `event_track_item` | Треклист события |
| `event_participant` | Участники событий |
| `refresh_tokens` | JWT refresh-токены |
| `tg_auth_user` | Telegram-сессии авторизации |
| `song_topic` | Связь с Telegram-топиками |
| `calendar` | ICS-календари пользователей |
| `calendar_attach_state` | Состояние бота при привязке календаря |

### Система прав (Permissions)

Claims с `claim_type = "permission"`:

| Permission | Описание |
|------------|----------|
| `participation.edit_own` | Редактирование своих участий |
| `participation.edit_any` | Редактирование любых участий |
| `songs.edit_own` | Редактирование своих песен |
| `songs.edit_any` | Редактирование любых песен |
| `songs.edit_featured` | Редактирование избранных песен |
| `events.edit` | Редактирование событий |
| `tracklists.edit` | Редактирование треклистов |

Новым пользователям выдаются: `songs.edit_own` + `participation.edit_own`.
Роль `Administrator` получает все permissions.

**Mermaid-диаграмма:** См. [`ER_SCHEME.md`](ER_SCHEME.md)

## CI/CD

### GitHub Actions

| Workflow | Триггер | Описание |
|----------|---------|----------|
| [`containers.yml`](.github/workflows/containers.yml) | Manual | Build & Push Docker-образов на Docker Hub |
| [`deploy.yml`](.github/workflows/deploy.yml) | Push в `master` / Manual | Деплой на VDS через SSH |

### Docker-образы

- `docker.io/<username>/musicclub-backend:<sha>`
- `docker.io/<username>/musicclub-frontend:<sha>`
- `docker.io/<username>/musicclub-bot:<sha>`

### Деплой на VDS

```bash
# Автоматически при push в master
# Или вручную через GitHub Actions

# Скрипт деплоя:
cd /opt/musicclub
cd source && git fetch && git reset --hard origin/master && git clean -fdx && cd -
docker compose -f docker-compose.yml up -d --build
```

## Конвенции разработки

### C# стиль (`.editorconfig`)

- **Отступы:** 4 пробела, `csharp_indent_case_contents = true`
- **Фигурные скобки:** `csharp_prefer_braces = false:warning` (разрешены однострочные)
- **var:** `csharp_style_var_for_built_in_types = true:suggestion`
- **Expression-bodied members:** Предпочитаются для методов/свойств в одну строку
- **Null-пропагация:** `csharp_style_null_propagation = true:warning`
- **Сортировка using:** `dotnet_sort_system_directives_first = true`

### Архитектурные принципы

1. **Domain-Driven Design:** Сущности в `Domain/`, логика в `Application/`
2. **Dependency Injection:** Все зависимости через конструктор
3. **Guard Clauses:** `Ardalis.GuardClauses` для валидации аргументов
4. **FluentValidation:** Валидация моделей через отдельные валидаторы
5. **EF Core:** Fluent API в `Infrastructure/Data/Configurations/`

### Именование

- **Пространства имён:** `CuMusicClub.{Layer}` (например, `CuMusicClub.Domain.Entities`)
- **Сущности:** Единственное число (`Song`, `Event`, `ApplicationUser`)
- **Таблицы БД:** snake_case, единственное число (`song`, `event`, `song_role`)
- **DTO:** Суффикс `Dto` или `Request`/`Response`

### Telegram-интеграция

- **Бот:** `Telegram.Bot` SDK
- **Аутентификация:** Поток `/start auth_<uuid>` → `tg_auth_user`
- **Уведомления:** `notify_day_before`, `notify_hour_before` в `event`
- **WebApp:** `Telegram__WebAppUrl` в `.env`

## Переменные окружения

### Backend (`.env`)

```ini
# Приложение
Logging__LogLevel__Default=Information
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080

# База данных
ConnectionStrings__CuMusicClubDb=Server=db;Port=5432;Database=db;Username=admin;Password=password

# Telegram
Telegram__BotToken=<token>
Telegram__ChatId=<chat_id>
Telegram__SkipChatMembershipCheck=true
Telegram__WebAppUrl=http://localhost:80

# Безопасность
Security__Secret=<min-32-chars>

# Frontend
API_URL=http://localhost:80/api
```

### Frontend

```ini
API_URL=http://localhost:80/api
HOST=0.0.0.0
PORT=5173
```

## Полезные команды

```bash
# Проверка здоровья сервисов
docker compose ps
docker compose logs -f backend
docker compose logs -f frontend

# Пересборка backend
docker compose up -d --build backend

# Очистка (данные БД сохранятся в volume)
docker compose down

# Полная очистка (включая БД)
docker compose down -v

# OpenAPI-документация (генерируется в wwwroot/openapi/)
# Доступна через Scalar UI: /scalar/v1
```

## Контакты и ресурсы

- **Исходный код:** GitLab (внутренний)
- **Docker Hub:** `docker.io/<username>/musicclub-*`
- **VDS:** `/opt/musicclub`
- **База данных:** PostgreSQL 18, порт 5432
- **Документация БД:** [`ER_SCHEME.md`](ER_SCHEME.md)
- **Схема БД (визуальная):** [`docs/musicclub_v2.png`](docs/musicclub_v2.png)

---

*Последнее обновление: 2026-08-26*
