# Stalksville — User Guide

How to run and use the Wolvesville Intelligence Workbench. For architecture notes see
[docs/architecture/overview.md](architecture/overview.md); for developer workflows see the
[README](../README.md).

---

## What Stalksville does

Stalksville turns the public [Wolvesville API](https://api-docs.wolvesville.com/) into a
persistent, searchable, explainable intelligence workspace. You import players and clans,
the workbench keeps snapshotting them in the background, and it derives changes,
relationships, exposure scores and alerts from what it observed — always with evidence.

Everything you see is labelled by what it is:

| Label | Meaning |
| --- | --- |
| **Observed** | Exactly what Wolvesville returned, stored as immutable snapshots. |
| **Derived** | Calculated by Stalksville (changes, memberships, exposure, insights, alerts). Every derived result carries the evidence that produced it — which snapshots, which fields, when. |
| **Hypothesis** | Possible connections not yet supported by evidence (reserved; the current engine is deterministic and produces none). |

---

## Getting started

### Option A — full stack in Docker (recommended)

Prerequisites: Docker.

1. Create a `.env` file next to `docker-compose.yml` (it is gitignored):

   ```env
   WOLVESVILLE_APIKEY=your-wolvesville-bot-key
   STALKSVILLE_ADMIN_PASSWORD=choose-an-admin-password
   STALKSVILLE_JWT_KEY=any-long-random-string
   # Optional: set WOLVESVILLE_MODE=Mock to try Stalksville without a key
   ```

2. Start everything:

   ```bash
   docker compose --profile app up -d --build
   ```

3. Open **http://localhost:4200** and log in as `admin` with your
   `STALKSVILLE_ADMIN_PASSWORD`.

The stack runs PostgreSQL, Redis, the API (port 5099), the background worker and the web
app. `docker compose logs -f worker` shows what the worker is doing.

### Option B — development mode

```bash
docker compose up -d            # PostgreSQL only

cd apps/api
dotnet tool restore
dotnet ef database update --project Stalksville.Infrastructure --startup-project Stalksville.Api
dotnet user-secrets init --project Stalksville.Api
dotnet user-secrets set "Wolvesville:ApiKey" "<key>" --project Stalksville.Api
dotnet user-secrets set "Auth:AdminPassword" "<password>" --project Stalksville.Api
dotnet run --project Stalksville.Api       # API on http://localhost:5099

cd ../web
npm install
npm start                                  # web on http://localhost:4200
```

The admin password is only seeded when the database is empty; if you leave it unset, a
random one is generated and logged once by the API on first boot. Change it later from the
Admin console.

### Mock mode (no API key needed)

`WOLVESVILLE_MODE=Mock` (or `Wolvesville:Mode=Mock` in user-secrets) replaces the upstream
client with an evolving demo dataset, including a scripted clan change so change detection
can be demonstrated offline. The Settings page clearly labels mock mode.

---

## Core concepts

- **Tracked player / clan** — an entity Stalksville keeps observing. Import once; the
  worker re-observes it forever.
- **Snapshot** — the full observed state of a player at one point in time. Re-observing an
  unchanged player only bumps its observation count (*smart snapshotting*); a changed
  player gets a new snapshot.
- **Change** — a field-level difference between two snapshots (clan, username, level,
  badges, friends …), stored with the exact evidence pair that proves it.
- **Timeline** — the chronological stream of observations and derived events.
- **Relationship** — a derived edge between entities (clan membership, friend links).
- **Alert** — an identity-relevant derived event routed to the Alerts inbox. Read state is
  **per user**: marking an alert read does not affect anyone else's inbox.
- **Watchlist** — your personal starred players (☆ in the dossier header). Watched players
  are refreshed first by the background worker.
- **Investigation** — a case file that groups targets, notes and an aggregated timeline.

---

## A tour of the app

Navigation lives in the sidebar (a drawer on mobile screens). Press **Ctrl+K** anywhere for
the command palette. The **☀/☾** button in the top bar toggles the light/dark theme.

### Overview (dashboard)

Landing page: current counts, recently seen players, and the latest derived intelligence
with links straight into the evidence.

### Players

- **Look up & import** — enter an exact Wolvesville username; the profile is fetched,
  normalized and snapshotted. Importing a clan member also records the membership.
- **Watched only** — filter the list to your personal watchlist.
- **Compare** (`/players/compare`) — pick two tracked players to see their observed states
  side by side plus derived overlap analysis (shared clans, mutual friends).

### Player dossier

Every tracked player gets a dossier with tabs **Overview · Identity · Clans · Progression ·
Snapshots · Changes · Intelligence** (arrow keys navigate the tabs).

Header actions:

- **Refresh from Wolvesville** (Analyst/Admin) — observe the player right now; the
  Intelligence tab reloads with the fresh assessment.
- **☆ Watch** (everyone) — add/remove the player from your watchlist.
- **Erase…** (Admin) — permanently removes the player's stored data; a reason of at least
  4 characters is required and recorded in the audit log.

Tab highlights:

- **Identity** — observed profile fields (personal message, badges with catalog names,
  profile icon, avatar).
- **Clans** — membership history derived from snapshots, current and ended periods.
- **Progression** — wins/losses/games observed over time.
- **Snapshots** — raw observed states with payload hashes and capture times.
- **Changes** — every detected field change with its evidence (from/to snapshot).
- **Intelligence** — derived, explainable analysis:
  - **Exposure score** (0–100): how much surface the player's *public* data exposes, broken
    into categories (identity, clan, historical, network, profile) where every point lists
    its evidence. It never claims anything about real-world identity.
  - **Insights**: evidence-backed observations such as win-rate trends, with the evidence
    links that justify each one.

### Highscores

Wolvesville's top-100 XP boards for **all-time / monthly / weekly / daily**. **Capture
now** (Analyst/Admin) stores a board; tracked players moving on the board raise
**rank-shift** alerts. **Track** imports an untracked player straight from the board.

### Ranked

Two views, toggled at the top:

- **Leaderboard** — the current ranked season's ladder with skill ratings. Captures are
  stored per capture; tracked players shifting rank raise alerts.
- **Hall of fame** — season winners with their avatars; a season selector lists every
  captured season. The worker captures each finished season automatically; a tracked
  player appearing among the winners raises a one-time alert.

### Clans

Search Wolvesville clans by name, inspect a clan (member list, description), and import a
clan to snapshot every member and maintain membership history. **Note:** the member list
endpoint requires your bot key to be a *clan bot* of that clan — the Settings page shows
this capability; without it, clan imports still work but members can only be added by
importing players individually.

### Investigations

Cases organize your work:

- Create a case, add **targets** (tracked players/clans), write **notes**, apply **tags**,
  assign an owner, and read the **case timeline** — every observation and derived event of
  all targets aggregated into one stream.
- **Explain case** produces an Observed / Derived / Hypothesis / Unknown narrative. The
  default narrator is deterministic and works offline; an optional LLM can be configured
  (see README) and only ever receives case facts, never invents them.
- **Export** a case as Markdown, CSV, JSON or a print-ready page.
- Cases are **archived, never deleted**; archived cases are read-only.

### Graph

The relationship network (players, clans, friend links, memberships) with a **connection
path finder** — pick two players and Stalksville shows the shortest relationship path
between them, with the evidence for each hop.

### Timeline

The global event stream with filters (player, clan, kind, date) and load-more paging.

### Alerts

Your inbox of identity-relevant events: clan changes, renames, level jumps, rank shifts,
new friend links, exposure shifts, hall-of-fame entries. Unread count shows in the
sidebar badge. **Mark read / mark all read** affects only *your* inbox. Every alert
deep-links to the exact evidence that raised it.

| Alert kind | Raised when |
| --- | --- |
| Clan changed | A tracked player's clan changed between snapshots. |
| Renamed | A tracked player's username changed. |
| Level jump | A player's level moved ≥ 10 in one observation window (configurable). |
| Rank shift | A tracked player moved on the XP highscore board. |
| Friend link added | A new friend connection appeared between tracked players. |
| Exposure shift | A player's exposure score changed by a configured amount. |
| Hall of fame entry | A tracked player appears among a season's ranked winners (once per season). |

### Analytics

Charts over your corpus: activity, derived relationship counts, board series, and more —
all labelled observed vs derived.

### Scheduled scans (Admin)

Automate the scanning itself: define **schedules** that re-observe your corpus on a cadence
and get **webhook notifications only when something actually changed**.

- **Schedules** — pick a kind (*Player refresh* re-observes tracked players; *Highscore
  capture* stores the XP boards), which players to scan (**All tracked players**, only
  **Watched** players, or a hand-picked **selection** via the player picker), an interval
  (5 minutes to a week) and a per-run player batch size. A new schedule is due immediately;
  then the background worker runs it on its interval. **Run now** executes immediately
  (manual runs skip the per-player refresh floor), **Pause/Resume** silences a schedule
  without losing its history, and every run lands in the **Run history** with observed,
  change and alert counts plus which players changed what.
- **Change notifications** — add webhook channels (Discord webhook URLs work out of the
  box; the payload is Discord-compatible JSON usable by any receiver). A run that detects
  changes or raises alerts POSTs a summary to every enabled channel; silent runs send
  nothing. **Test** verifies delivery, and each channel shows its last delivery outcome.
  Webhook URLs are credentials — they are stored server-side only and shown masked.
  Webhooks must be `https://` and cannot target private/loopback hosts.

### Settings

The Wolvesville **connection report**: mode (Real/Mock), upstream reachability, cache state
and the bot's capabilities (e.g. whether clan-member listings are available). No secrets
are ever shown — the API key stays in the backend.

### Admin (Admin role only)

- **Users** — create users, set roles (Admin / Analyst / Viewer), deactivate accounts.
- **API keys** — issue client keys for programmatic API access (sent as `X-Api-Key`,
  stored as SHA-256 hashes, carrying the owning user's role). The full key is shown
  exactly once at creation.
- **Audit log** — who did what, when, with load-more paging.

---

## Roles

| Role | Can |
| --- | --- |
| **ADMIN** | Everything, plus the Admin console and data erasure. |
| **ANALYST** | Import/refresh players and clans, capture boards, edit cases. |
| **VIEWER** | Read everything: dossiers, graph, timeline, analytics, exports, case explanations — plus their own watchlist and alert reads. |

Anyone can star a watchlist — it is personal bookkeeping and does not grant write access to
intelligence data.

---

## The background worker

The worker (`apps/worker`, started automatically in the Docker stack) keeps the corpus
fresh without anyone clicking:

- **Adaptive refresh** — every 15 minutes (configurable via `Worker:*`) it re-observes the
  least-recently-seen tracked players through the full pipeline. **Watched players are
  refreshed first.**
- **Scheduled scans** — every 30 seconds (`Worker:ScanTickSeconds`) the worker checks for
  due admin-defined schedules and executes them; runs with detected changes dispatch the
  webhook notifications described above.
- **Board captures** — XP highscores and the ranked leaderboard daily (24 h default); the
  ranked **hall of fame** once per finished season; cosmetics catalogs refreshed daily so
  badge/profile-icon names resolve.
- **Snapshot retention** — keeps the newest snapshot per player plus everything younger
  than 90 days (`Worker:SnapshotRetentionDays`), so history stays useful without growing
  forever.

---

## Safety rules built into the app

- Stalksville is **read-only toward Wolvesville** — no write operations are implemented.
- Your Wolvesville API key lives **only** in the backend; the frontend never sees it.
- Outbound HTTP is restricted to public `https://` hosts.
- Every derived result stores its evidence (source endpoint, snapshot hashes, capture
  time) — if Stalksville claims it, it can show you why.

---

## Troubleshooting

| Symptom | Explanation / fix |
| --- | --- |
| Ranked leaderboard is empty or capture fails | The upstream ranked endpoint intermittently returns errors on Wolvesville's side. The worker retries automatically; **Capture now** retries immediately. Hall of fame and other pages are unaffected. |
| A clan's members are empty | Listing members requires a **clan-bot key** for that clan (a Wolvesville restriction, shown in Settings → Capabilities). |
| No friend links appear | Wolvesville often does not expose friend ids in profiles; the friend network activates automatically when the data appears. |
| Forgot the admin password | Passwords seed only on an empty database. Have another admin reset it in the Admin console, or reset your database: `docker compose down -v` (⚠ destroys all stored data) and start fresh with a new `STALKSVILLE_ADMIN_PASSWORD`. |
| Port 4200 or 5099 already in use | Something else is listening; stop it or map different ports in `docker-compose.yml`. |
| Where is the API key shown? | Nowhere — by design. Rotate it in the Wolvesville developer portal and update `.env` / user-secrets. |

---

## Keyboard shortcuts

| Key | Action |
| --- | --- |
| `Ctrl+K` | Open the command palette (navigate anywhere, search actions). |
| `←` / `→` / `Home` / `End` | Move between dossier tabs (roving focus). |
| `Esc` | Close dialogs / the palette. |
