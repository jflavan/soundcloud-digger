# SoundCloud Digger

A web app that gives you a better view of your SoundCloud feed. Sort by likes, plays, reposts, and more. Filter by genre, and discover the best tracks from the artists you follow.

## Features

### Sorting and filtering

- **Sort by likes, plays, reposts, comments, or date** — surface what matters first
- **Time range filter** — view tracks from the last 24h, 7 days, 30 days, or all time
- **Time field toggle** — filter the time range by when a track appeared in your feed or when it was uploaded
- **Genre include filter** — multi-select dropdown to show only selected genres
- **Genre exclude filter** — multi-select dropdown to hide selected genres (wins over include)
- **Duration filter** — dual-thumb range slider with manual time entry (0–60 min)

### Playback

- **Inline bottom player** — click a track to open a persistent SoundCloud embed at the bottom of the page
- **Autoplay next** — when a track finishes, the next one starts automatically
- **Prev / next controls** — step through the current list
- **Shuffle mode** — random playback with no repeats until the queue is exhausted, then reshuffles; prev steps back through shuffle history
  - Shuffle toggle in the player while playing, and as a FAB when no track is active (clicking it starts a random track)
- **Click-through to SoundCloud** — the artwork and title open the track page; the artist name opens the artist page (new tab)
- **Keyboard shortcuts** (active while the player is open and focus is not on an input/button)
  - `Space` — play / pause
  - `←` / `→` — seek backward / forward by 10 seconds
  - `Ctrl`/`Cmd` + `←` / `→` — previous / next track in the queue

### UI

- **Floating action buttons** — refresh the feed, scroll to the currently playing track, scroll to top, or toggle shuffle
- **Auto-refresh** — feed updates in the background every minute

## Architecture

- **Backend:** .NET 10 Web API — handles SoundCloud OAuth, fetches and caches feed data
- **Frontend:** SvelteKit (Svelte 5) SPA — client-side sorting and filtering

The backend authenticates with SoundCloud via OAuth 2.1 + PKCE, fetches the user's feed in the background, and caches it in memory. The frontend receives the full dataset and performs all sorting/filtering client-side for instant responsiveness.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (v20+)

## Quick Start

**macOS / Linux:**

```bash
./start.sh
```

**Windows (PowerShell):**

```powershell
.\start.ps1
```

**Windows (WSL / Git Bash):**

```bash
./start.sh
```

> `start.sh` is a Bash script, so on Windows it requires a Bash-compatible terminal such as [WSL](https://learn.microsoft.com/en-us/windows/wsl/install) or [Git Bash](https://git-scm.com/downloads). For native Windows use, run `start.ps1` in PowerShell instead.

Open `http://scdigger.localhost:5173` — the setup wizard will walk you through registering a SoundCloud app and entering your credentials. That's it.

> The app uses `scdigger.localhost` instead of `localhost` so the OAuth session cookie is scoped correctly across the frontend and backend. The `.localhost` TLD is reserved by RFC 6761 and resolves to `127.0.0.1` automatically on all modern operating systems (macOS, Linux with glibc 2.26+, Windows 10+) — no `/etc/hosts` edit required. If your system is old enough that it doesn't resolve, add `127.0.0.1 scdigger.localhost` to your hosts file.

## Setup (manual)

If you prefer to set things up manually instead of using the setup wizard:

### 1. Configure SoundCloud credentials

Copy the example env file and fill in your credentials:

```bash
cp backend/src/SoundCloudDigger.Api/.env.example backend/src/SoundCloudDigger.Api/.env
```

Or use .NET user secrets:

```bash
cd backend/src/SoundCloudDigger.Api
dotnet user-secrets set "SoundCloud:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "SoundCloud:ClientSecret" "YOUR_CLIENT_SECRET"
```

Set the redirect URI in your [SoundCloud developer app](https://soundcloud.com/you/apps) to `http://scdigger.localhost:5173/auth/callback`.

### 2. Run

```bash
./start.sh          # macOS / Linux / WSL / Git Bash
# or: make dev
```

```powershell
.\start.ps1         # Windows (PowerShell)
```

Or start each service manually:

```bash
# Terminal 1 — Backend (port 5032)
cd backend && dotnet run --project src/SoundCloudDigger.Api

# Terminal 2 — Frontend (port 5173)
cd frontend && npm run dev
```

Open `http://scdigger.localhost:5173` and click "Log in with SoundCloud."

The Vite dev server proxies `/api` and `/auth` requests to the backend (port 5032) automatically. The OAuth callback path (`/auth/callback`) goes through the proxy so the session cookie set by the backend is scoped to the same origin the browser sees.

## Running tests

```bash
# Backend
cd backend
dotnet test

# Frontend
cd frontend
npm test                 # run the suite once
npm run test:coverage    # run with coverage report (fails under 80%)
```

The frontend coverage threshold is 80% across statements, branches, functions, and lines, enforced by `vitest --coverage`. The HTML report is written to `frontend/coverage/`.

## Project structure

```
backend/
  src/SoundCloudDigger.Api/
    Controllers/       # Auth (OAuth flow) and Feed (cached data) endpoints
    Models/            # SoundCloud API DTOs, FeedTrack domain model, FeedResponse
    Services/          # SoundCloudClient, TokenService, FeedCache, FeedService
    Helpers/           # PKCE code challenge generation
  tests/SoundCloudDigger.Tests/

frontend/
  src/
    lib/
      components/      # TrackRow, TrackList, ControlsBar, DurationRangeSlider, LoadingIndicator, BottomPlayer
      stores/          # feedStore, filterStore, filteredFeedStore (derived), shuffleQueue
      utils/           # keyboardShortcuts, duration (pure helpers)
      api.ts           # API client
      types.ts         # TypeScript types
    routes/
      +page.svelte         # Login page
      feed/+page.svelte    # Feed page — polling, FABs, player wiring, shuffle state
  tests/
```
