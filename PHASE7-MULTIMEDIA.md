# BestieSpace – Phase 7: Multimedia Messaging Architecture & Implementation 🌸📸🎤

This document outlines the design, architecture, and verification of **Phase 7: Multimedia Messaging (Photo Sharing, Built-in Stickers & Voice Messages)** for BestieSpace.

---

## 1. Overview & Technology Stack

- **Framework:** ASP.NET Core Web API & Blazor Server (.NET 10)
- **Database:** Microsoft SQL Server with Entity Framework Core 10
- **Real-Time Communication:** ASP.NET Core SignalR (`/chathub`)
- **Storage:** Secure file isolation outside `wwwroot` in `App_Data/media` with GUID filenames and zero physical path leakage.
- **Client:** Interactive Blazor Server with JS Interop for browser audio recording (`MediaRecorder`) and rich pastel multimedia chat UI.

---

## 2. Multimedia Architecture & Security Isolation

### A. Isolated File Storage
- Physical files are stored strictly outside `wwwroot` under:
  - Images: `App_Data/media/images/{circleId}/{yyyyMMdd}/{guid}.{ext}`
  - Thumbnails: `App_Data/media/images/{circleId}/{yyyyMMdd}/{guid}_thumb.{ext}`
  - Voice Notes: `App_Data/media/voice/{circleId}/{yyyyMMdd}/{guid}.{ext}`
- Internal physical storage paths are never leaked to the client or returned in API responses.
- Streaming is only accessible via authorized endpoints:
  - `GET /api/circles/{circleId}/media/{mediaId}`
  - `GET /api/circles/{circleId}/media/{mediaId}/thumb`

### B. Strict Circle Authorization
- Every media request validates that:
  1. The caller is an authenticated user with a valid JWT token.
  2. The caller is an active member of `{circleId}` (`ICircleAuthorizationService.IsCircleMemberAsync`).
  3. The `MediaFile` belongs specifically to `{circleId}` and is not soft-deleted (`IsDeleted == false`).
- Cross-circle media leakage is strictly blocked.

### C. Magic Byte Inspection (Anti-Spoofing)
Every uploaded binary stream is inspected at the header level to prevent malicious file extension spoofing:
- **JPEG:** `FF D8 FF`
- **PNG:** `89 50 4E 47 0D 0A 1A 0A`
- **WebP:** Starts with `RIFF` (`52 49 46 46`), bytes 8..11 match `WEBP` (`57 45 42 50`)
- **WebM:** EBML header `1A 45 DF A3`
- **Ogg:** `4F 67 67 53` (`OggS`)
- **WAV:** Starts with `RIFF`, bytes 8..11 match `WAVE` (`57 41 56 45`)
- **MP3:** ID3 tag `49 44 33` or MPEG frame sync `FF FB` / `FF F3` / `FF F2`
- **M4A/MP4:** Contains `ftyp` (`66 74 79 70`) in the header.

Any file whose magic bytes do not match the declared extension is immediately rejected with `400 Bad Request`.

---

## 3. Database Schema Extensions

### A. `MediaFiles` Table
| Column | Type | Description |
|---|---|---|
| `MediaId` | INT (PK, Identity) | Unique media file identifier |
| `CircleId` | INT (FK -> BestieCircles) | Associated Bestie Circle (CASCADE) |
| `UploadedByUserId` | INT (FK -> Users) | Uploader user account |
| `StorageKey` | NVARCHAR(260) | Internal relative path in `App_Data/media` |
| `ThumbnailStorageKey` | NVARCHAR(260), NULL | Internal thumbnail path |
| `OriginalFileName` | NVARCHAR(260) | Sanitized original filename |
| `ContentType` | NVARCHAR(100) | MIME type (e.g. `image/jpeg`, `audio/webm`) |
| `MediaType` | NVARCHAR(20) | `image` or `voice` |
| `FileSize` | BIGINT | Size in bytes (max 10MB photo, 15MB voice) |
| `DurationSeconds` | FLOAT, NULL | Duration for audio clips (max 120s) |
| `CreatedAt` | DATETIME2 | Upload timestamp (UTC) |
| `IsDeleted` | BIT | Soft-delete status |
| `DeletedAt` | DATETIME2, NULL | Soft-deletion timestamp |

### B. `StickerCategories` Table
| Column | Type | Description |
|---|---|---|
| `CategoryId` | INT (PK, Identity) | Category ID |
| `CategoryName` | NVARCHAR(50), UNIQUE | e.g. "Cute", "Love", "Happy", "Sad", "Funny", "Miss You", "Good Morning", "Good Night", "Hearts" |
| `Icon` | NVARCHAR(50) | Category display icon/emoji |
| `DisplayOrder` | INT | Sort order |
| `IsActive` | BIT | Active toggle |
| `CreatedAt` | DATETIME2 | Creation date |

### C. `Stickers` Table
| Column | Type | Description |
|---|---|---|
| `StickerId` | INT (PK, Identity) | Sticker ID |
| `CategoryId` | INT (FK -> StickerCategories) | Parent category |
| `StickerName` | NVARCHAR(100) | Sticker label |
| `ImageUrl` | NVARCHAR(500) | Emoji or SVG/WebP URL |
| `DisplayOrder` | INT | Sort order |
| `IsActive` | BIT | Active toggle |
| `CreatedAt` | DATETIME2 | Creation date |

### D. `ChatMessages` Table Extensions
- `ThumbnailUrl` (NVARCHAR(1000), NULL)
- `FileName` (NVARCHAR(260), NULL)
- `ContentType` (NVARCHAR(100), NULL)
- `FileSize` (BIGINT, NULL)
- `MediaId` (INT, NULL, FK -> MediaFiles)
- `VoiceDurationSeconds` / `DurationSeconds` (FLOAT, NULL)

---

## 4. Dynamic RBAC Permissions Seeded

| Permission Code | Description | Default Roles Assigned |
|---|---|---|
| `CHAT_SEND_IMAGE` | Upload and share photo messages | SuperAdmin, Admin, User, Bestie, CircleOwner, CircleAdmin, CircleMember |
| `CHAT_SEND_STICKER` | Send stickers from the built-in catalog | SuperAdmin, Admin, User, Bestie, CircleOwner, CircleAdmin, CircleMember |
| `CHAT_SEND_VOICE` | Record and send voice messages | SuperAdmin, Admin, User, Bestie, CircleOwner, CircleAdmin, CircleMember |
| `CHAT_VIEW_MEDIA` | View and stream circle multimedia | SuperAdmin, Admin, User, Bestie, CircleOwner, CircleAdmin, CircleMember |
| `CHAT_DELETE_MEDIA` | Delete multimedia files and messages | SuperAdmin, Admin, CircleOwner, CircleAdmin |
| `STICKER_VIEW` | Browse sticker categories and stickers | SuperAdmin, Admin, User, Bestie, CircleOwner, CircleAdmin, CircleMember |
| `STICKER_MANAGE` | Create, edit, and delete sticker packs | SuperAdmin, Admin |

---

## 5. API Endpoints

### Media Endpoints (`/api/circles/{circleId}/media`)
- `GET /api/circles/{circleId}/media/{mediaId}`: Authenticated streaming with byte-range support.
- `GET /api/circles/{circleId}/media/{mediaId}/thumb`: Authenticated image thumbnail streaming.
- `POST /api/circles/{circleId}/media/photo`: Multipart image upload with optional caption.
- `POST /api/circles/{circleId}/media/photo-base64`: Base64 image payload upload.
- `POST /api/circles/{circleId}/media/voice`: Multipart voice note upload.
- `POST /api/circles/{circleId}/media/voice-base64`: Base64 voice note payload upload.
- `POST /api/circles/{circleId}/media/sticker`: Send sticker message by `stickerId`.

### Sticker Endpoints (`/api/stickers`)
- `GET /api/stickers/categories`: Returns active categories with nested active stickers.
- `GET /api/stickers/all`: Admin listing of all stickers including inactive ones.
- `POST /api/stickers/categories`: Admin create category.
- `POST /api/stickers`: Admin create sticker.
- `PUT /api/stickers/{id}`: Admin update sticker.
- `DELETE /api/stickers/{id}`: Admin delete sticker.

---

## 6. SignalR Real-Time Events (`BestieChatHub`)

| Event Name | Payload | Trigger Condition |
|---|---|---|
| `ReceiveMessage` | `CircleChatMessageDto` | Broadcast to `circle_{circleId}` whenever text, photo, sticker, or voice message is created |
| `MessageUpdated` | `CircleChatMessageDto` | Broadcast when a text message is edited |
| `MessageDeleted` | `CircleMessageDeletedEventDto` | Broadcast when any message is soft-deleted |
| `MediaUploaded` | `{ mediaId, circleId, mediaUrl, ... }` | Broadcast when a new photo is uploaded |
| `StickerSent` | `{ circleId, stickerId, imageUrl, ... }` | Broadcast when a sticker is sent |
| `VoiceMessageSent` | `{ mediaId, circleId, durationSeconds, ... }` | Broadcast when a voice message is sent |

---

## 7. Interactive Blazor UI Features (`CircleChat.razor`)

1. **Photo Upload & Preview Modal:**
   - 📸 Attach button invokes native `<InputFile>` filter.
   - Shows thumbnail preview dialog before upload with a cute caption input.
   - Clickable thumbnail in chat bubble opens a high-resolution lightbox modal.
2. **Sticker Picker Drawer:**
   - 🌸 Sticker button toggles an animated drawer with category tabs:
     - *Cute 🌸*, *Love 💖*, *Happy 🥳*, *Sad 🥺*, *Funny 😹*, *Miss You 💌*, *Good Morning ☀️*, *Good Night 🌙*, *Hearts 💕*.
   - One-click sticker delivery directly into the active circle chat.
3. **Voice Recording & Custom Pastel Player:**
   - 🎤 Mic button starts recording via HTML5 `MediaRecorder` with live timer (`0:05 / 2:00`) and animated waveform preview.
   - Voice message bubble features play/pause toggle, animated playback bars, and duration counter.

---

## 8. Verification & Test Output

All cryptographic, RBAC, social, circle, real-time presence, and Phase 7 multimedia tests passed cleanly:
```bash
dotnet run --project BestieSpace.Api/BestieSpace.Api.csproj -- --test-auth
```
- `[PASS]` PasswordHasher PBKDF2 verified
- `[PASS]` JWT Access Token generation & claim validation verified
- `[PASS]` Refresh Token generation and SHA-256 hash verified
- `[PASS]` Dynamic Permission Policy Provider verified (USER_MANAGE)
- `[PASS]` Canonical Friendship Pairing symmetry verified
- `[PASS]` Dynamic Phase 4 policies verified (FRIEND_REQUEST_SEND, USER_BLOCK, USER_SEARCH)
- `[PASS]` Maximum 9-Member Circle Capacity Invariant verified
- `[PASS]` Dynamic Phase 5 policies verified (CIRCLE_CREATE, CIRCLE_INVITE, CIRCLE_TRANSFER_OWNERSHIP)
- `[PASS]` Phase 6 Multi-Connection Presence Service verified
- `[PASS]` Dynamic Phase 6 policies verified (CHAT_SEND, CHAT_EDIT_OWN, CHAT_DELETE_OWN, CHAT_MODERATE, CHAT_VIEW_HISTORY)
- `[PASS]` Phase 7 Image Magic Byte inspection verified (JPEG, PNG, WebP vs malicious spoof: PASS)
- `[PASS]` Phase 7 Voice Magic Byte inspection verified (WebM, Ogg, WAV, MP3 vs malicious spoof: PASS)
- `[PASS]` Dynamic Phase 7 policies verified (CHAT_SEND_IMAGE, CHAT_SEND_STICKER, CHAT_SEND_VOICE, CHAT_VIEW_MEDIA, CHAT_DELETE_MEDIA, STICKER_VIEW, STICKER_MANAGE)
