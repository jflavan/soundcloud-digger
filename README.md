# SoundCloud Digger

A web app that gives you a better view of your SoundCloud feed. Sort by likes, plays, reposts, and more. Filter by genre, and discover the best tracks from the artists you follow.

## Features

- **Sort by likes** — surface the most popular tracks in your feed
- **Sort by plays** — find the most listened-to tracks
- **Sort by reposts** — see what's being shared the most
- **Sort by comments** — find tracks generating the most discussion
- **Sort by date** — see the latest tracks first
- **Time range filter** — view tracks from the last 24h, 7 days, 30 days, or all time
- **Genre filter** — multi-select dropdown to filter by genre tags
- **Duration filter** — dual-thumb range slider to filter tracks by length (0–60 min)
- **Auto-refresh** — feed updates in the background every 5 minutes

## Architecture

- **Backend:** .NET 10 Web API — handles SoundCloud OAuth, fetches and caches feed data
- **Frontend:** SvelteKit (Svelte 5) SPA — client-side sorting and filtering

The backend authenticates with SoundCloud via OAuth 2.1 + PKCE, fetches the user's feed in the background, and caches it in memory. The frontend receives the full dataset and performs all sorting/filtering client-side for instant responsiveness.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (v20+)
- A [SoundCloud developer app](https://soundcloud.com/you/apps) with redirect URI set to `http://localhost:5032/auth/callback`

## Setup

### 1. Configure SoundCloud credentials

Store your app credentials using .NET user secrets (keeps them out of source control).

Set the redirect URI in your [SoundCloud developer app](https://soundcloud.com/you/apps) to `http://localhost:5032/auth/callback`.

```bash
cd backend/src/SoundCloudDigger.Api
dotnet user-secrets set "SoundCloud:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "SoundCloud:ClientSecret" "YOUR_CLIENT_SECRET"
```

### 2. Install dependencies

```bash
# Backend
cd backend
dotnet restore

# Frontend
cd frontend
npm install
```

### 3. Run

Start both services in separate terminals:

```bash
# Terminal 1 — Backend (port 5032)
cd backend
dotnet run --project src/SoundCloudDigger.Api

# Terminal 2 — Frontend (port 5173)
cd frontend
npm run dev
```

Open `http://localhost:5173` and click "Log in with SoundCloud."

The Vite dev server proxies `/api` and `/auth` requests to the backend (port 5032) automatically.

## Running tests

```bash
# Backend (29 tests)
cd backend
dotnet test

# Frontend (17 tests)
cd frontend
npx vitest run
```

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
      components/      # TrackRow, TrackList, ControlsBar, DurationRangeSlider, LoadingIndicator
      stores/          # feedStore, filterStore, filteredFeedStore (derived)
      api.ts           # API client
      types.ts         # TypeScript types
    routes/
      +page.svelte     # Login page
      feed/+page.svelte # Feed page with polling
  tests/
```
