# 🌸 BestieSpace — Kawaii Digital Memory Box & Real-Time Chat

A full-stack C# solution built with **ASP.NET Core Web API (Feature-Based Architecture)**, **Entity Framework Core (Database-First Scaffolded Style)**, **SignalR**, and an interactive **Blazor UI** with a pastel kawaii aesthetic.

Built with lots of love to tell your best friend how much you miss her, celebrate your favorite memories in tilted Polaroid frames, unlock mystery envelopes with heartfelt secret notes, and chat in real-time with text, stickers, photos, and voice notes! 💌✨

---

## 🎀 Architecture Overview

The solution `BestieSpace.sln` is organized into three clean projects:

```
BestieSpace/
│
├── BestieSpace.Shared/              # Shared DTOs and contracts
│   └── DTOs/
│       ├── DashboardDto.cs          # Dashboard composite data
│       ├── AppStatDto.cs            # Welcome letter & Miss You counter
│       ├── MemoryDto.cs             # Polaroid photos & corner stickers
│       ├── SecretNoteDto.cs         # Mystery envelopes & secrets
│       └── ChatMessageDto.cs        # Text, sticker, photo, voice chat messages
│
├── BestieSpace.Api/                 # ASP.NET Core Web API (Vertical Slice Architecture)
│   ├── Infrastructure/Data/
│   │   ├── init.sql                 # SQL schema script for DB-First setup
│   │   ├── BestieDbContext.cs       # EF Core scaffolded DbContext with Fluent API
│   │   ├── DatabaseBootstrapper.cs  # Auto-creates DB, tables & seeds heartfelt data
│   │   └── Entities/                # AppStat, Memory, SecretNote, ChatMessage
│   │
│   ├── Features/
│   │   ├── Dashboard/               # DashboardController & DashboardService
│   │   ├── Memories/                # MemoriesController & MemoriesService
│   │   ├── SecretNotes/             # SecretNotesController & SecretNotesService
│   │   └── Chat/                    # ChatController, ChatService & BestieChatHub (SignalR)
│   │
│   └── wwwroot/                     # Static file storage (Polaroids, voice notes, photos)
│       ├── images/sample-memories/  # Adorable SVG illustrations
│       └── uploads/                 # User-uploaded multimedia
│
└── BestieSpace.Client/              # Blazor Web App (Kawaii Pastel UI/UX)
    ├── Components/
    │   ├── Pages/Home.razor         # Interactive Welcome Gate & 4 Main Sections
    │   └── Layout/MainLayout.razor
    ├── Services/BestieApiClient.cs  # HTTP client communicating with Web API
    └── wwwroot/
        ├── app.css                  # Pastel kawaii styling, pill buttons, floating stickers
        ├── js/voiceRecorder.js      # MediaRecorder JS Interop for voice recording & player
        └── js/confettiBurst.js      # Heart/confetti burst & floating sticker animations
```

---

## 🎨 Visual Theme & Kawaii Aesthetics

- **Pastel Palette:**
  - `#FFFDF9` Warm Cream Background with subtle polka-dot pattern
  - `#FFE5EC` Blush Pink Cards & Containers
  - `#FF8FAB` Strawberry Pink Accents & Buttons
  - `#FB6F92` Vibrant Strawberry Pink for Primary Actions & Badges
  - `#E2F0CB` Soft Mint Green Accents
  - `#4A3B3C` Warm Cocoa Text
- **Typography:** Google Fonts `'Fredoka'` and `'Quicksand'`
- **Components:** Rounded pill buttons (`border-radius: 9999px`), soft pink glow drop-shadows, and floating kawaii sticker animations (🎀, 🧸, ✨, 💖).

---

## ✨ Features Walkthrough

### 1. Step 1: Interactive Welcome Gate
- When she opens the website, she is greeted with **ONLY** a full-screen cute animated gift card with floating stickers:
  > *"Hi Bestie! Click to open your surprise 💌"*
- Clicking the bouncing button fires an animated confetti and heart burst, smoothly revealing the full clubhouse experience.

### 2. Step 2: Main Experience Sections
1. **Heartfelt Letter & Miss You Meter:**
   - A stationery card featuring a heartfelt letter titled *"To My Favorite Human in the Entire World 🌸"*.
   - A live **Miss You Meter** badge (initial count = 1000).
   - An interactive **"I Miss You Too! 💖"** button: increments the database count in real-time and spawns floating hearts across the screen.
2. **Polaroid Photo Gallery (+ Add Photo Modal):**
   - Displays photos in tilted Polaroid frames (`rotate(-2.5deg)` / `rotate(2.0deg)`) with cute corner stickers (🎀 Bow, 🧸 Bear, ✨ Sparkle, 💖 Heart).
   - **"Add Our Photo 📸"** button opens a cute modal with image picker, live preview, caption input, sticker selector, and date picker that uploads to `/api/memories`.
3. **"Explore More" Mystery Envelopes:**
   - Clickable cute envelopes with sealed wax ribbons (*"Open when you miss me"*, *"My favorite memory of us"*, *"10 reasons why you're my bestie"*, *"A pinky promise"*).
   - Clicking an envelope unlocks it via `PUT /api/notes/{id}/unlock`, opens a stationery modal revealing the secret heartfelt letter and timestamp.
4. **Real-Time Multimedia Bestie Chat Box:**
   - Connected via **SignalR** (`/chathub`) for instant synchronization across devices.
   - **Sender Toggle:** Switch chatting identity between **"Me 🎀"** and **"Bestie 🌸"** with an animated pill selector.
   - **Text & Kawaii Stickers:** Text input plus a collapsible sticker tray (🧸 Hugging Bear, 😿 Missing-You Cat, 💖 Sparkling Heart, 🧋 Boba Cheers, 🐰 Bunny, 🍓 Sweet Strawberry, 🤞 Pinky Promise, 👭 BFFs).
   - **Photo Sharing (`📸`):** Pick any photo, uploads to `/api/chat/upload-photo`, displays in chat bubble with click-to-open lightbox preview.
   - **Voice Notes (`🎤`):** Uses browser `MediaRecorder` API (`voiceRecorder.js`) with pulsing pink timer (`Recording... 0:05`), uploads audio blob to `/api/chat/upload-voice`, and renders a custom pastel audio player with Play/Pause and waveform bars.

---

## 🚀 Running BestieSpace

### Prerequisites
- .NET 8 or .NET 10 SDK installed.

### Option 1: Run Web API and Blazor Client in Parallel

**Terminal 1 — Run Web API:**
```bash
dotnet run --project BestieSpace.Api\BestieSpace.Api.csproj
```
*API will run on `http://localhost:5000` (auto-bootstraps database, seeds initial data, and hosts SignalR at `/chathub`).*

**Terminal 2 — Run Blazor UI:**
```bash
dotnet run --project BestieSpace.Client\BestieSpace.Client.csproj
```
*Blazor UI will run on `http://localhost:5002`.*

Open your browser to: **`http://localhost:5002`** and enjoy the surprise! 🌸

---

## 🧪 Automated Self-Verification

You can run the built-in self-verification suite to test the database bootstrapper, EF Core scaffolded queries, and all feature services:

```bash
dotnet run --project BestieSpace.Api\BestieSpace.Api.csproj -- --verify
```
Outputs:
```text
================ BESTIESPACE VERIFICATION ================
[PASS] Dashboard fetched. Letter: 'To My Favorite Human in the Entire World 🌸', MissYouCount: 1000
[PASS] MissYouCount incremented to: 1001
[PASS] Seeded Memories count: 4
[PASS] New Memory added ID: 5, Caption: 'Test Polaroid Memory 🌸', Angle: 2.2
[PASS] Secret Notes count: 4
[PASS] Note 1 unlocked. Title: 'Open when you miss me 💌', IsUnlocked: True
[PASS] Initial Chat Messages: 2
[PASS] Text message sent ID: 3, Content: 'Testing our cute chat pipeline! 🌸'
[PASS] Sticker message sent ID: 4, Content: 'hugging-bear'
================ ALL VERIFICATIONS PASSED! ================
```
