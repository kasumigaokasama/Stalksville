import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { defineConfig } from '@playwright/test';

/**
 * E2E smoke tests run the full stack: the API in Wolvesville:Mode=Mock (built-in demo dataset,
 * no API key needed) plus ng serve, against a dedicated throwaway database.
 * Requires `docker compose up -d` (Postgres) beforehand.
 */

// Playwright evaluates this config in more than one process, so the random password is
// persisted to a temp file to keep every evaluation (API env + specs) on the same value —
// and stable across runs, matching the persistent e2e database's seeded admin.
// It is never a static credential in source; STALKSVILLE_E2E_ADMIN_PASSWORD overrides it.
// To reset the e2e environment: delete the temp file and run
//   docker exec stalksville-postgres psql -U stalksville -d postgres \
//     -c "DROP DATABASE stalksville_e2e WITH (FORCE)"
const passwordFile = join(tmpdir(), 'stalksville-e2e-admin-password');
let e2eAdminPassword: string;
if (process.env.STALKSVILLE_E2E_ADMIN_PASSWORD) {
  e2eAdminPassword = process.env.STALKSVILLE_E2E_ADMIN_PASSWORD;
} else if (existsSync(passwordFile)) {
  e2eAdminPassword = readFileSync(passwordFile, 'utf8');
} else {
  e2eAdminPassword = `e2e-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
  writeFileSync(passwordFile, e2eAdminPassword);
}
process.env.E2E_ADMIN_PASSWORD = e2eAdminPassword;

export default defineConfig({
  testDir: './e2e',
  timeout: 45_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  // One worker: specs share a persistent database and one mock Wolvesville whose data mutates
  // per fetch, so parallel files race each other (e.g. two specs refreshing the same player).
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'off',
  },
  // NOTE: Playwright starts webServers BEFORE globalSetup, so a DB reset in globalSetup would
  // kill the freshly-seeded database. The e2e database is therefore persistent; see the
  // password note above for the manual reset procedure.
  webServer: [
    {
      // Run the built DLL directly (dotnet build first): `dotnet run` would nest the app as a
      // child process, which Playwright cannot tear down reliably on Windows.
      command: 'dotnet ../api/Stalksville.Api/bin/Debug/net10.0/Stalksville.Api.dll',
      url: 'http://localhost:5099/api/v1/auth/login',
      reuseExistingServer: false,
      timeout: 120_000,
      stdout: 'pipe',
      // Spread process.env: Playwright's webServer env replaces the environment rather than
      // merging, and dotnet needs PATH etc.
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: 'http://localhost:5099',
        WOLVESVILLE__MODE: 'Mock',
        CONNECTIONSTRINGS__DATABASE:
          'Host=localhost;Port=5432;Database=stalksville_e2e;Username=stalksville;Password=stalksville',
        AUTH__ADMINPASSWORD: e2eAdminPassword,
      },
    },
    {
      command: 'npx ng serve --port 4200',
      url: 'http://localhost:4200',
      reuseExistingServer: true,
      timeout: 180_000,
    },
  ],
});
