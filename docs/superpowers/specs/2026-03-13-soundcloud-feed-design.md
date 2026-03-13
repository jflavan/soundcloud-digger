# SoundCloud Digger — Feed Sort & Filter

A web app that surfaces your SoundCloud feed with sorting by likes and filtering by genre tag, giving you better control over content from artists you follow.

## Architecture

Two services, no database.

**Backend (.NET Web API)** handles SoundCloud OAuth, fetches and caches the user's feed, and exposes a REST API for the frontend. All SoundCloud credentials and tokens stay server-side.

**Frontend (Svelte SPA)** renders the feed with sorting/filtering controls. Receives the full feed dataset from the backend on load, then performs all sorting and filtering client-side for instant interaction.

```
User ──► Svelte SPA ──► .NET API ──► SoundCloud API
                          │
                     in-memory cache
                     (per-user, ~5 min TTL)
```

## Authentication

OAuth 2.1 Authorization Code flow with PKCE, per SoundCloud's requirements.

### Flow

1. User clicks "Log in with SoundCloud"
2. Backend generates PKCE code_verifier + code_challenge (S256), stores verifier in session
3. Redirect to `https://secure.soundcloud.com/authorize` with client_id, redirect_uri, response_type=code, code_challenge, code_challenge_method=S256, state
4. User approves on SoundCloud
5. SoundCloud redirects to backend callback with authorization code
6. Backend exchanges code + code_verifier for tokens via `POST https://secure.soundcloud.com/oauth/token` (grant_type=authorization_code, includes client_id and client_secret)
7. Tokens stored in an in-memory session store on the server. A session ID is issued to the client as an HTTP-only cookie — tokens never leave the server
8. User redirected to Svelte app, background feed fetch kicks off

### Token Management

- Auth header format: `Authorization: OAuth ACCESS_TOKEN`
- Tokens expire ~1 hour
- On 401, backend refreshes via `POST https://secure.soundcloud.com/oauth/token` with grant_type=refresh_token, client_id, client_secret
- Refresh tokens are single-use — each refresh returns a new refresh token that replaces the old one
- Sign-out: `POST https://secure.soundcloud.com/sign-out` with access_token in JSON body

## Backend

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/auth/login` | Builds OAuth URL with PKCE, redirects to SoundCloud |
| GET | `/auth/callback` | Exchanges code for tokens, stores in session, redirects to frontend |
| POST | `/auth/logout` | Calls SoundCloud sign-out, clears session |
| GET | `/api/feed` | Returns cached feed data + metadata |

`GET /api/feed` response:

```json
{
  "tracks": [FeedTrack],
  "totalCount": 1842,
  "loadingComplete": true
}
```

`loadingComplete` is `false` while the background feed fetch is still running. When `false`, `tracks` contains whatever has been fetched so far and `totalCount` reflects the current count. The frontend polls this endpoint every 2 seconds until `loadingComplete` is `true`.

### Feed Service

**Initial fetch (on auth):** Background job pages through `GET https://api.soundcloud.com/me/feed/tracks` using `next_href` for pagination (max 200 items per page). Continues until all items are older than 24 hours or 10,000 tracks reached.

**Background refresh:** Every ~5 minutes, fetches new items. Stops when it encounters tracks already in cache.

**Rate limit awareness:** Tracks API call count and backs off with exponential delay on 429 responses. The documented rate limits (50 tokens/12h per app, 30/1h per IP) are for the client credentials flow; the authorization code flow may have different limits. The backend should detect 429s dynamically rather than relying on hardcoded thresholds.

**Cache:** In-memory, keyed per user, ~5 minute TTL for refresh checks. Full dataset retained until session expires.

### FeedTrack Model

Mapped from SoundCloud's track schema (via the `origin` field in activity items):

| Field | Source | Type |
|-------|--------|------|
| title | `origin.title` | string |
| artistName | `origin.user.username` | string |
| artworkUrl | `origin.artwork_url` | string |
| genre | `origin.genre` | string |
| tags | `origin.tag_list` (parsed from space-separated string) | string[] |
| likesCount | `origin.favoritings_count` | int |
| playbackCount | `origin.playback_count` | int |
| createdAt | `origin.created_at` | datetime |
| permalinkUrl | `origin.permalink_url` | string |
| duration | `origin.duration` | int (ms) |
| access | `origin.access` | string |
| activityType | activity `type` field | string |
| appearedAt | activity `created_at` field | datetime |

## Frontend

### Pages

**LoginPage** — "Log in with SoundCloud" button. Redirects to `/auth/login`.

**FeedPage** — main view after authentication.

### Components

**ControlsBar** — horizontal bar above the track list:
- Sort toggle: Likes / Date (default: Likes)
- Time range buttons: 24h / 7d / 30d / All (default: 24h)
- Genre filter: multi-select dropdown populated from the `genre` field of tracks in the feed. This filters on the `genre` field only, not `tag_list`

**TrackList** — scrollable vertical list of TrackRow components.

**TrackRow** — single track row: artwork thumbnail, title, artist name, genre tag, like count, duration. Click opens track on SoundCloud via `permalinkUrl`.

**LoadingIndicator** — shown during initial feed fetch with progress ("Loading your feed... 400 tracks fetched").

### State Management

Svelte stores:
- `feedStore` — raw feed data from backend
- `filterStore` — current sort, time range, and tag selections
- `filteredFeedStore` (derived) — computed from feedStore + filterStore

All sorting and filtering happens client-side after the initial load. The backend is only called for auth, initial feed fetch, and periodic refresh. Available genres for the filter dropdown are derived client-side from the `genre` field across all tracks in `feedStore`.

**Frontend refresh:** The frontend polls `GET /api/feed` every 60 seconds after initial load is complete, to pick up new tracks from the backend's background refresh.

### Layout

Horizontal controls bar at the top, vertical track list below. Each track row shows: artwork (40px square), title, artist, genre tag, like count, and duration. Sorted by the active sort option within the active time range.

## Error Handling

| Scenario | Behavior |
|----------|----------|
| OAuth failure | Error message with retry button on LoginPage |
| Token expiry during fetch | Backend refreshes transparently, retries request. If refresh fails → 401 → redirect to login |
| Rate limiting (429) | Backend backs off with exponential delay. Frontend shows partial data indicator |
| Empty feed | Friendly empty state message |
| SoundCloud API down | Return last cached data with stale indicator. No cache → error with retry |
| Feed exceeds 10k tracks | Stop fetching, work with what we have |

## Testing

**Backend unit tests:** Feed service (pagination assembly, cache management, 429 backoff logic), auth (PKCE generation, token exchange, refresh token rotation).

**Frontend unit tests:** Sorting logic (by likes, by date), filtering logic (time range, genre), derived store computation. ControlsBar state changes, TrackList rendering and empty states.

**Backend integration tests:** Optional round-trip against SoundCloud API with test account.

**E2E test:** Login → feed load → sort by likes → filter by genre → verify results.
