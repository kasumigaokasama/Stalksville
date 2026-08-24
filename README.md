# Stalksville

**Wolvesville Intelligence Workbench** — turns the [Wolvesville public API](https://api-docs.wolvesville.com/) into a persistent, searchable, explainable intelligence workspace.

Stalksville distinguishes three kinds of information everywhere — in the database, in the API contracts, and in the UI:

- **Observed** — data directly returned by Wolvesville.
- **Derived** — conclusions calculated by Stalksville (changes, relationships). Every derived result carries traceable evidence.
- **Hypothesis** — possible connections that are not yet sufficiently supported. (Not produced by the deterministic engine in this phase.)

## Stack

| Layer | Technology |
| --- | --- |
| Frontend | Angular 22 (standalone components, signals, SCSS, Angular CDK) |
| Backend | ASP.NET Core (.NET 10), Clean Architecture |
| Database | PostgreSQL (EF Core 10, migrations) |
| Cache | In-memory behind `ICacheProvider` (Redis-ready) |
| Testing | xUnit, vitest, Playwright |

Current feature set: dashboard, player dossier (snapshots/changes/evidence + **Intelligence tab** with explainable exposure and anomaly insights), clan search & import, **investigations (cases, targets, notes, aggregated timeline, AI case explanation, Markdown/CSV/JSON exports)**, global timeline with filters, **relationship graph** (Cytoscape, confidence edges, connection-path finder), **analytics**, **player compare with overlap analysis**, **role-based access control** (ADMIN / ANALYST / VIEWER, with admin user management), settings/connection report, Ctrl+K command palette, and an **adaptive-refresh background worker** (`apps/worker`).

### AI narration (optional by design)

Case explanations work out of the box with a deterministic narrator that composes the Observed / Derived / Hypothesis / Unknown sections directly from the evidence — no LLM required. To upgrade to an LLM, configure any OpenAI-compatible API:

```bash
dotnet user-secrets set "Ai:Provider" "OpenAiCompatible"
dotnet user-secrets set "Ai:ApiKey" "<key>"
dotnet user-secrets set "Ai:BaseUrl" "https://api.openai.com/v1"
dotnet user-secrets set "Ai:Model" "gpt-4o-mini"
```

The LLM receives only case facts/evidence/confidence, is instructed never to invent facts, and any failure falls back to the deterministic narrator.

### Roles

| Role | Capabilities |
| --- | --- |
| ADMIN | everything + user management (`/api/v1/admin/users`) |
| ANALYST | imports, refreshes, case editing |
| VIEWER | read-only (dossiers, graph, timeline, analytics, exports, case explanations) |

The seeded `admin` creates other users via the API. Write operations against Wolvesville are **not implemented**; `Wolvesville:EnableWriteOperations` exists as scaffolding only.

### Background worker

```bash
dotnet run --project apps/api/Stalksville.Worker
```

Every 15 minutes (configurable via `Worker:*`) it re-refreshes the least-recently-observed tracked players through the full pipeline — snapshots, change detection and memberships keep accumulating without anyone clicking. In mock mode this makes the demo dataset evolve on its own.

## Repository layout

```
apps/
  web/                    Angular 22 SPA
  api/                    .NET solution (Api, Application, Domain, Infrastructure, Intelligence, tests)
docs/architecture/        architecture notes
docker-compose.yml        local PostgreSQL
```

## Getting started

Prerequisites: Node 24+, .NET SDK 10, Docker.

```bash
# 1. Database
docker compose up -d

# 2. Backend (http://localhost:5099)
cd apps/api
dotnet tool restore                       # dotnet-ef (local tool)
dotnet ef database update \
  --project Stalksville.Infrastructure --startup-project Stalksville.Api
dotnet run --project Stalksville.Api

# 3. Frontend (http://localhost:4200, proxies /api to the backend)
cd apps/web
npm install
npm start
```

### Configuration (backend secrets)

Never commit secrets. Use user-secrets or environment variables:

```bash
cd apps/api/Stalksville.Api
dotnet user-secrets init
dotnet user-secrets set "Wolvesville:ApiKey" "<your wolvesville bot api key>"
dotnet user-secrets set "Auth:AdminPassword" "<choose an admin password>"
```

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Database` | local docker Postgres | PostgreSQL connection |
| `Wolvesville:Mode` | `Real` | `Real` or `Mock` (demo without an API key) |
| `Wolvesville:BaseUrl` | `https://api.wolvesville.com` | Upstream API base |
| `Wolvesville:ApiKey` | — | Bot API key (**backend only**) |
| `Auth:AdminPassword` | generated + logged once | Password of the seeded `admin` user |

The Wolvesville API key is used exclusively server-side; it is never exposed to the Angular app.

### Mock mode

`Wolvesville:Mode=Mock` replaces the upstream client with an in-process demo dataset (no API key required), including a scripted clan change so change detection can be demonstrated offline. The connection status page clearly labels mock mode.

## Testing

```bash
# Backend (requires docker compose up -d; integration tests create throwaway databases)
cd apps/api
dotnet test

# Frontend unit tests
cd apps/web
npm test

# E2E (full stack in mock mode; builds must be current: dotnet build + ng build)
cd apps/web
dotnet build ../api
npx playwright test
```

E2E notes: Playwright boots the API (mock mode, dedicated `stalksville_e2e` database) and `ng serve` itself. The admin password for the E2E database is generated randomly and kept in a temp file so repeated runs stay consistent; see `apps/web/playwright.config.ts` for the reset procedure.

## Live-API behavior (verified against api.wolvesville.com)

- Real Wolvesville IDs are GUID-like strings — stored as strings, never conflated with Stalksville UUIDs.
- `GET /clans/search` and `GET /clans/{id}/info` are public and work with any bot key.
- `GET /clans/{id}/members` requires the bot to be a **clan bot of that clan** (returns no members otherwise). Stalksville's capability model reflects this; the connection page lists it as "requires clan bot".
- Upstream 404/429/5xx are retried and mapped to clean ProblemDetails responses.

## Core loop

```
SEARCH → DISCOVER → IMPORT → SNAPSHOT → COMPARE → CORRELATE → INVESTIGATE
```

1. Look up a player by exact username (`/players/lookup`).
2. Stalksville normalizes the profile, stores a hash-addressed snapshot.
3. Re-observation of an identical state only bumps `observationCount` (smart snapshotting).
4. Any difference produces field-level change records, timeline events, and membership transitions — each with evidence references to the snapshots that prove it.
5. Importing a clan snapshots every member and maintains membership history + `MEMBER_OF` relationships.
6. **Investigations** organize tracked players/clans into cases: targets, analyst notes, an aggregated per-case timeline and complexity stats. Cases are archived, never deleted; archived cases are read-only.

See [docs/architecture/overview.md](docs/architecture/overview.md) for the architecture notes and [docs/design-system.md](docs/design-system.md) for the UI colour/style semantics (observed vs derived).

## Safety rules

- **No Wolvesville write operations are implemented.** Read-only endpoints only.
- Outbound HTTP is restricted to `https://` and non-private, non-loopback hosts.
- All API routes require JWT authentication (seeded `admin` user).
- Every derived intelligence result stores evidence: source endpoint, snapshot hashes, capture time.

See [docs/architecture/overview.md](docs/architecture/overview.md) for the architecture notes.
