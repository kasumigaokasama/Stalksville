# Stalksville Architecture Overview

## Layers

```
Angular 22 SPA (apps/web)
  │  HTTPS + JWT (never sees the Wolvesville API key)
  ▼
Stalksville.Api (ASP.NET Core, /api/v1)
  ▼
Stalksville.Application      orchestration: PlayerService, ClanService, AuthService
  ▼                            │
Stalksville.Intelligence      │  pure engines: SnapshotEngine, ChangeDetector, MembershipTracker
  ▼                            ▼
Stalksville.Domain          Stalksville.Infrastructure
   entities + observed        EF Core stores, Wolvesville read client,
   state models               cache, auditing, seeding
                               ▼
                            PostgreSQL (+ in-memory cache, Redis-ready)
```

Dependency direction: `Api → Application → Intelligence/Domain`, `Infrastructure → Application/Domain` (implements its abstractions).

## Observed vs derived

- `players`, `player_snapshots.payload` = **observed** data (raw, normalized, hash-addressed; source endpoint recorded per snapshot).
- `player_changes`, `clan_memberships`, `relationships`, `timeline_events` = **derived** data. Every row links back to the snapshots / membership records that prove it.
- API contracts mirror this split (`observed` vs `derived` sections in the dossier response), and the UI labels the two differently.

## Snapshots

A snapshot stores a canonicalized JSON serialization of the normalized player state plus its SHA-256 hash. Re-observing an unchanged state only updates `lastObservedAt` and `observationCount` on the latest snapshot — no duplicate rows.

Volatile fields (e.g. `lastOnline`) are recorded but excluded from change detection to avoid noise.

## Wolvesville client

Hand-written typed **read-only** client (no official machine-readable OpenAPI spec is published — checked `openapi.json` and community sources — so the plan's "generate from OpenAPI" approach was replaced by a manually typed client whose every endpoint is traced to the official docs).

Pipeline per request:

```
WolvesvilleClient
  → HostValidationHandler   (https only; blocks loopback/private/link-local/reserved hosts;
                             test hosts allowed only behind Wolvesville:AllowTestHost + Development)
  → Rate limiter            (token bucket, outbound throttle)
  → Logging handler         (api_requests table: endpoint, status, latency)
  → Resilience handler      (retry w/ backoff on 429/5xx, total timeout, circuit breaker)
  → HttpClient → https://api.wolvesville.com  (Authorization: Bot <key>, JSON headers)
```

Endpoints used (all verified against https://api-docs.wolvesville.com/):

| Client method | Endpoint | Notes |
| --- | --- | --- |
| `GetPlayerById` | `GET /players/{playerId}` | full profile |
| `GetPlayerByUsername` | `GET /players/search?username={username}` | exact-match lookup |
| `SearchClans` | `GET /clans/search?name=…` | supports `exactName` etc. |
| `GetClanInfo` | `GET /clans/{clanId}/info` | |
| `GetClanMembers` | `GET /clans/{clanId}/members` | `ClanMember` objects keyed by `playerId` (lighter than full profiles) |
| `Ping` | `GET /roles` | cheap authenticated call for connectivity status |

**No write endpoints are implemented.** The capability model (`GET /api/v1/system/wolvesville/status`) reports write operations as disabled; clan-bot-gated endpoints (chat, announcements, ledger, logs, blocklist, quests) are listed as "requires clan bot" and are not called in this phase.

Verified live against the production API: `/roles` (ping), `/players/search`, `/clans/search`, `/clans/{id}/info` and `/clans/{id}/members` all work with a regular bot key. Real Wolvesville IDs are GUID-like strings (36 chars); identity columns are sized accordingly.

## Authentication

- JWT bearer, HS256, key from configuration (`Auth:JwtKey`) or ephemeral in Development.
- Fallback authorize policy: every `/api/v1` route requires authentication except `/auth/login`.
- Seeded `admin` user (role `ADMIN`); roles `ANALYST`/`VIEWER` gate write vs read-only access.
- Internal rate limiting: fixed window per user, 100 req/min.

## Data model (initial)

`users`, `players`, `player_snapshots`, `player_changes`, `clans`, `clan_memberships`, `relationships`, `evidence`, `timeline_events`, `api_requests`, `audit_logs`, `investigations`, `investigation_targets`, `investigation_notes`.

Wolvesville IDs are stored separately from Stalksville UUIDs (`players.wolvesville_player_id`, `clans.wolvesville_clan_id`) so more data sources can be added later.

## Investigations (phase 3)

Cases (`investigations`, sequential `case_number` displayed as `#0042`) organize already-tracked entities: `investigation_targets` reference players/clans by Stalksville UUID (adding a target requires the entity to be tracked — investigations organize observed data, they never invent entities). The workspace aggregates per-target timelines and computes complexity stats (targets, timeline events, snapshots, high-confidence relationships). Cases are archived rather than deleted; archived cases reject modifications (HTTP 409). The global timeline (`GET /api/v1/timeline`) filters by entity type, event type and observed/derived.

## Graph, advanced intelligence & analytics (phases 4–5)

- **Graph** (`GET /api/v1/graph`, optional `?investigationId=`): nodes are tracked players and clans; edges are the derived relationships with confidence (current memberships solid, historical dashed). Investigation scope expands one hop from the case's targets (clans + co-members). `GET /api/v1/graph/paths?from=&to=` runs in-memory BFS (`Intelligence/Engine/GraphPaths`) to find connection paths between two players through shared clans. `GET /api/v1/graph/analytics` adds deterministic graph measures (`Intelligence/Engine/GraphMetrics`): degree, exact betweenness (Brandes) and communities (label propagation over a fixed node order) — the UI colors node borders by community and lists the top connectors. The canvas renders with Cytoscape — the only new frontend dependency.
- **Progression** (`GET /api/v1/players/{id}/progression`): the observed series (level, wins, games played) projected from the full snapshot history, oldest first. Purely observed data; the dossier's Progression tab charts it with a hand-rolled SVG line component (each dot is one real snapshot).
- **Exposure** (`GET /api/v1/players/{id}/exposure`): deterministic, explainable public-information exposure (plan §15). Five categories (identity, clan, historical, network, profile), each 0–100 with a factor list where every point names its evidence. Computed on demand by `ExposureAnalyzer` — never persisted as an unexplained number.
- **Insights** (`GET /api/v1/players/{id}/insights`): anomaly heuristics over the change history (`InsightGenerator`): membership volatility (≥2 clan changes in 14 days), identity churn (renames), rapid progression, cosmetics momentum — each with confidence and the change-record ids that prove it.
- **Compare** (`GET /api/v1/players/compare?a=&b=`): side-by-side observed fields plus overlap analysis (shared current clan = observed fact 1.0, shared clan history = 0.9, shared badges = weak 0.5) with an explicit "correlation is not proof" disclaimer.
- **Analytics** (`GET /api/v1/analytics/summary`): totals plus per-day series (changes, snapshots, membership joins/leaves) via SQL group-bys. Charts are hand-rolled CSS bars — no charting dependency (deliberate; swap for a charting library if needs grow).

## AI layer (phase 6, optional)

`IAiNarrator` (Application port) has two implementations in Infrastructure. The default **TemplateNarrator** is deterministic and composes the four guardrail sections (Observed / Derived / Hypothesis / Unknown) directly from the case workspace — `InvestigationFacts` (Application, pure) builds those sections and is shared with the LLM path as prompt facts and fallback. The optional **OpenAiCompatibleNarrator** calls any OpenAI-compatible chat-completions API with a strict no-invention system prompt and degrades to the template on any failure. The LLM HTTP client goes through the same HostValidationHandler as the Wolvesville client (outbound security constraint). Endpoint: `POST /api/v1/investigations/{id}/explain`. Narratives are on-demand and never persisted — no AI output is ever stored as data.

## Alerting (expansion phase 1)

`AlertEngine` (Intelligence, pure) evaluates each freshly detected change batch and raises alerts for identity-relevant movement only — clan changes, username renames, level jumps (|Δ| ≥ `Alerts:LevelJumpThreshold`, default 10). Ordinary drift stays in the timeline. Alerts are derived intelligence: every row carries the change ids and snapshot ids that justify it (`evidence` jsonb), and the inbox renders them as deep-links. Dedupe keys on the backing change's snapshot (`kind:entity:snapshotId`), which keeps ingestion idempotent without suppressing real same-day oscillation — smart snapshots already make identical re-observations produce no changes. Wired into `PlayerService.ApplyObservationAsync` so manual refreshes and worker cycles alert identically; read state is global (single-workbench semantics). API: `GET /api/v1/alerts` (+ `unread-count`, `POST /{id}/read`, `POST /read-all`); UI: `/alerts` inbox with a polled unread badge in the shell. Rate limiting is relaxed to 1000 req/min in Development (the serial e2e suite legitimately bursts).

## Highscores (expansion phase 3)

`GET /players/highscores` on the Wolvesville side returns the four top-100 XP boards (`allTime`/`monthly`/`weekly`/`daily`, spec schema `PlayerRank`). `HighscoreService.CaptureAsync` stores a full capture (append-only `highscore_entries` rows; captures are strictly time-ordered so "previous capture" is unambiguous), resolves tracked players by username for dossier deep-links, and derives rank shifts for tracked players by diffing consecutive captures of the same period: |Δrank| ≥ `Alerts:RankShiftThreshold` (default 10) raises a `RankShift` alert whose evidence names both captures, plus a derived `HighscoreRankChanged` timeline event. API: `GET /api/v1/highscores?period=` and `POST /highscores/capture` (analyst). The worker captures at most once per `Worker:HighscoreCaptureIntervalHours` (default 24). UI: `/highscores` leaderboard with period switcher, tracked badges and one-click "Track" that imports a player straight from the board. Clan-bot-gated endpoints (chat, announcements, ledger, logs, quests) remain uncalled: the capability report models them as RequiresClanBot, and implementing them needs a clan-bot key to verify against.

## Collaboration, search & exports (expansion phase 4)

- **Workspace search** (`GET /api/v1/search?q=`): identifier substring match for tracked players and clans, PostgreSQL FTS (`to_tsvector('simple', …)` with GIN indexes) over active investigation titles, descriptions and notes — note hits join back to their case. The Ctrl+K palette searches the whole workspace; the `player:` prefix keeps its live Wolvesville lookup behavior. LIKE metacharacters in user input are escaped so queries can't widen the match.
- **Case collaboration**: `AssignedToUserId` + `Tags` (jsonb, normalized to trimmed lowercase, max 12) on investigations, edited via `PATCH /api/v1/investigations/{id}` (partial semantics; an explicit null with `assigneeProvided` clears the assignee). `GET /api/v1/investigations/assignees` lists assignable analysts/admins for the workspace dropdown. Every change is audit-logged (`INVESTIGATION_UPDATED`).
- **Print/PDF export**: `export?format=html` returns a standalone print-optimized HTML report (the Markdown report with print CSS and an auto-invoked print dialog → browser "Save as PDF"). Zero PDF dependencies by design.
- **Light mode**: `[data-theme="light"]` token overrides in the design system (dark stays the default), toggled from the shell and persisted in localStorage. The observed/derived hues keep their sky/violet identity in both themes.

## Operations (phase 7)

- **RBAC**: JWT role claims (short names; inbound claim mapping disabled) drive two policies — `analyst` (ANALYST/ADMIN) guards all mutating endpoints (lookup, refresh, import, case editing), `admin` (ADMIN) guards user management (`/api/v1/admin/users`). VIEWER is read-only including exports and explanations. Users are created via the admin API, never deleted.
- **Exports**: `GET /api/v1/investigations/{id}/export?format=md|csv|json` — Markdown report (targets, timeline, relationships, notes, confidence & limitations), CSV timeline, JSON workspace. Reports always carry the disclaimer.
- **Write operations**: not implemented, by design (repository rules). `Wolvesville:EnableWriteOperations` is scaffolding surfaced in the capability report; enabling them is a future product decision.
- **Worker** (`apps/worker/Stalksville.Worker`): a .NET background service running the adaptive refresh loop — `RefreshScheduler` (pure) picks the least-recently observed tracked players per cycle (skip-below-min-interval, capped per run) and pushes them through `PlayerService.RefreshAsync`. Shares the Infrastructure stack (same DB, same client, same provenance/audit).

## Repository layout

```
apps/
  web/        Angular 22 SPA
  api/        .NET solution: Api, Application, Domain, Infrastructure, Intelligence, Worker, UnitTests, IntegrationTests
docs/         architecture overview, design system
docker-compose.yml   local PostgreSQL
```

## Testing

- **Unit** (`Stalksville.UnitTests`): change detection, snapshot hashing/dedup, membership transitions — pure engine tests.
- **Integration** (`Stalksville.IntegrationTests`): `MockWolvesvilleServer` (an in-process ASP.NET server mirroring the real API's paths, auth header rules, and failure modes) drives the full HTTP pipeline of the real API app via `WebApplicationFactory`, against a throwaway Postgres database. No dependency on the live Wolvesville API.
- **E2E** (Playwright): login → player lookup → dossier → changes, running the backend in `Wolvesville:Mode=Mock`.
