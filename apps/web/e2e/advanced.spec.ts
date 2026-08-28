import { expect, test } from '@playwright/test';

const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? '';

async function login(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });
}

/** Imports a player through the palette — specs must not rely on state from earlier runs (CI starts fresh). */
async function importPlayer(page: import('@playwright/test').Page, name: string): Promise<void> {
  await page.keyboard.press('Control+k');
  const palette = page.getByRole('dialog', { name: 'Command palette' });
  await palette.getByPlaceholder(/Search…/).fill(`player:${name}`);
  await expect(palette.getByRole('button', { name: /open dossier/ })).toBeVisible({ timeout: 15_000 });
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: name, exact: true })).toBeVisible({ timeout: 15_000 });
}

test.describe('Advanced intelligence (mock Wolvesville)', () => {
  test('graph page renders nodes, edges and connection paths', async ({ page }) => {
    await login(page);

    // Two clanmates are needed for a deterministic connection path on a fresh database.
    await importPlayer(page, 'nightowl');
    await importPlayer(page, 'talon');

    await page.getByRole('link', { name: 'Graph' }).click();
    await expect(page.getByRole('heading', { name: 'Graph' })).toBeVisible();
    await expect(page.getByText(/nodes ·/)).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('.canvas canvas').first()).toBeVisible();
    await expect(page.getByText('MEMBER_OF (current)').first()).toBeVisible(); // legend

    // Analytics panel: derived connectors and community count.
    await expect(page.getByRole('heading', { name: 'Analytics' })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('columnheader', { name: 'Betweenness' })).toBeVisible();
    await expect(page.locator('.side table a').first()).toBeVisible();

    // Path finding between two clanmates (both fixed in Moon Wolves).
    await page.locator('.path-finder select').nth(0).selectOption({ label: 'nightowl' });
    await page.locator('.path-finder select').nth(1).selectOption({ label: 'talon' });
    await expect(page.getByRole('heading', { name: 'Connection paths' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.path').first()).toContainText('nightowl');
    await expect(page.locator('.path').first()).toContainText('talon');
  });

  test('dossier Intelligence tab shows explainable exposure and insights', async ({ page }) => {
    test.setTimeout(90_000); // import + two scripted refreshes before the assertions
    await login(page);

    await importPlayer(page, 'shadowfox');

    // The mock drift toggles shadowfox's clan on every fetch: two refreshes produce the two
    // clan changes the volatility insight needs (a fresh database starts at zero changes).
    for (let i = 0; i < 2; i++) {
      await page.getByRole('button', { name: 'Refresh from Wolvesville' }).click();
      await expect(page.getByText(/change\(s\) detected in this observation/)).toBeVisible({ timeout: 20_000 });
    }

    await page.getByRole('tab', { name: /Intelligence/ }).click();
    await expect(page.getByText('Exposure score')).toBeVisible();
    await expect(page.locator('.exposure-total').first()).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText('Identity surface', { exact: true })).toBeVisible();
    await expect(page.getByText('Why? — every point and its evidence')).toBeVisible();

    // shadowfox has scripted clan switches → volatility insight with evidence.
    await expect(page.getByText('Membership volatility').first()).toBeVisible();
  });

  test('dossier Progression tab charts the observed snapshot series', async ({ page }) => {
    await login(page);

    await page.keyboard.press('Control+k');
    await page.getByPlaceholder(/Search…/).fill('player:shadowfox');
    await page.getByRole('button', { name: /open dossier/ }).click();
    await expect(page.getByRole('heading', { name: 'shadowfox', exact: true })).toBeVisible();

    // A refresh with the mock drift appends an observation, then the chart renders the series.
    await page.getByRole('button', { name: 'Refresh from Wolvesville' }).click();
    await expect(page.getByText(/change\(s\) detected in this observation/)).toBeVisible({ timeout: 20_000 });

    await page.getByRole('tab', { name: 'Progression' }).click();
    await expect(page.getByText('Level, wins and games played across every stored snapshot')).toBeVisible();
    await expect(page.locator('stl-progression-chart svg polyline').first()).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText('Wins', { exact: true })).toBeVisible(); // legend
  });

  test('analytics page renders totals and series charts', async ({ page }) => {
    await login(page);

    await page.getByRole('link', { name: 'Analytics' }).click();
    await expect(page.getByRole('heading', { name: 'Analytics' })).toBeVisible();
    await expect(page.getByText('Players tracked')).toBeVisible();
    await expect(page.getByText('Changes detected').first()).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.bars .bar').first()).toBeVisible();
  });

  test('compare page shows fields and shared-clan overlap', async ({ page }) => {
    await login(page);

    await importPlayer(page, 'nightowl');
    await importPlayer(page, 'talon');

    await page.goto('/players/compare');
    await expect(page.getByRole('heading', { name: 'Compare players' })).toBeVisible();

    await page.locator('.pickers select').nth(0).selectOption({ label: 'nightowl' });
    await page.locator('.pickers select').nth(1).selectOption({ label: 'talon' });

    await expect(page.getByText('Potential overlaps')).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText('Shared current clan').first()).toBeVisible();
    await expect(page.getByText(/Correlation is not proof/)).toBeVisible();
    await expect(page.locator('.compare-table tr').first()).toBeVisible();
  });
});
