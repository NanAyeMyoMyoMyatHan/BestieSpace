# BestieSpace – Phase 4: User Profiles, Social Discovery & Friendship System 🌸👭

Welcome to the **Phase 4** upgrade documentation for **BestieSpace**. This document explains all architectural, database, security, and client changes implemented to deliver a **complete multi-user profile, user discovery, friendship system, and blocking engine** while preserving BestieSpace's signature cozy Kawaii experience.

---

## 1. Summary of Changes

| Feature | Phase 3 (RBAC) | Upgraded Phase 4 (Social & Friendship System) |
| :--- | :--- | :--- |
| **User Profiles** | Basic display name & sticker | Full profile with **ProfilePicture**, **Bio**, **DisplayName**, **FriendCount**, and online presence |
| **Profile Image Uploads** | None | Secure image upload pipeline to `wwwroot/uploads/profile-pictures/` with validation (PNG, JPG, WEBP, GIF, max 5 MB) |
| **User Discovery** | None | Paged search by username and display name, suggested users, debounced typing, and online badges |
| **Friendship Lifecycle** | Schema only | Full lifecycle: Send, Accept, Reject, Cancel, and Remove friends via [`FriendshipService.cs`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Social/FriendshipService.cs) |
| **Duplicate Prevention** | Schema indexes | Guaranteed database canonical pairing (`UserA = MIN(UserId, FriendUserId)`, `UserB = MAX(UserId, FriendUserId)`) with unique index preventing duplicate entries regardless of direction |
| **Mutual Requests** | Unhandled | Auto-accepts mutual requests seamlessly (if User B sends to User A while User A has a pending request to User B) |
| **Blocking System** | Schema only | Full blocking engine: prevents requests, cleans up active friendships upon block, hides blocked accounts from discovery |
| **IDOR Protection** | Role-level only | Strict server-side ownership checks: only recipient can accept/reject, only sender can cancel, only friend can remove |
| **Blazor Social UI** | None | Consolidated Kawaii **Bestie Social Hub** at [`/friends`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Pages/FriendsHub.razor) with 6 interactive tabs and confirmation dialogs |

---

## 2. Business Logic & Safety Rules

### A. Canonical Friendship Pairing & Duplicate Elimination
To prevent race conditions and duplicate friendships (e.g. Alice friending Bob while Bob friends Alice):
1. The SQL Server `Friendships` table computes:
   - `UserA = CASE WHEN [UserId] < [FriendUserId] THEN [UserId] ELSE [FriendUserId] END`
   - `UserB = CASE WHEN [UserId] < [FriendUserId] THEN [FriendUserId] ELSE [UserId] END`
2. A unique index `IX_Friendships_UserA_UserB` enforces that at most **one** record can exist between any two users at any given time.
3. When querying or inserting:
   - `UserId` records the user who initiated the request.
   - `FriendUserId` records the recipient of the request.
   - `Status` tracks the state (`Pending`, `Accepted`, `Rejected`).

### B. Request Lifecycle & Rules
- **No Self-Friendship**: Database check constraint `[UserId] <> [FriendUserId]` and server validation prevent self-friend requests.
- **Mutual Auto-Acceptance**: If User A sends a request to User B while User B already sent a pending request to User A, the system automatically accepts the request, creating an instant friendship without error.
- **Decline/Reject**: The recipient can reject a pending request (`Status = "Rejected"`). The requester can re-request later if appropriate.
- **Cancellation**: Only the sender can cancel their own pending request.
- **Removal (Unfriend)**: Either user in an accepted friendship can remove the friendship, dissolving the link cleanly.

### C. Blocking Architecture
- **Self-Block Check**: A user cannot block themselves.
- **Relationship Dissolution**: When User A blocks User B, any existing friendship (active or pending) between them is immediately dissolved.
- **Discovery Privacy**: Blocked users and blockers are completely excluded from user discovery, search results, and suggested users.
- **Request Barrier**: Neither user can send friend requests to each other while a block is in place.

---

## 3. Standardized RBAC Permissions (Phase 4 Additions)

| Domain | Permission Code | Description |
| :--- | :--- | :--- |
| **Profiles** | `PROFILE_VIEW` | View own and other users' public profiles |
| | `PROFILE_UPDATE` | Edit own profile details, bio, sticker, and picture |
| **Discovery** | `USER_SEARCH` | Search and discover clubhouse besties |
| **Friendships** | `FRIEND_REQUEST_SEND` | Send friend requests |
| | `FRIEND_REQUEST_ACCEPT` | Accept incoming friend requests |
| | `FRIEND_REQUEST_REJECT` | Decline incoming friend requests |
| | `FRIEND_REQUEST_CANCEL` | Cancel outgoing pending friend requests |
| | `FRIEND_REMOVE` | Remove an existing friendship |
| **Blocks** | `USER_BLOCK` | Block unwanted users from interactions |
| | `USER_UNBLOCK` | Unblock a previously blocked user |
| | `BLOCKED_USERS_VIEW` | View the personal blocked accounts list |

---

## 4. Backend Architecture

### Services ([`BestieSpace.Api/Features/Social/`](file:///d:/For%20My%20Besties/BestieSpace.Api/Features/Social/))
- **`IUserProfileService` / `UserProfileService`**: Profile retrieval, profile editing, and secure image upload handling.
- **`IFriendshipService` / `FriendshipService`**: Complete friendship lifecycle with canonical lookup and IDOR ownership verification.
- **`IUserBlockService` / `UserBlockService`**: Blocking, unblocking, block listing, and relationship cleanup.
- **`IUserDiscoveryService` / `UserDiscoveryService`**: Debounced search, server-side pagination, block filtering, and suggested besties.

### Controllers
- **`UserProfileController`** (`/api/userprofile`): View own profile, view user by ID/username, update profile, upload picture.
- **`FriendshipController`** (`/api/friendship`): Send request, accept, reject, cancel, remove, incoming list, outgoing list, friends list, status check.
- **`UserBlockController`** (`/api/userblock`): Block user, unblock user, list blocked users.
- **`UserDiscoveryController`** (`/api/userdiscovery`): Paged search, suggested besties.

---

## 5. Blazor Client & Bestie Social Hub ([`FriendsHub.razor`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Pages/FriendsHub.razor))

Accessible at routes `/friends` and `/profile`, featuring 6 coordinated tabs:
1. 🌸 **My Profile**: Display photo or avatar sticker, nickname, bio, joined date, friend count, and modal to edit details or upload picture.
2. 🔍 **Discover Besties**: Debounced search input, suggested besties carousel, profile cards with real-time friendship badges (`💖 Bestie`, `💌 Pending`, `➕ Add Friend`), and server-side pagination.
3. 👭 **My Friends**: Searchable grid of active besties, chat shortcuts, and unfriend button with confirmation.
4. 💌 **Incoming Requests**: Cards displaying sender details, date, and Accept / Decline buttons.
5. 🚀 **Sent Requests**: Cards showing pending outgoing requests with Cancel Request option.
6. 🚫 **Blocked Users**: List of blocked accounts with one-click Unblock control.

### Navigation Integration
- Added 👭 **Friends & Profile** shortcut pill on [`Home.razor`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Pages/Home.razor).
- Added sidebar navigation link in [`NavMenu.razor`](file:///d:/For%20My%20Besties/BestieSpace.Client/Components/Layout/NavMenu.razor).

---

## 6. Automated Verification

Run the built-in test suite:
```powershell
dotnet run --project BestieSpace.Api/BestieSpace.Api.csproj -- --test-auth
```

**Verification Results:**
```
================ BESTIESPACE AUTH CRYPTO VERIFICATION ================
[PASS] PasswordHasher PBKDF2 verified (Hash: PBKDF2-SHA256:100000:D+IX+3G8BIh...)
[PASS] JWT Access Token generation & claim validation verified (Token length: 580)
[PASS] Refresh Token generation and SHA-256 hash verified (Hash: 27f3e33ce97f324e4032b496983f8d310cb36df2fe6bbc1acec290f2a9e080fb)
[PASS] Dynamic Permission Policy Provider verified: Policy 'USER_MANAGE' generated with PermissionRequirement('USER_MANAGE')
[PASS] Canonical Friendship Pairing symmetry verified (101 <-> 42 => [42, 101])
[PASS] Dynamic Phase 4 policies verified (FRIEND_REQUEST_SEND, USER_BLOCK, USER_SEARCH)
================ ALL AUTH, RBAC & PHASE 4 SOCIAL TESTS PASSED! 🌸 ================
```
