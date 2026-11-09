# BestieSpace – Phase 2: Authentication & User Registration 🌸

Welcome to the **Phase 2** upgrade documentation for **BestieSpace**. This document explains all architectural, security, database, and client changes implemented to deliver a complete, enterprise-grade authentication system while fully preserving BestieSpace's signature cozy Kawaii experience.

---

## 1. Summary of Changes

| Feature | Phase 1 (Foundation) | Upgraded Phase 2 (Authentication System) |
| :--- | :--- | :--- |
| **User Registration** | None | Full public registration with unique username & email checks, password validation, and automatic `User` role assignment |
| **Password Storage** | Plaintext / legacy hint | Cryptographically secure **PBKDF2** with HMAC-SHA256, 100,000 iterations, 128-bit random salt, and constant-time verification |
| **Plaintext Passwords** | Present in legacy rows | **Zero plaintext storage.** Plaintext fields are cleared upon hash upgrade |
| **Authentication Flow** | Simple demo PIN/code | Dual-flow: Secure **JWT Bearer Token** + **Hashed Refresh Token Rotation** & Kawaii Strawberry PIN login |
| **Login Identifiers** | Nickname only | Supports login via **Username OR Email** |
| **Token Management** | None | [`JwtTokenService.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Auth/JwtTokenService.cs) with configurable keys, issuer/audience, and SignalR WebSocket support |
| **Refresh Tokens** | None | [`RefreshTokens`](file:///d:/For%20My%20Besties/BestieSpace.Api/Infrastructure/Data/Entities/RefreshToken.cs) table storing only SHA-256 hashes, with rotation, revocation, and token-reuse breach detection |
| **Blazor State** | Client-side `localStorage` | [`BestieAuthenticationStateProvider.cs`](file:///d:/For%20My%20Besties/BestieSpace.Client/Services/BestieAuthenticationStateProvider.cs) using server-side circuit memory + `ProtectedSessionStorage` (no raw tokens exposed to JS) |
| **Pages & Protection** | Unprotected | Protected route view (`<AuthorizeRouteView>`), dedicated Kawaii [`/login`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Pages/Login.razor) and [`/register`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Pages/Register.razor) pages |
| **RBAC Foundation** | Schema only | Database-driven role and permission claims loaded into JWT (`ClaimTypes.Role`, `permission`) |

---

## 2. Security Architecture

### A. Password Hashing ([`PasswordHasherService.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Auth/PasswordHasherService.cs))
- **Standard Algorithm:** PBKDF2 with HMAC-SHA256.
- **Iterations:** 100,000 rounds.
- **Salt:** 16 bytes (128 bits) of cryptographically secure randomness via `RandomNumberGenerator`.
- **Output Format:** `PBKDF2-SHA256:100000:<saltBase64>:<hashBase64>`.
- **Timing-Attack Resistance:** Verification utilizes `CryptographicOperations.FixedTimeEquals` to prevent side-channel timing analysis.
- **Non-Destructive Migration:** Legacy test accounts (`strawberry_bestie`, `mochi_maker`) are automatically upgraded to PBKDF2 on their next login, simultaneously wiping their legacy plaintext field.

### B. JWT & Token Management ([`JwtTokenService.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Auth/JwtTokenService.cs))
- **Access Tokens:** Signed with HMAC-SHA256 (`HmacSha256Signature`). Contains:
  - `ClaimTypes.NameIdentifier`: `UserId`
  - `ClaimTypes.Name`: `Username`
  - `ClaimTypes.Email`: `Email`
  - `displayName`: Cute display nickname
  - `avatarSticker`: Selected avatar sticker
  - `roleKey`: System role key
  - `ClaimTypes.Role`: All roles assigned to user in `UserRoles` table
  - `permission`: Granular permission capability codes from `RolePermissions` table
  - `jti`: Unique token GUID
- **Refresh Tokens:**
  - Generated as 64 random bytes (URL-safe Base64).
  - Only the **SHA-256 hash** of the refresh token is stored in the database.
  - **Rotation:** Upon refresh, the previous token is marked revoked (`RevokedAt = DateTime.UtcNow`) and linked to its replacement (`ReplacedByTokenHash`). A brand new token pair is issued.
  - **Breach / Reuse Detection:** If an already-revoked refresh token is submitted, the system flags potential token theft and immediately revokes all active tokens for that user account.

---

## 3. Database Changes

### A. `RefreshTokens` Table
Added to SQL Server schema in [`init.sql`](file:///d:/For%20My%20Besties/BestieSpace.Api/Infrastructure/Data/init.sql) and mapped in [`BestieDbContext.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Infrastructure/Data/BestieDbContext.cs):

| Column | Type | Constraints / Description |
| :--- | :--- | :--- |
| `RefreshTokenId` | `INT` | Primary Key, Identity(1,1) |
| `UserId` | `INT` | Foreign Key -> `Users(UserId)`, On Delete Cascade |
| `TokenHash` | `NVARCHAR(256)` | Required, Non-clustered Index |
| `ExpiresAt` | `DATETIME2` | Required, Expiration timestamp |
| `CreatedAt` | `DATETIME2` | Default: `SYSUTCDATETIME()` |
| `RevokedAt` | `DATETIME2` | Nullable, revocation timestamp |
| `ReplacedByTokenHash` | `NVARCHAR(256)` | Nullable, hash of replacement token |

### B. User Role Seeding
When users register or when the database boots, any user without a role in `UserRoles` is automatically linked to the default `User` role.

---

## 4. Blazor Client Authentication Architecture

Blazor Server executes components on the server inside a SignalR circuit. Rather than exposing raw JWT or refresh tokens to client JavaScript or unencrypted `localStorage`, BestieSpace implements a layered approach:

1. **Circuit-Scoped Authentication:** [`BestieAuthenticationStateProvider.cs`](file:///d:/For%20My%20Besties/BestieSpace.Client/Services/BestieAuthenticationStateProvider.cs) maintains the authenticated `ClaimsPrincipal` in server memory for the duration of the user's circuit.
2. **Encrypted Session Persistence:** During interactive sessions, state is encrypted via ASP.NET Core `ProtectedSessionStorage`. Raw JWTs are never accessible to client scripts.
3. **Automatic Bearer Injection:** [`BestieApiClient.cs`](file:///d:/For%20My%20Besties/BestieSpace.Client/Services/BestieApiClient.cs) automatically attaches the bearer token (`Authorization: Bearer <token>`) to outgoing HTTP requests.
4. **Router Protection:** [`Routes.razor`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Routes.razor) uses `<AuthorizeRouteView>` with `<RedirectToLogin />` to ensure unauthenticated users are guided to log in.

---

## 5. Configuration & Environment Variables

JWT settings can be configured via `appsettings.json`, `appsettings.Development.json`, or environment variables:

| Setting | Default Value | Environment Variable Override |
| :--- | :--- | :--- |
| `Jwt:Key` | `BestieSpaceSuperSecretKawaiiKeyForJwtTokenAuth2026_MustBeAtLeast32BytesLong!` | `JWT_SECRET_KEY` |
| `Jwt:Issuer` | `BestieSpace` | `JWT_ISSUER` |
| `Jwt:Audience` | `BestieSpaceClient` | `JWT_AUDIENCE` |
| `Jwt:AccessTokenExpirationMinutes` | `60` | `JWT_ACCESS_MINUTES` |
| `Jwt:RefreshTokenExpirationDays` | `7` | `JWT_REFRESH_DAYS` |

---

## 6. Verification & How to Run

### Step 1: Run the Cryptographic Authentication Verification Suite
To verify password hashing, constant-time checks, JWT claim generation, token validation, and refresh token hashing without requiring an active database connection:
```powershell
dotnet run --project BestieSpace.Api/BestieSpace.Api.csproj -- --test-auth
```
**Expected Output:**
```text
================ BESTIESPACE AUTH CRYPTO VERIFICATION ================
[PASS] PasswordHasher PBKDF2 verified (Hash: PBKDF2-SHA256:100000:...)
[PASS] JWT Access Token generation & claim validation verified (Token length: 580)
[PASS] Refresh Token generation and SHA-256 hash verified
================ ALL AUTH CRYPTO TESTS PASSED! 🌸 ================
```

### Step 2: Build the Solution
```powershell
dotnet build BestieSpace.sln -m:1
```
*Expected: 0 Warnings, 0 Errors.*

### Step 3: Run the API Server
```powershell
dotnet run --project BestieSpace.Api/BestieSpace.Api.csproj
```

### Step 4: Run the Blazor Client
In another terminal:
```powershell
dotnet run --project BestieSpace.Client/BestieSpace.Client.csproj
```
Navigate to:
- `http://localhost:5100/login` – Login with Nickname/Email or Strawberry PIN.
- `http://localhost:5100/register` – Register a new Bestie account.
- `http://localhost:5100/` – Main clubhouse dashboard (protected).

---

## 7. Next Steps for Phase 3
Phase 2 establishes the complete security and user foundation. Phase 3 will introduce:
- Friendship requests, friend search, accept/reject workflows.
- User blocking endpoints & UI.
- Bestie Circle creation and invite management.
- Service-layer transactional enforcement of the **9-member maximum per circle**.
