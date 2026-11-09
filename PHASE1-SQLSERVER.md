# BestieSpace – Phase 1: SQL Server Database Upgrade & Foundation 🌸

Welcome to the **Phase 1** upgrade documentation for **BestieSpace**. This document explains all architectural and schema changes made while migrating the database engine from SQLite to **Microsoft SQL Server**, detailing the new database structure, data migration process, configuration, and verification steps.

---

## 1. Summary of Changes

| Component | Before (Phase 0) | Upgraded (Phase 1) |
| :--- | :--- | :--- |
| **Database Engine** | SQLite (`bestiespace.db`) | Microsoft SQL Server (LocalDB / Express / Azure SQL / Container) |
| **EF Core Provider** | `Microsoft.EntityFrameworkCore.Sqlite` | `Microsoft.EntityFrameworkCore.SqlServer` (v10.0.10) |
| **Data Migration** | None | Safe, non-destructive `SqliteToSqlServerDataMigrator` preserving IDs & relationships |
| **Total Tables** | 5 tables | **15 relational tables** supporting friendships, circles, RBAC, and chat receipts |
| **User Identification** | `Id` | Primary Key: `UserId` (with backwards-compatible `Id` property alias) |
| **Friendship Handling** | None | Canonical pairs (`UserA`, `UserB`) preventing duplicates in any direction & self-friending |
| **Circles (Clubs)** | None | `BestieCircles` & `BestieCircleMembers` (prepared for max 9 members) |
| **Access Control** | Hardcoded role keys | Full Dynamic RBAC (`Roles`, `Permissions`, `UserRoles`, `RolePermissions`) |
| **Chat System** | 1-on-1 private clubhouse | Upgraded for Circles (`CircleId`, `SenderId`) + `ChatMessageReads` receipts |
| **Preserved Kawaii UI** | Dashboard, Memories, Notes, Chat | 100% preserved and fully functional |

---

## 2. Upgraded Database Schema (15 Tables)

### A. Core Platform & Social Tables

#### 1. `Users`
- **Primary Key:** `UserId` (`INT`, Identity)
- **Compatibility Alias:** `Id` mapped to `UserId` in C# (ignored by EF Core)
- **Columns:**
  - `UserId` (INT, PK, Identity)
  - `Username` (NVARCHAR(50), Unique)
  - `NormalizedUsername` (NVARCHAR(50), Unique)
  - `Email` (NVARCHAR(255), Nullable)
  - `NormalizedEmail` (NVARCHAR(255), Nullable)
  - `PasswordHash` (NVARCHAR(255), Nullable)
  - `CreatedAt` (DATETIME2)
  - `UpdatedAt` (DATETIME2, Nullable)
  - `IsActive` (BIT, Default: 1)
  - *Preserved Kawaii Auth:* `Password`, `DisplayName`, `RoleKey`, `AvatarSticker`, `PasswordHint`, `QuickPin`, `IsOnline`, `LastSeenAt`, `LastLoginAt`

#### 2. `Friendships`
- **Primary Key:** `FriendshipId` (`INT`, Identity)
- **Columns:**
  - `UserId` (INT, FK -> Users.UserId)
  - `FriendUserId` (INT, FK -> Users.UserId)
  - `Status` (NVARCHAR(50), Default: `'Pending'`)
  - `CreatedAt` (DATETIME2)
  - `UpdatedAt` (DATETIME2, Nullable)
  - `UserA` (INT, Computed Persisted column: `LEAST(UserId, FriendUserId)`)
  - `UserB` (INT, Computed Persisted column: `GREATEST(UserId, FriendUserId)`)
- **Constraints & Indexes:**
  - `CK_Friendships_NoSelfFriendship`: `[UserId] <> [FriendUserId]`
  - `CK_Friendships_ValidStatus`: `[Status] IN ('Pending', 'Accepted', 'Rejected')`
  - `IX_Friendships_CanonicalPair`: Unique constraint on `(UserA, UserB)` to guarantee friendship requests cannot be duplicated in reverse order.
  - Foreign keys configured with `DeleteBehavior.Restrict` to prevent SQL Server multiple cascade delete cycles.

#### 3. `UserBlocks`
- **Primary Key:** `UserBlockId` (`INT`, Identity)
- **Columns:**
  - `BlockerUserId` (INT, FK -> Users.UserId)
  - `BlockedUserId` (INT, FK -> Users.UserId)
  - `CreatedAt` (DATETIME2)
- **Constraints & Indexes:**
  - `CK_UserBlocks_NoSelfBlock`: `[BlockerUserId] <> [BlockedUserId]`
  - Unique index on `(BlockerUserId, BlockedUserId)`
  - Foreign keys configured with `DeleteBehavior.Restrict`

---

### B. Bestie Circles (Max 9 Members)

#### 4. `BestieCircles`
- **Primary Key:** `CircleId` (`INT`, Identity)
- **Columns:**
  - `CircleName` (NVARCHAR(100), Required)
  - `Description` (NVARCHAR(500), Nullable)
  - `OwnerUserId` (INT, FK -> Users.UserId)
  - `CreatedAt` (DATETIME2)
  - `UpdatedAt` (DATETIME2, Nullable)
  - `IsActive` (BIT, Default: 1)
- **Foreign Keys:** `OwnerUserId` references `Users(UserId)` (`DeleteBehavior.Restrict`).

#### 5. `BestieCircleMembers`
- **Primary Key:** `CircleMemberId` (`INT`, Identity)
- **Columns:**
  - `CircleId` (INT, FK -> BestieCircles.CircleId, Cascade)
  - `UserId` (INT, FK -> Users.UserId, Restrict)
  - `RoleId` (INT, FK -> Roles.RoleId, Restrict)
  - `JoinedAt` (DATETIME2)
  - `IsActive` (BIT, Default: 1)
- **Constraints & Indexes:**
  - Unique index on `(CircleId, UserId)` to prevent a user from joining the same circle twice.
  - Filtered indexes on `(CircleId, IsActive)` prepared for service-layer transactional enforcement of the **9-member maximum** (including circle owner).

#### 6. `CircleInvitations`
- **Primary Key:** `InvitationId` (`INT`, Identity)
- **Columns:**
  - `CircleId` (INT, FK -> BestieCircles.CircleId, Cascade)
  - `InviterUserId` (INT, FK -> Users.UserId, Restrict)
  - `InviteeUserId` (INT, FK -> Users.UserId, Restrict)
  - `Status` (NVARCHAR(50), Default: `'Pending'`)
  - `CreatedAt` (DATETIME2)
  - `RespondedAt` (DATETIME2, Nullable)
- **Constraints & Indexes:**
  - `CK_CircleInvitations_NoSelfInvite`: `[InviterUserId] <> [InviteeUserId]`
  - `CK_CircleInvitations_ValidStatus`: `[Status] IN ('Pending', 'Accepted', 'Declined', 'Cancelled')`
  - Index on `(CircleId, InviteeUserId, Status)`

---

### C. Role-Based Access Control (RBAC)

#### 7. `Roles`
- **Primary Key:** `RoleId` (`INT`, Identity)
- **Columns:** `RoleName` (NVARCHAR(50), Unique), `Description` (NVARCHAR(255))
- **Pre-seeded System Roles:**
  - `Owner`: Circle creator with full administrative rights.
  - `Admin`: Circle manager capable of handling invites and members.
  - `Member`: Standard circle member with chat and memory posting rights.
  - `Bestie`: VIP Bestie with private clubhouse access.
  - `User`: Standard platform registered user.

#### 8. `Permissions`
- **Primary Key:** `PermissionId` (`INT`, Identity)
- **Columns:** `PermissionCode` (NVARCHAR(100), Unique), `Description` (NVARCHAR(255))
- **Pre-seeded System Permissions:**
  - `circle:view`, `circle:edit`, `circle:delete`, `circle:invite`, `circle:remove_member`, `circle:chat`, `circle:share_memory`.

#### 9. `UserRoles`
- **Primary Key:** `UserRoleId` (`INT`, Identity)
- **Columns:** `UserId` (FK -> Users.UserId, Cascade), `RoleId` (FK -> Roles.RoleId, Cascade)
- **Indexes:** Unique index on `(UserId, RoleId)`

#### 10. `RolePermissions`
- **Primary Key:** `RolePermissionId` (`INT`, Identity)
- **Columns:** `RoleId` (FK -> Roles.RoleId, Cascade), `PermissionId` (FK -> Permissions.PermissionId, Cascade)
- **Indexes:** Unique index on `(RoleId, PermissionId)`

---

### D. Upgraded Messaging & Read Receipts

#### 11. `ChatMessages`
- **Primary Key:** `Id` (`INT`, Identity)
- **Columns:**
  - `Id` (INT, PK, Identity)
  - `CircleId` (INT, Nullable, FK -> BestieCircles.CircleId, OnDelete SetNull)
  - `SenderId` (INT, Nullable, FK -> Users.UserId, OnDelete SetNull)
  - `SenderName` (NVARCHAR(100), Required)
  - `MessageType` (NVARCHAR(20), e.g., 'text', 'photo', 'voice', 'sticker')
  - `Content` (NVARCHAR(MAX), Nullable)
  - `MediaUrl` (NVARCHAR(1000), Nullable)
  - `VoiceDurationSeconds` (FLOAT, Nullable)
  - `Timestamp` (DATETIME2)
  - `IsRead` (BIT, Default: 0)
  - `ReadAt` (DATETIME2, Nullable)
- **Preservation:** All existing 1-on-1 private clubhouse messages are preserved (with `CircleId = NULL` and `SenderId = NULL`).

#### 12. `ChatMessageReads`
- **Primary Key:** `ChatMessageReadId` (`INT`, Identity)
- **Columns:** `ChatMessageId` (FK -> ChatMessages.Id, Cascade), `UserId` (FK -> Users.UserId, Restrict), `ReadAt` (DATETIME2)
- **Indexes:** Unique index on `(ChatMessageId, UserId)` to prevent duplicate read receipts.

---

### E. Preserved Kawaii Feature Tables

#### 13. `AppStats`
- Global Miss-You meter count and heartfelt welcome letter.

#### 14. `Memories`
- Polaroid photo memory wall, sticker rotation angles, and memory dates.

#### 15. `SecretNotes`
- Pastel mystery envelope notes with hints and unlock status.

---

## 3. SQL Server Connection Configuration

The application retrieves the SQL Server connection string with fallback priority:
1. `ConnectionStrings:DefaultConnection` in `appsettings.json` or `appsettings.Development.json`.
2. Environment variable: `SQL_SERVER_CONNECTION_STRING` or `ConnectionStrings__DefaultConnection`.
3. Default fallback: LocalDB (`Server=(localdb)\\mssqllocaldb;Database=BestieSpaceDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;`).

### Connection String Examples

#### LocalDB (Default on Windows Dev machines)
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BestieSpaceDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
```

#### SQL Server Express
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=BestieSpaceDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
```

#### SQL Server in Docker / Linux Container
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=BestieSpaceDb;User Id=sa;Password=YourStrongPassword123!;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
```

#### Azure SQL Database
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=tcp:your-server.database.windows.net,1433;Initial Catalog=BestieSpaceDb;Persist Security Info=False;User ID=your-user;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
}
```

---

## 4. How the Database Initializes

Initialization is managed automatically by `DatabaseBootstrapper.InitializeAsync` when the API starts:

1. **Directories & Assets:** Ensures `wwwroot/uploads/` and SVG sample polaroids exist.
2. **Database & Schema Creation:**
   - Calls `context.Database.EnsureCreatedAsync()` to establish the database and EF Core mappings.
   - Idempotently executes `init.sql` batch-by-batch (splitting on `GO` tokens) to ensure all check constraints, computed columns, and indices are enforced.
3. **One-Time Data Import:**
   - Resolves `SqliteToSqlServerDataMigrator`.
   - Locates `bestiespace.db` in `DATA_DIR` or the application root directory.
   - Executes non-destructive data migration if SQL Server tables are empty.
4. **Seed Fallback:**
   - Seeds system roles (`Owner`, `Admin`, `Member`, `Bestie`, `User`) and permissions (`circle:*`).
   - If the database is completely new, seeds the default Kawaii accounts (`strawberry_bestie`, `mochi_maker`) and sample notes/memories.

---

## 5. How SQLite Data Migration Works

The migration is handled by `SqliteToSqlServerDataMigrator.cs`:

- **Strict Read-Only Access:** Opens `bestiespace.db` with `SqliteOpenMode.ReadOnly`. The original SQLite database is never deleted or altered.
- **Identity Preservation:** Executes `SET IDENTITY_INSERT [Table] ON` to keep existing IDs for `Users`, `Memories`, `SecretNotes`, `AppStats`, and `ChatMessages`.
- **Duplicate Prevention:** Evaluates `AnyAsync()` per table. If SQL Server already has records for a table, that table's import is cleanly skipped.
- **Data Transformation:** Converts SQLite text date timestamps safely into SQL Server `DATETIME2`, generates normalized usernames (`STRAWBERRY_BESTIE`), and constructs default email addresses.

---

## 6. How to Run the Project

### Prerequisites
- .NET 10 SDK
- SQL Server (LocalDB, SQL Express, or Docker container)

### Step 1: Restore Dependencies
```powershell
dotnet restore
```

### Step 2: Build the Solution
```powershell
dotnet build BestieSpace.sln -m:1
```
*Expected: 0 Warnings, 0 Errors.*

### Step 3: Run the Backend API
```powershell
dotnet run --project BestieSpace.Api/BestieSpace.Api.csproj
```
The API will launch, connect to SQL Server, execute `init.sql`, import existing records from `bestiespace.db`, and begin listening on `http://localhost:5000` (or configured port).

### Step 4: Run the Blazor Client
In a separate terminal:
```powershell
dotnet run --project BestieSpace.Client/BestieSpace.Client.csproj
```
Access the application in your browser at `https://localhost:7001` or `http://localhost:5100`.

---

## 7. Limitations & Roadmap for Phase 2

### Intentionally Deferred to Phase 2:
- User Registration & Password Hashing (BCrypt / Argon2).
- JWT Token Authentication & Bearer Authorization middleware.
- Friend request sending, accepting, and blocking endpoints & UI.
- Circle creation, invitations, and membership management endpoints & UI.
- Transactional enforcement of the **max 9 members per circle** rule in the application service layer.
- SignalR group hub for real-time circle chat and read receipt dispatching.
