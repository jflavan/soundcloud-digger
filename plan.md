# Plan: First-Time Setup Experience

## Context

A first-time user who downloads this repo currently must: install .NET 10 SDK + Node.js, learn `dotnet user-secrets`, figure out SoundCloud app registration on their own, and run two separate terminal commands. If credentials are missing, the app starts but fails cryptically at login. We want a self-guided, zero-friction first run.

## Changes

### 1. Backend `.env` file support

**New file:** `backend/src/SoundCloudDigger.Api/EnvFileLoader.cs`
- Static helper: reads `.env` from project root, parses `KEY=value` lines (skip `#` comments, blank lines), calls `Environment.SetEnvironmentVariable()` for each
- Keys use `SoundCloud__ClientId` / `SoundCloud__ClientSecret` (ASP.NET Core `__` convention for nested config)

**Modify:** `backend/src/SoundCloudDigger.Api/Program.cs` *(done)*
- Call `EnvFileLoader.Load()` before `WebApplication.CreateBuilder(args)` so env vars are picked up by the config system
- Try CWD first (for `dotnet run`), then fallback to `AppContext.BaseDirectory`-relative (for compiled binary)

**Status:** Program.cs already updated to call `EnvFileLoader.Load()`. `EnvFileLoader.cs` still needs to be created.

---

### 2. `.env.example`

**New file:** `backend/src/SoundCloudDigger.Api/.env.example`

```
# SoundCloud Developer App credentials
# Get these from https://soundcloud.com/you/apps
SoundCloud__ClientId=
SoundCloud__ClientSecret=
```

Short, self-documenting. The `__` separator is the standard ASP.NET Core convention for nested config keys via environment variables.

---

### 3. Backend setup API

**New file:** `backend/src/SoundCloudDigger.Api/Controllers/SetupController.cs`
- `GET /api/setup/status` → returns `{ "configured": true/false }` (checks if both ClientId and ClientSecret are non-empty in config)
- `POST /api/setup/credentials` → accepts `{ "clientId": "...", "clientSecret": "..." }`, writes them to a `.env` file in the project root, sets env vars on the running process, reloads config via `IConfigurationRoot.Reload()`
- No auth required on either endpoint

**Modify:** `backend/src/SoundCloudDigger.Api/Controllers/AuthController.cs` *(done)*
- Guard at top of `Login()`: if `SoundCloud:ClientId` is empty, return 400 with `{ error: "not_configured" }` instead of redirecting to a broken SoundCloud OAuth URL

**Status:** AuthController.cs guard already added. SetupController.cs still needs to be created.

---

### 4. Frontend setup wizard

**New file:** `frontend/src/routes/setup/+page.svelte`
- 3-step wizard (all in one component, reactive `$state`):
  1. **Register App** — step-by-step instructions for creating a SoundCloud developer app:
     - Link to https://soundcloud.com/you/apps
     - Tell user to click "Register a new application"
     - Show the redirect URI to set: `http://localhost:5032/auth/callback` (with copy button)
     - Tell user to copy the Client ID and Client Secret
  2. **Enter Credentials** — input fields for Client ID and Client Secret, "Save & Continue" button calls `POST /api/setup/credentials`
  3. **Done** — success message with "Log in with SoundCloud" link (`/auth/login` with `data-sveltekit-reload`)
- Styled to match existing dark theme (`#111` bg, `#f50` accent, `#1a1a1a` cards, `#999` secondary text, system fonts, scoped CSS)

**Modify:** `frontend/src/lib/api.ts` *(done)*
- Added `checkSetupStatus()` → `GET /api/setup/status`
- Added `saveCredentials(clientId, clientSecret)` → `POST /api/setup/credentials`

**Modify:** `frontend/src/routes/+page.svelte` *(done)*
- On mount, calls `checkSetupStatus()`. If not configured, `goto('/setup')`. Existing login UI gated behind `ready` flag to prevent flash.

**Status:** api.ts and +page.svelte already updated. `setup/+page.svelte` wizard still needs to be created.

---

### 5. Root-level start script

**New file:** `start.sh` (executable)
- Checks for `dotnet`, `node`, `npm` prerequisites with clear error messages
- Runs `dotnet restore` and `npm install`
- Launches backend and frontend as background processes
- `trap 'kill 0' EXIT` for clean Ctrl+C shutdown
- Prints URL to open (`http://localhost:5173`)
- Works on macOS and Linux

---

### 6. Makefile

**New file:** `Makefile`
- `make dev` → runs `./start.sh`
- `make setup` → install deps only (`dotnet restore` + `npm install`)
- `make test` → run all tests (`dotnet test` + `npx vitest run`)
- `make test-backend` / `make test-frontend` → run individually

---

### 7. README update

**Modify:** `README.md`
- Add quick-start section: `./start.sh` → open browser → setup wizard guides you through the rest
- Mention `.env` file as alternative to `dotnet user-secrets`

---

## Summary of all files

### Files to modify
| File | Status |
|------|--------|
| `backend/src/SoundCloudDigger.Api/Program.cs` | Done |
| `backend/src/SoundCloudDigger.Api/Controllers/AuthController.cs` | Done |
| `frontend/src/lib/api.ts` | Done |
| `frontend/src/routes/+page.svelte` | Done |
| `README.md` | TODO |

### New files to create
| File | Status |
|------|--------|
| `backend/src/SoundCloudDigger.Api/EnvFileLoader.cs` | TODO |
| `backend/src/SoundCloudDigger.Api/.env.example` | TODO |
| `backend/src/SoundCloudDigger.Api/Controllers/SetupController.cs` | TODO |
| `frontend/src/routes/setup/+page.svelte` | TODO |
| `start.sh` | TODO |
| `Makefile` | TODO |

## Verification
1. Delete any existing `.env` file, run `./start.sh`
2. Open http://localhost:5173 — should auto-redirect to `/setup`
3. Follow wizard steps, enter credentials, save
4. Verify `.env` file created with correct values
5. Click "Log in with SoundCloud" — should proceed to OAuth flow
6. Run `make test` to ensure no regressions
