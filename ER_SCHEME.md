# ER-схема приложения Music Club

Схема описывает текущую структуру базы данных (PostgreSQL), формируемую EF Core 10 из флюент-конфигураций в `src/Infrastructure/Data/Configurations/`.

- Таблицы домена — в snake_case, единственное число (`song`, `song_role`).
- UUID-первичные ключи: `gen_random_uuid()`; метки времени — `NOW()`.
- `link_kind` — enum PostgreSQL `song_link_type` (`youtube` | `yandex_music` | `soundcloud`).
- **Единая модель пользователя — ASP.NET Identity.** `ApplicationUser` (`src/Domain/Entities/`, extends `IdentityUser`) маппится в `AspNetUsers` и расширен колонками `TgUserId`, `IsChatMember`, `DisplayName`, `AvatarUrl`, `CreatedAt`/`UpdatedAt`. Легаси-таблиц `app_user`/`user_permissions` нет.
- **Гранулярные права — `permission` claims** в `AspNetUserClaims`/`AspNetRoleClaims` (claim type `"permission"`, значения `participation.edit_own`, `participation.edit_any`, `songs.edit_own`, `songs.edit_any`, `songs.edit_featured`, `events.edit`, `tracklists.edit`).

## Mermaid-диаграмма

```mermaid
erDiagram
    AspNetUsers {
        uuid id PK
        text user_name UK
        text normalized_user_name UK
        text email
        text normalized_email UK
        text password_hash
        bigint tg_user_id UK
        boolean is_chat_member
        text display_name
        text avatar_url
        timestamptz created_at
        timestamptz updated_at
    }

    AspNetUserClaims {
        int id PK
        uuid user_id FK
        text claim_type
        text claim_value
    }

    AspNetRoles {
        uuid id PK
        text name UK
        text normalized_name UK
    }

    AspNetRoleClaims {
        int id PK
        uuid role_id FK
        text claim_type
        text claim_value
    }

    AspNetUserRoles {
        uuid user_id PK, FK
        uuid role_id PK, FK
    }

    song {
        uuid id PK
        text title
        text artist
        text description
        song_link_type link_kind
        text link_url
        uuid created_by FK
        text thumbnail_url
        boolean is_featured
        timestamptz created_at
        timestamptz updated_at
    }

    song_role {
        uuid song_id PK, FK
        text role PK
    }

    song_role_assignment {
        uuid id PK
        uuid song_id FK
        text role
        uuid user_id FK
        timestamptz joined_at
    }

    event {
        uuid id PK
        text title
        timestamptz start_at
        text location
        boolean notify_day_before
        boolean notify_hour_before
        uuid created_by FK
        timestamptz created_at
        timestamptz updated_at
    }

    event_track_item {
        uuid id PK
        uuid event_id FK
        int position
        uuid song_id FK
        text custom_title
        text custom_artist
    }

    event_participant {
        uuid id PK
        uuid event_id FK
        uuid track_item_id FK
        uuid user_id FK
        text role
        timestamptz joined_at
    }

    tg_auth_user {
        uuid id PK
        uuid user_id FK
        bigint tg_user_id UK
        boolean success
    }

    refresh_tokens {
        uuid id PK
        uuid user_id FK
        text token UK
        timestamptz expires_at
        timestamptz created_at
    }

    song_topic {
        uuid song_id PK, FK
        bigint topic_id
        timestamptz created_at
        timestamptz updated_at
    }

    calendar {
        uuid user_id PK, FK
        text calendar_url
        timestamptz created_at
        timestamptz updated_at
    }

    calendar_attach_state {
        bigint tg_user_id PK
        smallint state
        uuid pending_user_id
        text pending_email
        timestamptz updated_at
    }

    AspNetUsers ||--o{ AspNetUserClaims : "permission claims (CASCADE)"
    AspNetUsers ||--o{ AspNetUserRoles : "членство в ролях (CASCADE)"
    AspNetRoles ||--o{ AspNetRoleClaims : "permission claims (CASCADE)"
    AspNetRoles ||--o{ AspNetUserRoles : "роли (CASCADE)"

    AspNetUsers ||--o{ song : "создатель (SET NULL)"
    AspNetUsers ||--o{ event : "создатель (SET NULL)"
    AspNetUsers ||--o{ song_role_assignment : "участие (CASCADE)"
    AspNetUsers ||--o{ event_participant : "участие (CASCADE)"
    AspNetUsers ||--o| calendar : "1:1 (CASCADE)"
    AspNetUsers ||--o{ refresh_tokens : "токены (CASCADE)"
    AspNetUsers ||--o{ tg_auth_user : "tg-сессии (CASCADE)"

    song ||--o{ song_role : "роли (CASCADE)"
    song ||--o{ song_role_assignment : "назначения (CASCADE)"
    song_role ||--o{ song_role_assignment : "роль существует (CASCADE)"
    song ||--o{ event_track_item : "треки (SET NULL)"
    song ||--o| song_topic : "1:1 (CASCADE)"

    event ||--o{ event_track_item : "треклист (CASCADE)"
    event ||--o{ event_participant : "участники (CASCADE)"
    event_track_item ||--o{ event_participant : "по (event_id, id)"
```

## Таблицы

### AspNetUsers (ApplicationUser)
Единая сущность пользователя приложения (ASP.NET Identity). Расширение `IdentityUser<Guid>` (Guid PK).

| Колонка | Тип | Примечание |
|---|---|---|
| `Id` | uuid PK | Guid-ключ |
| `UserName` / `NormalizedUserName` | text | уникальные |
| `Email` / `NormalizedEmail` | text | уникальный нормализованный |
| `PasswordHash` | text | legacy cookie-логин, UI не использует |
| `TgUserId` | bigint | уникальный (`idx_application_user_tg_user_id`), связь с Telegram |
| `IsChatMember` | boolean | default `false` |
| `DisplayName` | text | default `""` |
| `AvatarUrl` | text | |
| `CreatedAt` / `UpdatedAt` | timestamptz | default `NOW()` |

Остальные колонки Identity (`SecurityStamp`, `ConcurrencyStamp`, `PhoneNumber`, `LockoutEnd` и т.д.) — стандартные.

### AspNetUserClaims / AspNetRoleClaims
Гранулярные права в виде claims: `claim_type = 'permission'`, `claim_value` — одно из `participation.edit_own`, `participation.edit_any`, `songs.edit_own`, `songs.edit_any`, `songs.edit_featured`, `events.edit`, `tracklists.edit`. Claims на роли и на пользователя складываются при построении principal. Новым пользователям выдаются `songs.edit_own` + `participation.edit_own`; роль `Administrator` сидится со всеми `permission` claims.

### song
Песня. FK `created_by` → `AspNetUsers.Id` с `ON DELETE SET NULL`.

### song_role
Роль для песни. Составной PK `(song_id, role)`. FK → `song.id` с CASCADE.

### song_role_assignment
Назначение пользователя на роль песни. FK `(song_id, role)` → `song_role(song_id, role)` (`song_role_exists`, CASCADE) — назначение возможно только на существующую роль. Уникальность `(song_id, role, user_id)`.

### event
Событие. FK `created_by` → `AspNetUsers.Id` с `SET NULL`. Индекс `idx_event_start_at`.

### event_track_item
Пункт треклиста события. Ограничение `track_item_requires_title`: `song_id IS NOT NULL OR custom_title IS NOT NULL`. Уникальность `(event_id, position)`. Альтернативный ключ `(event_id, id)` (`track_item_identity`) — по нему ссылаются участники.

### event_participant
Участие пользователя в событии на роли (возможно, на конкретном треке). FK `(event_id, track_item_id)` → `event_track_item(event_id, id)` (`fk_event_participant_track_item`, CASCADE). Уникальность `(event_id, role, user_id, track_item_id)` (`uniq_event_participation`).

### tg_auth_user
Сессия подтверждения Telegram-авторизации (для потока `/start auth_<uuid>`). FK `user_id` → `AspNetUsers.Id` с CASCADE. `tg_user_id` уникален.

### refresh_tokens
Ротируемые refresh-токены. FK `user_id` → `AspNetUsers.Id` с CASCADE. `token` уникален.

### song_topic
Связь песни с топиком Telegram-канала (1:1). FK → `song.id` с CASCADE. `topic_id` не уникален (индекс `idx_song_topic_topic_id`).

### calendar
Ссылка на ICS-календарь пользователя (1:1). FK `user_id` → `AspNetUsers.Id` с CASCADE.

### calendar_attach_state
Состояние машины состояний бота при привязке календаря (short state 1/2/3). PK — `tg_user_id`. FK на пользователя нет (связь по `tg_user_id`).

## Сводка связей

| От | К | FK | On delete |
|---|---|---|---|
| AspNetUserClaims | AspNetUsers | `UserId` | CASCADE |
| AspNetUserRoles | AspNetUsers | `UserId` | CASCADE |
| AspNetUserRoles | AspNetRoles | `RoleId` | CASCADE |
| AspNetRoleClaims | AspNetRoles | `RoleId` | CASCADE |
| song | AspNetUsers | `created_by` | SET NULL |
| song_role | song | `song_id` | CASCADE |
| song_role_assignment | song | `song_id` | CASCADE |
| song_role_assignment | AspNetUsers | `user_id` | CASCADE |
| song_role_assignment | song_role | `(song_id, role)` | CASCADE |
| event | AspNetUsers | `created_by` | SET NULL |
| event_track_item | event | `event_id` | CASCADE |
| event_track_item | song | `song_id` | SET NULL |
| event_participant | event | `event_id` | CASCADE |
| event_participant | event_track_item | `(event_id, track_item_id)` | CASCADE |
| event_participant | AspNetUsers | `user_id` | CASCADE |
| song_topic | song | `song_id` | CASCADE |
| calendar | AspNetUsers | `user_id` | CASCADE |
| tg_auth_user | AspNetUsers | `user_id` | CASCADE |
| refresh_tokens | AspNetUsers | `user_id` | CASCADE |

## Примечания
- `calendar_attach_state` не имеет FK на пользователя (связь по `tg_user_id`).
- Enum `song_link_type` маппится в Npgsql через `MapEnum<SongLinkType>()`.
- Dev-база создаётся через `EnsureDeletedAsync` + `EnsureCreatedAsync` (миграции не применяются).
