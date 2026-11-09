# BestieSpace – Phase 3: Dynamic Role-Based Access Control (RBAC) 👑

Welcome to the **Phase 3** upgrade documentation for **BestieSpace**. This document explains all architectural, security, database, and client changes implemented to deliver a **complete, fully database-driven Dynamic Role-Based Access Control (RBAC) and Permission Management System** while preserving BestieSpace's signature cozy Kawaii experience.

---

## 1. Summary of Changes

| Feature | Phase 2 (Authentication) | Upgraded Phase 3 (Dynamic Database-Driven RBAC) |
| :--- | :--- | :--- |
| **RBAC Architecture** | Schema only, static claims | **100% Dynamic, Database-Driven RBAC**. Roles, permissions, and assignments are resolved live from SQL Server |
| **Role-Permission Mappings** | Hardcoded/unmanaged | Dynamically defined in `dbo.RolePermissions` table with zero hardcoded code mappings in controllers or UI |
| **System Roles** | Unprotected | Protected system roles (`SuperAdmin`, `Admin`, `User`, `Bestie`) with `IsSystemRole = true` to prevent deletion or renaming |
| **Permission Catalog** | Minimal/unmapped | Comprehensive catalog of **27 standardized uppercase permissions** covering Users, Roles, Circles, Chat, Memories, Notes, and System Metrics |
| **Dynamic Policy Provider** | Static policies | Custom [`PermissionPolicyProvider.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Rbac/PermissionPolicyProvider.cs) synthesizing policies on the fly for any `[Authorize(Policy = "...")]` attribute |
| **Authorization Handler** | Default handler | Custom [`PermissionAuthorizationHandler.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Rbac/PermissionAuthorizationHandler.cs) evaluating live user permissions against current database or high-speed cache |
| **Cache Invalidation** | None | Atomic generational versioning (`Volatile.Read` / `Interlocked.Increment`). Modifying permissions or roles instantly invalidates all cached permissions across the entire system |
| **SuperAdmin Safeguards** | None | Impossible to delete the last active `SuperAdmin` account; SuperAdmin role automatically possesses all permissions in catalog |
| **Admin Console UI** | None | Full-featured 4-tab Kawaii administration UI at [`/admin/rbac`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Pages/AdminRbac.razor) for roles, role-permission matrix, user assignments, and catalog |
| **Navigation & Shortcuts** | None | Cute 👑 **Admin Console** shortcut pill on [`Home.razor`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Pages/Home.razor) and sidebar link in [`NavMenu.razor`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Layout/NavMenu.razor) |

---

## 2. Dynamic RBAC Architecture

### A. Database-Driven Model (No Hardcoded Mappings)
All access control is governed through the relational schema:
- `Roles`: `RoleId`, `RoleName`, `Description`, `IsSystemRole`
- `Permissions`: `PermissionId`, `PermissionCode`, `Description`
- `RolePermissions`: `RolePermissionId`, `RoleId`, `PermissionId`
- `UserRoles`: `UserRoleId`, `UserId`, `RoleId`

Administrators can create custom roles, assign or revoke permissions, and assign roles to users directly at runtime. When changes are made, they take effect immediately without redeploying the application or forcing users to re-login.

### B. High-Performance Atomic Cache Invalidation
Evaluating database permissions on every single HTTP request would introduce unnecessary database latency. To solve this with maximum performance and zero staleness:
1. Permissions for user `userId` are cached in `IMemoryCache` with a generational key:
   `rbac_perms_v{globalVersion}_user_{userId}`
2. Whenever any role's permissions or user roles are updated:
   - `Interlocked.Increment(ref _globalVersion);` is called.
3. This instantaneously and atomically invalidates all cached permission entries across all threads and users in a single CPU cycle, ensuring zero stale permission state.

### C. System Safety Rules
- **Protected System Roles**: `SuperAdmin`, `Admin`, and `User` have `IsSystemRole = true`. Any attempt to rename or delete them returns an immediate validation error.
- **Last SuperAdmin Guard**: Removing the `SuperAdmin` role from an account is prohibited if it would leave the system with zero SuperAdmins.
- **Self-Role Guard**: Users cannot modify their own roles to prevent privilege escalation or accidental lockout.
- **SuperAdmin Hierarchy**: Only an existing SuperAdmin is allowed to grant the `SuperAdmin` role to another account.

---

## 3. Standardized Permission Catalog (27 Permissions)

| Category | Permission Code | Description |
| :--- | :--- | :--- |
| **User Management** | `USER_VIEW` | View user profiles and user lists |
| | `USER_CREATE` | Create new user accounts manually |
| | `USER_UPDATE` | Update user profile details and status |
| | `USER_DELETE` | Delete or deactivate user accounts |
| | `USER_MANAGE` | Full administrative user lifecycle management |
| **Roles & Permissions** | `ROLE_VIEW` | View roles, permissions, and descriptions |
| | `ROLE_CREATE` | Create new custom security roles |
| | `ROLE_UPDATE` | Update existing roles (names and descriptions) |
| | `ROLE_DELETE` | Delete custom roles when no members are assigned |
| | `PERMISSION_VIEW` | View the global permission catalog |
| | `PERMISSION_ASSIGN`| Assign or revoke permissions to/from roles |
| | `USER_ROLE_MANAGE` | Assign and remove roles from user accounts |
| **Friendships** | `FRIEND_REQUEST_SEND` | Send friend connection requests |
| | `FRIEND_REQUEST_ACCEPT` | Accept or reject incoming friend requests |
| | `FRIEND_REMOVE` | Remove an existing friendship connection |
| **Bestie Circles** | `CIRCLE_CREATE` | Create new private Bestie Circles |
| | `CIRCLE_VIEW` | View circles and their member lists |
| | `CIRCLE_UPDATE` | Edit circle details and settings |
| | `CIRCLE_DELETE` | Delete a circle |
| | `CIRCLE_MEMBER_MANAGE`| Invite, accept, and manage circle members |
| **Clubhouse Chat** | `CHAT_SEND` | Send chat messages, stickers, and multimedia |
| | `CHAT_READ` | Read chat messages and channel history |
| **Memories Gallery** | `MEMORY_CREATE` | Upload new polaroid memories |
| | `MEMORY_UPDATE` | Edit memory captions and sticker decorations |
| | `MEMORY_DELETE` | Delete memories from the gallery |
| **Secret Notes** | `SECRET_NOTE_MANAGE` | Create, edit, and manage mystery envelope notes |
| **System & Metrics** | `SYSTEM_INFO_VIEW` | View system health, metrics, and application stats |

---

## 4. Backend Implementation

### A. Core Services
- [`IRoleService.cs` / `RoleService.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Rbac/RoleService.cs): Complete lifecycle management of roles and role permissions with validation, system role guards, and cache invalidation.
- [`IPermissionService.cs` / `PermissionService.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Rbac/PermissionService.cs): Catalog retrieval, role permission lookup, and automatic category categorization.
- [`IUserAuthorizationService.cs` / `UserAuthorizationService.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Rbac/UserAuthorizationService.cs): Dynamic permission evaluation (`HasPermissionAsync`), user role assignments, SuperAdmin hierarchy enforcement, and generational cache invalidation.

### B. Dynamic Policy Authorization
- [`PermissionRequirement.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Rbac/PermissionRequirement.cs): Implements `IAuthorizationRequirement` with `Permission` code.
- [`PermissionAuthorizationHandler.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Rbac/PermissionAuthorizationHandler.cs): Extracts the authenticated user's ID from claims and verifies permission dynamically against the live database / cache via `_userAuthService.HasPermissionAsync`.
- [`PermissionPolicyProvider.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Rbac/PermissionPolicyProvider.cs): Dynamic policy provider that automatically synthesizes authorization policies for any permission code in `[Authorize(Policy = "...")]`.

### C. REST API Endpoints ([`RbacController.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Rbac/RbacController.cs))
- `GET /api/rbac/my-permissions` – Get current user's active permissions.
- `GET /api/rbac/roles` (`[Authorize(Policy = "ROLE_VIEW")]`) – List all roles with permission and user counts.
- `GET /api/rbac/roles/{id}` (`[Authorize(Policy = "ROLE_VIEW")]`) – Get detailed role information.
- `POST /api/rbac/roles` (`[Authorize(Policy = "ROLE_CREATE")]`) – Create custom role.
- `PUT /api/rbac/roles/{id}` (`[Authorize(Policy = "ROLE_UPDATE")]`) – Update role name/description.
- `DELETE /api/rbac/roles/{id}` (`[Authorize(Policy = "ROLE_DELETE")]`) – Delete custom role.
- `PUT /api/rbac/roles/{id}/permissions` (`[Authorize(Policy = "PERMISSION_ASSIGN")]`) – Update role permissions.
- `GET /api/rbac/permissions` (`[Authorize(Policy = "PERMISSION_VIEW")]`) – Get permission catalog.
- `GET /api/rbac/roles/{id}/permissions` (`[Authorize(Policy = "PERMISSION_VIEW")]`) – Get permissions for a role.
- `GET /api/rbac/users` (`[Authorize(Policy = "USER_VIEW")]`) – Get all users with assigned roles.
- `GET /api/rbac/users/{userId}/roles` (`[Authorize(Policy = "USER_VIEW")]`) – Get a user's assigned roles.
- `POST /api/rbac/users/{userId}/roles` (`[Authorize(Policy = "USER_ROLE_MANAGE")]`) – Assign role to user.
- `DELETE /api/rbac/users/{userId}/roles/{roleId}` (`[Authorize(Policy = "USER_ROLE_MANAGE")]`) – Remove role from user.

---

## 5. Blazor Administration Console ([`AdminRbac.razor`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Pages/AdminRbac.razor))

Available at route `/admin/rbac` for administrators with 4 interactive tabs:
1. 👑 **Roles Management**: View all roles, active user counts, descriptions, and system badges. Create new custom roles, edit details, or safely delete unused roles.
2. 🧩 **Role Permissions Matrix**: Select any role to view permissions grouped by functional categories. Toggle checkboxes and click "Save Role Permissions" to apply changes in real time.
3. 👥 **User Role Assignments**: Search clubhouse members, view assigned roles as cute pastel badges, add new roles via modal, and remove roles (with safeguards protecting the last SuperAdmin).
4. 📜 **Permission Catalog**: Search and browse the entire 27-permission catalog with codes, categories, and descriptions.

---

## 6. Verification & Automated Test Suite

Run the built-in cryptographic and RBAC verification test suite:

```powershell
dotnet run --project BestieSpace.Api/BestieSpace.Api.csproj -- --test-auth
```

**Verification Results:**
```
================ BESTIESPACE AUTH CRYPTO VERIFICATION ================
[PASS] PasswordHasher PBKDF2 verified (Hash: PBKDF2-SHA256:100000:L302NT5VeiT...)
[PASS] JWT Access Token generation & claim validation verified (Token length: 580)
[PASS] Refresh Token generation and SHA-256 hash verified (Hash: 8193f4e61b85aa213f5a4a62190f67aee5eeb7207c148bd098b74573a8359291)
[PASS] Dynamic Permission Policy Provider verified: Policy 'USER_MANAGE' generated with PermissionRequirement('USER_MANAGE')
================ ALL AUTH & RBAC CRYPTO TESTS PASSED! 🌸 ================
```
