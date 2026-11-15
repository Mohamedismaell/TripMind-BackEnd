# TripMind BackEnd — Project Structure

**TripMind** is an **AI-driven Egyptian Domestic Tourism Planner** backend built with **.NET 8** using the **Clean Architecture** pattern (4 project layers) backed by **SQL Server / Entity Framework Core**.

---

## Table of Contents

1. [Solution Overview](#solution-overview)
2. [Layering & Dependency Rules](#layering--dependency-rules)
3. [Top-Level Files](#top-level-files)
4. [TripMind.Domain](#1-tripminddomain--)
5. [TripMind.Application](#2-tripmindapplication--)
6. [TripMind.Infrastructure](#3-tripmindinfrastructure--)
7. [TripMind.API](#4-tripmindapi--)
8. [Database Migrations](#database-migrations)
9. [Request → Response Flow](#request--response-flow)

---

## Solution Overview

```
TripMind.sln
 ├── TripMind.Domain          (Class Library — pure entities/enums, no dependencies)
 ├── TripMind.Application     (Class Library — DTOs, interfaces, business rules)
 ├── TripMind.Infrastructure  (Class Library — EF Core, JWT, email, AI client, images)
 └── TripMind.API             (ASP.NET Core Web API — controllers, middleware, startup)
```

The API exposes the following feature areas (all under `/api/v1`):

| Area       | Route prefix          | Purpose                                                        |
|------------|-----------------------|----------------------------------------------------------------|
| Auth       | `/api/v1/auth`        | Register, login, OTP flows, 2FA, password reset, Google/Facebook |
| Users      | `/api/v1/users`       | Profile, dashboard stats, interests, photo upload, delete account |
| Trips      | `/api/v1/trips`       | Create/plan/update trips, share link, reviews                   |
| Favorites  | `/api/v1/favorites`   | Save favorite places & trips                                    |
| AI         | `/api/v1/ai`          | AI plan generation, chatbot, edit, places discovery & search    |

---

## Layering & Dependency Rules

```
TripMind.API               references → TripMind.Application, TripMind.Infrastructure
TripMind.Infrastructure    references → TripMind.Application, TripMind.Domain
TripMind.Application       references → TripMind.Domain
TripMind.Domain            references → nothing (pure C#)
```

- The API layer is the **composition root**: it wires concrete infrastructure implementations to the Application interfaces (see `Program.cs`).
- The Application layer only knows **interfaces** (e.g. `IAppDbContext`, `IJwtProvider`), never EF Core directly.
- The Domain layer has **zero dependencies**.

---

## Top-Level Files

| File | Purpose |
|------|---------|
| `TripMind.sln` | Visual Studio solution that groups the 4 projects (fixed fake GUIDs for Domain/Application/Infrastructure/API). |
| `Dockerfile` | Multi-stage Docker build: `base` (aspnet:8.0) → `build` (restore) → `publish` → `final`. Exposes ports 8080/8081, runs `dotnet TripMind.API.dll`. |
| `.dockerignore` | Excludes `.git`, `.vs`, `bin`, `obj` from the Docker build context. |
| `.gitignore` | Ignores `bin/`, `obj/`, `*.user`, `.vs/`, `.env`, Production/Development `appsettings` files, and logs. |
| `.gitattributes` | Git line-ending/attribute normalization rules. |
| `HOW_TO_RUN.md` | Manual: prerequisites, connection string (`DESKTOP-0MDMFGG\MSSQLSERVER04`), running migrations, testing endpoints in Swagger, known errors. |
| `Migrations/` | Standalone SQL migration script (see [Database Migrations](#database-migrations)). |

---

## 1. TripMind.Domain  📦

Pure domain model — entities and enums with no external dependencies (the csproj redundantly lists packages used elsewhere, but the code itself is clean).

### `TripMind.Domain.csproj`
Project definition; targets `net8.0`, `OutputType=Library`. (Package references are duplicated leftovers only — no domain file uses them.)

### `Enums/`

| File | Purpose |
|------|---------|
| `TripStatus.cs` | Trip lifecycle: `Draft (0)`, `InProgress (1)`, `Complete (2)`. |
| `ModerationStatus.cs` | Content moderation state: `Pending`, `Approved`, `Rejected`. |
| `LocationCategory.cs` | Place categories: Museum, Beach, Restaurant, Hotel, Mosque, Church, Market, Park, Cafe, Heritage, Entertainment, Nature, Shopping, Other. |

### `Entities/`

| File | Purpose |
|------|---------|
| `User.cs` | App user account. Email (unique), `PasswordHash`, `DisplayName`, optional `Username`/`PhoneNumber`/`Bio`/`ProfilePhotoUrl`/`HomeGovernorate`, `LanguagePreference` (AR default), `RememberMe`, `IsActive`. Security fields: `GoogleId`/`FacebookId` for social login, `IsEmailVerified` + `EmailVerificationOtp`/`EmailOtpExpiry`, `TwoFactorEnabled` + `TwoFactorOtp`/`TwoFactorOtpExpiry`, `FailedLoginAttempts` + `LockoutEnd` (brute-force lockout), `PasswordResetToken`/`ResetTokenExpiry`. Timestamps `CreatedAt`/`UpdatedAt`. Navigation collections: Trips, UserInterests, AuditLogs, RefreshTokens. |
| `Trip.cs` | A user's planned trip. Destination (governorate/city), `StartDate`/`EndDate`/`DurationDays`, `People`, `TotalBudgetEgp`/`TotalCost`, AI-session data (`SessionId`, `CollectedJson`, `PlanJson`), `Status` (Draft→InProgress→Complete), `IsPublic` + `ShareToken` for sharing. Nav: owner `User`, `TripReviews`. |
| `TripReview.cs` | Rating (1–5) + optional comment left by a user on a completed trip. Unique per (TripId, UserId). |
| `UserInterest.cs` | Tag-based interest for recommendation personalization; `InterestTag` + `Weight`. |
| `RefreshToken.cs` | Long-lived token for the refresh flow. `Token`, `ExpiresAt`, `IsRevoked`, `ReplacedByToken`, `CreatedByIp`. Non-mapped computed prop `IsActive` = not revoked and not expired. |
| `FavoritePlace.cs` | "Save" of an AI places-database point (`PlaceId`) per user. |
| `FavoriteTrip.cs` | "Save" of another of the user's own trips. |
| `AuditLog.cs` | Security/activity audit record: `EventType` (e.g. `AUTH.LOGIN`), IP, User-Agent, success flag, details, timestamp. |

---

## 2. TripMind.Application  📦

Contains request/response **DTOs** (with DataAnnotations validation), the service **interfaces**, and one concrete service (`AuthService` — chosen to live here since it depends only on abstractions + Google.Apis.Auth).

### `TripMind.Application.csproj`
References Domain. Packages: `Microsoft.EntityFrameworkCore` (for `DbSet` in `IAppDbContext`), `Google.Apis.Auth` (Google ID-token validation), `Microsoft.Extensions.Configuration.Abstractions`.

### `Interfaces/`

| File | Purpose |
|------|---------|
| `IAuthService.cs` | Auth contract: register, login, OTP flows, social login, refresh/revoke/logout, password reset/change, 2FA enable/confirm/disable. |
| `IUserService.cs` | User contract: get/update profile, dashboard stats, update interests, delete account. |
| `ITripService.cs` | Trip contract: CRUD, plan update, rename, status transitions, share-link + shared view, trip reviews (add/update/delete/list/mine). |
| `IFavoritesService.cs` | Favorites contract: add/remove/list favorite places and trips. |
| `IJwtProvider.cs` | JWT contract: generate access token, generate refresh token, refresh-token lifetime (remember-me aware). |
| `IImageService.cs` | Image contract: upload profile photo, delete by URL. |
| `IAiService.cs` | AI contract: plan generation, chat, edit, home feed, recommend, search, nearby, top-rated, get places, get place by id — all return raw `JsonElement`. |
| `IAppDbContext.cs` | The EF-Core unit-of-work abstraction the Application depends on (so no hard EF reference is needed by services): exposes `DbSet`s for Users, AuditLogs, RefreshTokens, FavoritePlaces, FavoriteTrips, Trips, TripReviews + `SaveChangesAsync`. |

### `Services/`

| File | Purpose |
|------|---------|
| `AuthService.cs` | The full authentication engine. Register (blocks disposable domains like `test.com`/`mailinator.com`, hashes OTPs with BCrypt, sends verification email), login (5-failed-attempt → 15-min lockout, optional 2FA gate), email verification + resend, Google login (via `GoogleJsonWebSignature.ValidateAsync`), Facebook login (calls `graph.facebook.com/me` with the access token), refresh-token rotation, logout/revoke, forgot-password OTP + verify + reset (revokes all active tokens on reset), change password, 2FA initiate/confirm/disable, resend OTPs. Throws the specialized `TripMind.Application.Exceptions` types (`Conflict`, `BadRequest`, `Unauthorized`, `Forbidden`, `NotFound`, `ServiceUnavailable`). Also declares the internal helper interfaces `IPasswordHasher` and `IEmailSender`, and Facebook JSON helper classes. |

### `DTOs/Auth/AuthDtos.cs`
All auth request/response shapes with validation: `RegisterRequest`, `LoginRequest`, `VerifyEmailOtpRequest`, `ResendEmailOtpRequest`, `LoginOtpRequest`, `ChangePasswordRequest`, `TwoFactorConfirmRequest`, `TwoFactorDisableRequest`, `RefreshTokenRequest`, `LogoutRequest`, `ForgotPasswordRequest`, `VerifyOtpRequest`, `GoogleLoginRequest`, `FacebookLoginRequest`, `ResetPasswordRequest`, and responses `TokenResponse` (access/refresh pair + user info), `PendingTwoFactorResponse`, `VerifyOtpResponse`, `ResetPasswordResponse`, `MessageResponse`.

### `DTOs/User/UserDtos.cs`
`UserProfileResponse`, `UserDashboardResponse` (TotalTrips/TotalReviews/TotalSaved), `UpdateProfileRequest` (validates username/phone), `UpdateInterestsRequest` (max 20 interests).

### `DTOs/Trip/`

| File | Purpose |
|------|---------|
| `TripDtos.cs` | `CreateTripRequest`/`UpdateTripRequest` (accept `Plan`/`Collected` raw `JsonElement`, custom validation: destination required, end≥start, ≤60 days, cost≤budget), `RenameTripRequest`, `TripSearchRequest` (sort/order/page), `TripResponse`, `PublicTripResponse` (share view), `TripReviewRequest/Response`, `MyTripReviewResponse`, generic `PagedResult<T>`. |
| `TripPlanValidator.cs` | Static recursion validating an AI-generated plan `JsonElement`: ≤200 KB, must be an object with at least one `day*` key, and every "place" node must have a non-empty `name`. |
| `TripReviewWithUserResponseDtos.cs` | Review DTO that also carries the reviewer's `DisplayName` for list views. |

### `DTOs/Favorite/FavoriteDtos.cs`
`FavoritePlaceRequest` (placeId), `FavoritePlaceResponse` (embeds the full `Place` `JsonElement` from the AI service), `FavoriteTripResponse`.

### `DTOs/Ai/`

| File | Purpose |
|------|---------|
| `AiDtos.cs` | Request/response models for the AI planner integration: `GeneratePlanRequest` (city/days/budget/people/interests/must_include), `ChatRequest` + `ChatCollected`, `EditRequest` + `PlanItemRequest` + `ConversationTurn`, `HomeRequest`, `RecommendRequest` (validates `PoolSize >= Limit`), `SearchPlacesRequest`, `NearbyRequest`, `TopRatedRequest`, `GetPlacesRequest`. |
| `AiAllowedValues.cs` | Central whitelists: supported Egyptian `Cities`, display `Interests`, `PlaceCategorySlugs`, `SortByValues`, `OrderValues`. |
| `AiValidation.cs` | `CustomValidation` helpers used by the DTOs to reject non-whitelisted cities/interests/categories/sort/order. |
| `ChatCardAnswersRequest.cs` | The card-style wizard answers forwarded to the AI chatbot. |
| `DayFieldConverter.cs` | JSON converter that maps the string `"accommodation"` to day `0` and numeric strings to ints for the plan's `day` field. |
| `PlaceFiltersRequest.cs` | Shared filter object (city_en, category, interests, rating/price ranges, hidden_gem, sort/order) used across recommend/search/nearby/top-rated/getplaces. |

---

## 3. TripMind.Infrastructure  📦

Concrete implementations: EF Core persistence, JWT, BCrypt, Brevo/Stub email, Cloudinary, AI HTTP client, background jobs.

### `TripMind.Infrastructure.csproj`
References Application + Domain. Packages: EF Core SqlServer/Tools, JwtBearer, Google.Apis.Auth, CloudinaryDotNet, System.IdentityModel.Tokens.Jwt, BCrypt.Net-Next, EF HealthChecks. Has `UserSecretsId` `8698b3ee-...` for local secrets.

### `Persistence/`

| File | Purpose |
|------|---------|
| `TripMindDbContext.cs` | The EF Core `DbContext` implementing `IAppDbContext`. Declares `DbSet`s for all entities; `OnModelCreating` applies all `IEntityTypeConfiguration`s from the assembly. Explicitly maps the interface `DbSet`s too (so the interface works against the same instance). |
| `DesignTimeDbContextFactory.cs` | `IDesignTimeDbContextFactory<TripMindDbContext>` so `dotnet ef` commands can build the context at design time from user-secrets + environment variables. |

### `Persistence/Configurations/`

| File | Purpose |
|------|---------|
| `UserConfiguration.cs` | Table mapping for `Users`: unique email index, unique filtered username index, max lengths, defaults (`NEWID()`, `GETUTCDATE()`, `AR` language). |
| `TripConfiguration.cs` | Mapping for `Trips`: `Status` stored as string, nvarchar(max) for Plan/Collected JSON, unique filtered `ShareToken` index, `SessionId` index, cascade delete from User. |
| `RefreshTokenConfiguration.cs` | `RefreshTokens`: unique token index, IP length 45, cascade from User, `IsActive` ignored by EF. |
| `AuditLogConfiguration.cs` | `AuditLogs`: composite index `(EventType, CreatedAt)`, `SetNull` when user deleted. |
| `OtherConfigurations.cs` | `UserInterestConfiguration`, `FavoritePlaceConfiguration` (unique `(UserId,PlaceId)`, **NoAction** FK — deleted manually before user removal), `FavoriteTripConfiguration` (unique `(UserId,TripId)`, Trip cascade, User NoAction), `TripReviewConfiguration` (unique `(TripId,UserId)`, Trip cascade, User NoAction). |

### `Security/`

| File | Purpose |
|------|---------|
| `PasswordHasher.cs` | BCrypt wrapper, work factor **12**. |
| `JwtProvider.cs` | Generates 15-minute HS256 access tokens (claims: nameid, sub, email, name, jti, iat, `lang`, `governorate`), 64-byte cryptographically-random URL-safe refresh tokens, and refresh lifetime 30 days (remember-me) / 7 days. |

### `Email/`

| File | Purpose |
|------|---------|
| `BrevoEmailSender.cs` | Real transactional email via **Brevo** (`api.brevo.com/v3/smtp/email`) with `api-key` header. Sends password-reset, email-verification, and 2FA OTP emails (HTML). |
| `StubEmailSender.cs` | Development fallback that prints the OTP to the console instead of sending. Selected in `Program.cs` when `Email:Provider != "Brevo"`. |

### `BackgroundJobs/`

| File | Purpose |
|------|---------|
| `UnverifiedUserCleanupJob.cs` | `BackgroundService` hourly loop that deletes users who never verified their email within 24h. |

### `Services/`

| File | Purpose |
|------|---------|
| `UserService.cs` | Implements `IUserService`: profile read/update (username uniqueness check), dashboard counters, replace-all interests, account deletion (manually removes favorites/reviews first — DB uses NoAction FKs). |
| `TripService.cs` | Implements `ITripService`: create (auto-title, share token, plan/collected JSON validation/size caps), update plan/rename/status (guarded transitions Draft→InProgress→Complete, completed trips locked), paged list with sort/order, share-link + public lookup, full review CRUD (only-trip-owner reviews, only on `Complete` trips, one per user). Also walks the plan JSON to count places, pick a cover image, and compute a rough `ProgressPercent`. |
| `FavoritesService.cs` | Implements `IFavoritesService`: favorite places (validates the place via the AI service, returns embedded place data; listing hydrates places with a semaphore-throttled (5) parallel AI lookups), favorite trips (same user's trips only). |
| `CloudinaryImageService.cs` | Implements `IImageService`: uploads profile photos to Cloudinary (`tripmind/profiles` folder, 400×400 face-crop, auto quality, jpg/png/webp only, ≤5 MB), deletes by extracting the public id from the URL. |
| `AiService.cs` | Implements `IAiService`: thin HTTP passthrough to the AI microservices (`PlannerUrl`, `ChatUrl`, `EditUrl`, `RecommendBase` from config). 120 s timeout; every call goes through shared `PostAsync`/`GetAsync` error handling (timeouts → 503 message, non-2xx → `AiServiceException`, invalid JSON → friendly error). `GetPlaceByIdAsync` maps 404 → `KeyNotFoundException`. |
| `AiServiceException.cs` | Exception carrying the upstream `StatusCode` + truncated raw body for the AI-service errors. |

### `Migrations/`

EF Core migration files + snapshot (see [Database Migrations](#database-migrations) for the full list).

---

## 4. TripMind.API  🌐

The startup web project. Wires DI, middleware pipeline, Swagger, CORS, health checks.

### `TripMind.API.csproj`
`Microsoft.NET.Sdk.Web`; references Application + Infrastructure; packages: EF Core Design tools, Swashbuckle, health-checks.

### `Program.cs`
Startup entry point that:
- Registers `TripMindDbContext` (SQL Server, migration assembly = Infrastructure, 30 s command timeout, 3× retry).
- Limits Kestrel request body to **2 MB**.
- Registers services: `IAppDbContext`→`TripMindDbContext`, `IJwtProvider`→`JwtProvider`, `IPasswordHasher`→`PasswordHasher`, and all application services (`IAuthService`, `IUserService`, `ITripService`, `IFavoritesService`, `IImageService`→Cloudinary, `IAiService` via typed `HttpClient`, `IEmailSender`→Brevo **or** Stub by config). Adds `UnverifiedUserCleanupJob` hosted service. Registers named `"Facebook"` HttpClient.
- Configures **JWT Bearer** auth (requires `Jwt:Secret` from secrets/env — throws if absent), authorization, **gzip/brotli response compression**, controllers with a unified 400 validation-error shape, **CORS** from `Cors:AllowedOrigins`, Swagger with a **Bearer** security definition, and a **DB health check** at `/health`.
- Runs `MigrateAsync()` automatically in Development (or when `Database:AutoMigrate`), failing hard if migration fails.
- Pipeline order: `ExceptionMiddleware` → `RateLimitingMiddleware` → ResponseCompression → Swagger → HTTPS redirect → CORS → Authentication → Authorization → `AuditLogMiddleware` → StaticFiles → Controllers → `/health`. Root `/` redirects to `/swagger`.

### `appsettings.json`
Non-secret config: JWT issuer/audience, AI service URLs (n8n webhook endpoints + Railway recommendation server), `BaseUrl` (`https://tripmind.runasp.net`), Email provider (`Brevo`), logging levels, allowed hosts, CORS origins (`app.tripmind.com`, `tripmind.com`). Secrets (JWT secret, Brevo key, Cloudinary creds, connection string) live in user-secrets/env vars (files are gitignored).

### `Controllers/`

| File | Purpose |
|------|---------|
| `AuthController.cs` | `api/v1/auth` — register (201), login (may return 200 token pair or `PendingTwoFactorResponse`), login/verify, login/resend-otp, email/verify, email/resend-otp, google, facebook, refresh, logout (auth), revoke (auth), password/forgot → verifyotp → reset + resend-otp, password/change (auth), 2FA initiate/confirm/disable/resend-otp (auth), `me` (auth, echoes current claims). Captures client IP from `X-Forwarded-For` (first value) or the socket. |
| `UsersController.cs` | `api/v1/users` (all `[Authorize]`) — GET/PATCH `me`, POST `me/photo` (validates, uploads to Cloudinary, updates profile, deletes the old image best-effort), GET `me/dashboard`, DELETE `me` (account deletion), PUT `me/interests`, GET `me/reviews`. |
| `TripsController.cs` | `api/v1/trips` (`[Authorize]`) — POST create, GET list (paged/sorted), GET `{id}`, PUT `{id}/plan`, PATCH `{id}/rename`, PATCH `{id}/status`, POST `{id}/share`, GET `share/{token}` (**`[AllowAnonymous]`** preview), review endpoints (POST/PATCH/DELETE `{id}/review`, GET `{id}/reviews`, GET `{id}/review/me`), DELETE `{id}`. Maps domain exceptions to 404/409. |
| `FavoritesController.cs` | `api/v1/favorites` (`[Authorize]`) — GET/POST `places`, DELETE `places/{placeId}`, GET/POST/DELETE `trips`. |
| `AiController.cs` | `api/v1/ai` (`[Authorize]`) — POST `generate-plan`, `chat`, `edit`, `places/home`, `places/recommend`, `places/search`, `places/nearby`, `places/top-rated`, `places/getplaces`, GET `places/{placeId}`. Passes through the AI service; surfaces `AiServiceException` status codes (raw body only in Development) and 503 on timeouts. |
| `HomeController.cs` | Hidden from Swagger; redirects `/` → `/swagger`. |

### `Middleware/`

| File | Purpose |
|------|---------|
| `ExceptionMiddleware.cs` | Global exception handler returning RFC-7807 problem+json. Maps `AppException` (carries `StatusCode` + `ErrorCode`) to its status with an `errorCode` extension; unexpected exceptions → 500 logged as `INTERNAL_SERVER_ERROR`. Full detail only for `AppException.Message`; unexpected errors get a generic message. |
| `RateLimitingMiddleware.cs` | Per-IP sliding-window limiter: **100 requests / 60 s** → 429 with `Retry-After`; periodic cleanup of stale entries. |
| `AuditLogMiddleware.cs` | After a request on a sensitivity-mapped path (all `/auth/*`, profile changes, trip create/delete/update/share, favorites), writes an `AuditLog` row deriving an `EventType` like `AUTH.LOGIN.SUCCESS` / `TRIP.CREATE.FAILURE`. |
| `SwaggerRedirectMiddleware.cs` | Redirects `/` → `/swagger` (redundant with the `MapGet` in `Program.cs`). |

### `Extensions/`

| File | Purpose |
|------|---------|
| `ClaimsPrincipalExtensions.cs` | `GetUserId()` helper that parses the `NameIdentifier` claim into a `Guid` or throws `UnauthorizedAccessException`. |

### `Properties/`

| File | Purpose |
|------|---------|
| `launchSettings.json` | Dev profile: launches browser on `/swagger`, listens on `https://localhost:49689` and `http://localhost:5001`, `ASPNETCORE_ENVIRONMENT=Development`. |
| `serviceDependencies.json` / `serviceDependencies.local.json` | Visual Studio connected-services metadata marking the Azure SQL dependency (`mssql1`) as ignored. |
| `PublishProfiles/site61701-WebDeploy.pubxml` | MSDeploy publish profile targeting the hosted `site61701.siteasp.net` server (deploys to `https://tripmind.runasp.net`). |

### `.config/dotnet-tools.json`
Local tool manifest pinning **`dotnet-ef` 10.0.3** for running EF Core migrations via `dotnet ef`.

---

## Database Migrations

EF Core migrations live in `TripMind.Infrastructure/Migrations/` (one `.cs` + `.Designer.cs` per migration + a shared `TripMindDbContextModelSnapshot.cs` that describes the *current* schema). Migration history (guides what each adds/changes):

| Migration | What it does |
|-----------|--------------|
| `20260310025613_InitialCreate` | Initial schema: Users, Trips, UserInterests, AuditLogs, RefreshTokens (+ all indexes/keys). |
| `20260310215548_AddIsActiveToUser` | Adds `IsActive` flag to Users (soft deactivation). |
| `20260402124028_AddAuthSecurityFields` | Password-reset & auth security columns: `IsEmailVerified`, `EmailVerificationOtp`/`EmailOtpExpiry`, `TwoFactorEnabled`/`TwoFactorOtp`/`TwoFactorOtpExpiry`, `FailedLoginAttempts`, `LockoutEnd`; fixes `IsActive` default to true. |
| `20260403115049_FixOtpColumnLength` | Widens OTP columns to 512 (they store BCrypt hashes, not raw codes). |
| `20260409155357_AddTourPackages` | Adds tour package tables (and later drops them). |
| `20260412124802_AddUsernamePhoneBio` | Adds `Username`, `PhoneNumber`, `Bio` + unique username index. |
| `20260622055443_AddFavoritesTables` | Big schema alignment: reworks the Trips table (adds `Title`, `People`, `PlanJson`, `CollectedJson`, `SessionId`, `TotalCost`, string `Status`), creates `FavoritePlaces`, `FavoriteTrips`, `TripReviews`, and drops legacy tables (Reviews, ReviewVotes, SavedItineraries). |
| `20260624221119_FixUserDeleteCascade` | Adjusts user-delete FK behavior for the favorites/review tables. |
| `20260625083301_CheckModelChanges` | Realigns FK delete behaviors (`DropForeignKey`/`AddForeignKey`) so the model and DB match. |

### `Migrations/Scripts/20260405_migration.sql`
A standalone, **idempotent** T-SQL script (guarded by `__EFMigrationsHistory`) that replays the early migrations against any server. It covers the first four migrations (InitialCreate with the full original schema — Locations, HiddenGems, LocationFeatures, Reviews, UserPreferences, ReviewVotes, Budgets, SavedItineraries, TripDays, TripLocations, etc. — plus IsActive and the auth-security fields). Useful for bootstrapping a database without `dotnet ef`.

---

## Request → Response Flow

```
Client (Flutter app)
   │  HTTP (Bearer JWT)
   ▼
TripMind.API
   ├─ RateLimitingMiddleware        (100 req/min/IP)
   ├─ Authentication/Authorization  (JWT → ClaimsPrincipal)
   ├─ ExceptionMiddleware           (unified problem+json errors)
   ├─ AuditLogMiddleware            (writes AuditLogs on sensitive paths)
   ├─ Controllers                   (validate DTOs, call services)
   └─ Swagger UI  +  /health check
            │
            ▼
TripMind.Application.Interfaces (IUserService, ITripService, IAiService, …)
            │
            ▼
TripMind.Infrastructure
   ├─ TripMindDbContext  (SQL Server via EF Core)
   ├─ JwtProvider / PasswordHasher   (tokens + BCrypt)
   ├─ BrevoEmailSender / StubEmailSender
   ├─ CloudinaryImageService        (profile photos)
   └─ AiService ──▶ n8n webhooks & places-recommendation service
```