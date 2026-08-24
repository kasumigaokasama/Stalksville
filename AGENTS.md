Project: Stalksville

Purpose:
Wolvesville Intelligence Workbench.

Frontend:
Angular 22
TypeScript
SCSS
Angular CDK
Angular official agent skills

Backend:
.NET 10
ASP.NET Core
Entity Framework Core

Database:
PostgreSQL

Cache:
Redis

Testing:
Playwright
xUnit

Architecture:
Feature-first Angular architecture.
Clean Architecture / layered backend.
API keys must never be exposed to the frontend.

Rules:
- Prefer modern Angular APIs and patterns.
- Use standalone components.
- Prefer Signals for local/reactive state.
- Keep features independently structured.
- Do not introduce unnecessary dependencies.
- Do not bypass the backend for Wolvesville API calls.
- Do not implement destructive Wolvesville API operations unless explicitly requested.
- Do not invent Wolvesville endpoints; verify against the official API specification.
- Keep observed API data separate from derived intelligence.
- Every derived intelligence result must have traceable evidence.
- Do not make unrelated refactors.
- Before major architectural changes, explain the impact.