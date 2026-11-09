# BestieSpace – Phase 6: Real-Time Private Group Chat with SignalR 🌸💬✨

## 1. Executive Summary & Architecture Overview

Phase 6 upgrades **BestieSpace** into a real-time, multi-group friendship platform where every private **Bestie Circle** possesses its own strictly isolated, real-time group chat room powered by **ASP.NET Core SignalR**, **Entity Framework Core 10**, **SQL Server**, and **Blazor Server**.

### Key Architectural Tenets
1. **Strict Circle Chat Isolation:**
   - Members of *Circle A* communicate exclusively inside the SignalR group `circle_{CircleId}`.
   - Messages, events, presence, and history from *Circle A* are never accessible to non-members or other circles.
   - Non-members are forbidden from joining circle SignalR groups, reading message history, or sending messages.

2. **Persistence-Before-Broadcast Contract:**
   - Messages are validated, sender identities are verified from authenticated JWT server claims, and records are written to **SQL Server** with `SaveChangesAsync()` *before* any SignalR broadcasting occurs.
   - Unsaved or in-memory-only messages are never treated as successful.

3. **Multi-Connection Online Presence:**
   - Online status tracks individual browser tabs and devices per user.
   - Closing one browser tab does not mark a user offline if they have another active connection.
   - Real-time `UserOnline` and `UserOffline` events are broadcast seamlessly.

4. **Dynamic RBAC & Moderation:**
   - Permissions `CHAT_READ`, `CHAT_SEND`, `CHAT_EDIT_OWN`, `CHAT_DELETE_OWN`, `CHAT_MODERATE`, and `CHAT_VIEW_HISTORY` are database-driven and integrated with Phase 3 RBAC and Phase 5 Circle roles (`CircleOwner`, `CircleAdmin`, `CircleMember`).

5. **Kawaii Pastel Blazor Experience:**
   - Interactive, real-time chat room at `/circles/{CircleId}/chat` with pastel gradient speech bubbles, date dividers, inline editing, soft deletion, and automatic reconnection recovery.

---

## 2. Database Schema & Data Integrity

### Upgraded `ChatMessages` Table
The existing table was safely augmented with metadata for message editing and soft deletion while preserving 100% of legacy messages:

| Column | Type | Nullable | Description |
| :--- | :--- | :--- | :--- |
| `Id` (`ChatMessageId`) | `INT IDENTITY(1,1)` | No | Clustered Primary Key |
| `CircleId` | `INT` | Yes | Foreign Key referencing `BestieCircles.CircleId` |
| `SenderId` | `INT` | Yes | Foreign Key referencing `Users.UserId` |
| `SenderName` | `NVARCHAR(100)` | No | Cached display name at send time |
| `MessageType` | `NVARCHAR(20)` | No | `"text"`, `"system"`, `"sticker"`, `"photo"`, `"voice"` |
| `Content` | `NVARCHAR(MAX)` | Yes | Sanitized text content (max 2,000 characters) |
| `MediaUrl` | `NVARCHAR(1000)` | Yes | Optional media URL for future phases |
| `VoiceDurationSeconds`| `FLOAT` | Yes | Optional duration for audio notes |
| `Timestamp` (`CreatedAt`)| `DATETIME2` | No | UTC message creation time |
| `UpdatedAt` | `DATETIME2` | Yes | Set when a message is edited |
| `IsDeleted` | `BIT` | No | Soft deletion flag (Default `0`) |
| `DeletedAt` | `DATETIME2` | Yes | Timestamp of soft deletion |
| `IsRead` | `BIT` | No | Default `0` |
| `ReadAt` | `DATETIME2` | Yes | Optional timestamp |

### Indexes
- `IX_ChatMessages_Circle_Timestamp`: Fast chronological message history querying filtered by circle.
- `IX_ChatMessages_Sender_Timestamp`: Sender activity tracking.
- `IX_ChatMessages_Timestamp`: Global chronological sorting.

---

## 3. SignalR Real-Time Hub & Protocol Design

### Hub Endpoint
- Path: `/chathub`
- Authentication: JWT Bearer Access Token passed via query string `?access_token=...` during WebSocket handshake or standard Authorization header.

### Group Naming Convention
- Private circle groups are isolated with the naming scheme: `circle_{CircleId}` (e.g. `circle_1`, `circle_42`).

### Client-to-Server Methods
- `JoinCircle(int circleId)`: Validates authenticated user claims, verifies active membership in the target circle, and adds connection to group `circle_{CircleId}`.
- `LeaveCircle(int circleId)`: Removes connection from group `circle_{CircleId}`.
- `SendMessage(SendCircleMessageRequestDto request)`: Persists message to SQL Server and broadcasts to group.
- `EditMessage(EditCircleMessageRequestDto request)`: Validates ownership or moderation rights, updates message in SQL Server, and broadcasts `MessageUpdated`.
- `DeleteMessage(DeleteCircleMessageRequestDto request)`: Performs soft delete in SQL Server and broadcasts `MessageDeleted`.

### Server-to-Client Real-Time Events
- `ReceiveMessage(CircleChatMessageDto message)`: Dispatched to circle group upon message creation.
- `MessageUpdated(CircleChatMessageDto message)`: Dispatched when a message is edited.
- `MessageDeleted(CircleMessageDeletedEventDto event)`: Dispatched when a message is soft-deleted.
- `UserJoinedCircle(CircleMemberJoinedEventDto event)`: Dispatched when a member connects to the room.
- `UserLeftCircle(CircleMemberLeftEventDto event)`: Dispatched when a member departs the room.
- `UserOnline(CircleMemberPresenceEventDto event)`: Broadcast when a user transitions from offline to online.
- `UserOffline(CircleMemberPresenceEventDto event)`: Broadcast when a user's final connection closes.

---

## 4. Multi-Connection Presence Architecture (`ChatPresenceService`)

To prevent users from prematurely appearing offline when reloading or closing one browser tab:
- **Connection Registry**: A thread-safe `ConcurrentDictionary<int, ConcurrentDictionary<string, byte>>` tracks all active connection IDs per `UserId`.
- **First Connection Event**: Transitioning from 0 to 1 connection triggers database `User.IsOnline = true` and fires `UserOnline`.
- **Last Connection Event**: Removing a connection only triggers `UserOffline` and sets `User.IsOnline = false` when zero connections remain for that `UserId`.

---

## 5. Dynamic RBAC Integration (Phase 6 Permissions)

The following database permissions were seeded in `Permissions` and assigned dynamically:

| Permission Code | Description | Default Roles Assigned |
| :--- | :--- | :--- |
| `CHAT_READ` | Read chat messages and view history | SuperAdmin, Admin, CircleOwner, CircleAdmin, CircleMember, User |
| `CHAT_SEND` | Send text and multimedia in chat | SuperAdmin, Admin, CircleOwner, CircleAdmin, CircleMember, User |
| `CHAT_EDIT_OWN` | Edit own messages in circle | SuperAdmin, Admin, CircleOwner, CircleAdmin, CircleMember, User |
| `CHAT_DELETE_OWN` | Soft delete own messages in circle | SuperAdmin, Admin, CircleOwner, CircleAdmin, CircleMember, User |
| `CHAT_MODERATE` | Soft delete/moderate any circle message | SuperAdmin, Admin, CircleOwner, CircleAdmin |
| `CHAT_VIEW_HISTORY`| View paginated history of circle | SuperAdmin, Admin, CircleOwner, CircleAdmin, CircleMember, User |

---

## 6. REST API Endpoints (`CircleChatController`)

Base Route: `/api/circles/{circleId:int}`

| HTTP Method | Route | Description | Authorization |
| :--- | :--- | :--- | :--- |
| `GET` | `/messages?pageNumber=1&pageSize=30` | Returns chronological paginated history | Active Circle Member + `CHAT_VIEW_HISTORY` |
| `POST` | `/messages` | REST fallback to send message | Active Circle Member + `CHAT_SEND` |
| `PUT` | `/messages/{messageId:int}` | Edit message content | Message Sender OR Circle Moderator |
| `DELETE` | `/messages/{messageId:int}` | Soft delete message | Message Sender OR Circle Moderator |
| `GET` | `/presence` | Live member online statuses | Active Circle Member |

---

## 7. Blazor Frontend (`CircleChat.razor`)

Route: `/circles/{CircleId:int}/chat`

### Features
1. **Header Bar**:
   - Back button to `/circles`.
   - Circle icon sticker (`🌸`, `🎀`, `🧸`, etc.) and circle title.
   - Role badge (`👑 Circle Owner`, `⭐ Circle Admin`, `🌸 Member`).
   - Live member presence count (`🟢 X of Y besties online`).
   - Roster Drawer toggle button.
2. **Message Area**:
   - Automatic chronological date separators (`Today`, `Yesterday`, `Nov 9, 2026`).
   - Right-aligned pastel strawberry bubbles for the current user.
   - Left-aligned white/cream bubbles with avatar sticker, display name, and timestamp for other members.
   - Inline message edit with Save/Cancel controls.
   - Soft-delete indicator (`🌸 This message was deleted`).
   - `(edited)` indicator for edited messages.
   - `📜 Load older bestie messages` button for server-side pagination.
3. **Message Input Composer**:
   - Multiline textarea supporting Enter to send and Shift+Enter for newlines.
   - Live character counter (`45/2000`) with red warning over 1,900 chars.
   - Cute send button (`💌 Send`) with sending animation state.
4. **Reconnection & Resilience**:
   - Configured with `WithAutomaticReconnect([0s, 2s, 5s, 10s])`.
   - On reconnect, automatically re-joins the circle group and refetches latest messages to ensure zero lost messages.
