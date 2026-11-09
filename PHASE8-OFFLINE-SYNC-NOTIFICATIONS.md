# BestieSpace – Phase 8: Reliable Offline Messaging, Per-User Read Receipts, Unread Message Management and Notifications 🌸💌

## 1. Executive Summary

Phase 8 elevates **BestieSpace** into a production-grade, highly reliable group communication system. In a multi-user private group chat (Bestie Circles of up to 9 members), network interruptions, closed browser tabs, and offline devices are inevitable. Phase 8 ensures that:
1. **No messages are lost:** SQL Server is the authoritative persistent store for all messages, receipts, and notifications.
2. **Per-user independent read receipts:** In private circles, read receipts are tracked individually per recipient via `ChatMessageReads`. A message sent to a 9-member circle maintains isolated read timestamps for each member; sender self-reads are excluded.
3. **Delivery tracking vs Read status:** Message delivery (`ChatMessageDeliveries`) is acknowledged when a recipient's device receives the message payload, distinguishing between *Sent* (✓), *Delivered* (✓✓ grey), and *Read* (✓✓ pink).
4. **Offline message synchronization:** When a client reconnects or boots up, it queries `GET /api/circles/{circleId}/messages/sync?afterMessageId={lastId}&pageSize=50` to retrieve missed messages in chronological order.
5. **Durable notifications system:** Notifications are stored in SQL Server and pushed in real-time over user-targeted SignalR channels (`user_{userId}`) for friend requests, circle invitations, roster changes, and circle messages.
6. **Kawaii Blazor Server UI:** Interactive "Read by" details modal, message status ticks, Notification Center bell with live unread badge, and circle card unread indicators.

---

## 2. Database Architecture (SQL Server & EF Core 10)

### 2.1 Entity: `ChatMessageDelivery` (`ChatMessageDeliveries` table)
Tracks when a circle member's client device has received a message payload:
- `ChatMessageDeliveryId` (INT, PK, Identity)
- `ChatMessageId` (INT, FK -> `ChatMessages.Id` ON DELETE CASCADE)
- `UserId` (INT, FK -> `Users.UserId`)
- `DeliveredAt` (DATETIME2, UTC)
- **Constraint:** Unique index `UQ_ChatMessageDeliveries_Message_User` on `(ChatMessageId, UserId)`.

### 2.2 Entity: `Notification` (`Notifications` table)
Durable notification pipeline for all user-facing events:
- `NotificationId` (INT, PK, Identity)
- `UserId` (INT, FK -> `Users.UserId` ON DELETE CASCADE)
- `NotificationType` (NVARCHAR(50)): e.g. `FriendRequestReceived`, `FriendRequestAccepted`, `CircleInvitationReceived`, `CircleMemberJoined`, `CircleMemberRemoved`, `CircleMemberLeft`, `CircleOwnershipTransferred`, `NewChatMessage`.
- `Title` (NVARCHAR(150))
- `Message` (NVARCHAR(500))
- `RelatedEntityId` (INT, Nullable)
- `RelatedEntityType` (NVARCHAR(50), Nullable)
- `IsRead` (BIT, Default 0)
- `CreatedAt` (DATETIME2, UTC)
- `ReadAt` (DATETIME2, Nullable)
- **Indexes:** `IX_Notifications_UserId_IsRead`, `IX_Notifications_CreatedAt`.

### 2.3 Preserved Entity: `ChatMessageRead` (`ChatMessageReads` table)
Tracks individual member read receipts:
- `ChatMessageReadId` (INT, PK, Identity)
- `ChatMessageId` (INT, FK -> `ChatMessages.Id` ON DELETE CASCADE)
- `UserId` (INT, FK -> `Users.UserId`)
- `ReadAt` (DATETIME2, UTC)
- **Constraint:** Unique index `UQ_ChatMessageReads_Message_User` on `(ChatMessageId, UserId)`.

---

## 3. Dynamic RBAC Permissions & Roles

Seeded in `init.sql` and `DatabaseBootstrapper.cs`:

| Permission Code | Name | Description | Assigned Roles |
|---|---|---|---|
| `NOTIFICATION_VIEW` | View Notifications | View own notification inbox and unread badges | SuperAdmin, Admin, User, Bestie, CircleOwner, CircleAdmin, CircleMember |
| `NOTIFICATION_MANAGE` | Manage Notifications | Mark notifications as read or clear alerts | SuperAdmin, Admin, User, Bestie, CircleOwner, CircleAdmin, CircleMember |
| `MESSAGE_READ_RECEIPT_VIEW` | View Read Receipts | Inspect detailed read status breakdown for messages | SuperAdmin, Admin, User, Bestie, CircleOwner, CircleAdmin, CircleMember |
| `MESSAGE_SYNC` | Synchronize Messages | Pull missed offline messages with cursor pagination | SuperAdmin, Admin, User, Bestie, CircleOwner, CircleAdmin, CircleMember |

---

## 4. SignalR Real-Time Infrastructure (`BestieChatHub`)

### 4.1 Personal User Channels
- On connection (`OnConnectedAsync`), authenticated users are automatically added to group `user_{userId}`.
- Allows direct push of notifications and delivery updates across multiple open tabs or windows.

### 4.2 Hub Methods
- `JoinCircle(int circleId)`: Validates active circle membership before joining group `circle_{circleId}`.
- `LeaveCircle(int circleId)`: Leaves circle group and updates multi-connection presence.
- `SendMessage(SendCircleMessageRequestDto)`: Dispatches message to circle and creates notifications for offline members.
- `MarkMessagesRead(MarkMessagesReadRequestDto)`: Records read receipts and broadcasts `MessagesRead` event.
- `AcknowledgeDelivery(AcknowledgeDeliveryRequestDto)`: Records delivery receipts and broadcasts `MessagesDelivered` event.

### 4.3 Client Real-Time Events
- `ReceiveMessage` (`CircleChatMessageDto`)
- `MessagesRead` (`MessageReadEventDto`)
- `MessagesDelivered` (`MessageDeliveredEventDto`)
- `ReceiveNotification` (`NotificationDto`)
- `UserOnline` / `UserOffline` (`CircleMemberPresenceEventDto`)

---

## 5. REST API Endpoints

### 5.1 Read Receipts & Unread Counters (`MessageReadController`)
- `POST /api/circles/{circleId}/messages/read` – Records read receipts for batch of message IDs.
- `GET /api/circles/{circleId}/messages/{messageId}/read-receipts` – Returns detailed recipient breakdown (Total, Read, Delivered, Pending).
- `GET /api/circles/unread-summary` – Returns total unread count and per-circle unread counts for authenticated user.
- `GET /api/circles/{circleId}/unread-count` – Returns unread count for a single circle.

### 5.2 Offline Synchronization & Delivery (`MessageSyncController`)
- `GET /api/circles/{circleId}/messages/sync?afterMessageId={id}&pageSize=50` – Chronological cursor-based sync of messages newer than `afterMessageId`.
- `POST /api/circles/{circleId}/messages/delivery` – Batch delivery acknowledgement when messages are received by client.

### 5.3 Notifications (`NotificationController`)
- `GET /api/notifications?pageNumber=1&pageSize=20&unreadOnly=false` – Paginated list of notifications.
- `GET /api/notifications/unread-count` – Fast query returning unread notification count.
- `PUT /api/notifications/{id}/read` – Marks single notification as read.
- `PUT /api/notifications/read-all` – Marks all user notifications as read.

---

## 6. Frontend & Blazor Server UI Enhancements

1. **Circle Chat Status Indicators (`CircleChat.razor`)**:
   - Sent tick: `✓` (single light grey tick).
   - Delivered tick: `✓✓` (double grey tick) + count `@msg.DeliveredCount/@msg.TotalRecipients`.
   - Read tick: `✓✓` (double pink `#F72585` tick) + count `@msg.ReadCount/@msg.TotalRecipients`.
2. **"Read by" Details Modal**:
   - Displays message preview (text, photo, sticker, or voice).
   - Summary stat pills for Read, Delivered, and Total Besties.
   - List of readers with name, avatar sticker, and exact time read.
   - List of delivered devices (unopened) and offline members awaiting sync.
3. **Notification Center (`NotificationCenter.razor`)**:
   - Bell icon with pulsating pink badge when unread alerts exist.
   - Dropdown with filter tabs ("All" / "Unread").
   - Category stickers: 💌 (Friend Request), 💖 (Accepted), 🌸 (Circle Invite), 🎉 (Joined), 👑 (Ownership), 💬 (Chat).
   - "Mark all read" action.
   - Clicking notification navigates to the relevant page (Friends hub, Circles hub, or Circle chat).
   - Connected to SignalR for real-time alert updates without page refresh.
4. **Offline Reconnection Cursor**:
   - On SignalR reconnect, `CircleChat` uses `SyncCircleMessagesAsync` with the highest received message ID to seamlessly backfill any offline messages.
5. **Circle Cards Unread Badge (`CirclesHub.razor`)**:
   - "💬 Open Chat" button displays `[X new]` badge when unread messages are present.
6. **Navigation Menu (`NavMenu.razor`)**:
   - Bell notification center embedded in top navbar.
   - Unread badge counter on "Bestie Circles" link.

---

## 7. Verification & Testing

Run the automated test suite without requiring an active database:
```powershell
dotnet run --project BestieSpace.Api/BestieSpace.Api.csproj -- --test-auth
```

All 9 test suites verify successfully:
- [PASS] PasswordHasher PBKDF2
- [PASS] JWT Access Token generation & claim validation
- [PASS] Refresh Token generation and SHA-256 hash
- [PASS] Dynamic Permission Policy Provider ('USER_MANAGE')
- [PASS] Canonical Friendship Pairing symmetry
- [PASS] Dynamic Phase 4 policies (FRIEND_REQUEST_SEND, USER_BLOCK, USER_SEARCH)
- [PASS] Maximum 9-Member Circle Capacity Invariant
- [PASS] Dynamic Phase 5 policies (CIRCLE_CREATE, CIRCLE_INVITE, CIRCLE_TRANSFER_OWNERSHIP)
- [PASS] Phase 6 Multi-Connection Presence Service (Multi-tab online continuity)
- [PASS] Dynamic Phase 6 policies (CHAT_SEND, CHAT_EDIT_OWN, CHAT_DELETE_OWN, CHAT_MODERATE, CHAT_VIEW_HISTORY)
- [PASS] Phase 7 Image Magic Byte inspection (JPEG, PNG, WebP vs spoof)
- [PASS] Phase 7 Voice Magic Byte inspection (WebM, Ogg, WAV, MP3 vs spoof)
- [PASS] Dynamic Phase 7 policies (CHAT_SEND_IMAGE, CHAT_SEND_STICKER, CHAT_SEND_VOICE, CHAT_VIEW_MEDIA, CHAT_DELETE_MEDIA, STICKER_VIEW, STICKER_MANAGE)
- [PASS] Dynamic Phase 8 policies (NOTIFICATION_VIEW, NOTIFICATION_MANAGE, MESSAGE_READ_RECEIPT_VIEW, MESSAGE_SYNC)
- [PASS] Phase 8 Per-User Independent Read Receipt Invariant (Sender self-read excluded, per-recipient state isolated)
