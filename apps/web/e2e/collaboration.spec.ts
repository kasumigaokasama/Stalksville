import { expect, test } from '@playwright/test';

const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? '';

async function login(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });
}

test.describe('Collaboration & UX (expansion round 2)', () => {
  test('admin console: create user, issue shown-once key, audit log lists entries', async ({ page }) => {
    await login(page);

    await page.getByRole('link', { name: 'Admin' }).click();
    await expect(page.getByRole('heading', { name: 'Admin console' })).toBeVisible();

    // The seeded admin appears in the user table.
    await expect(page.locator('.stl-table').first()).toContainText('admin');

    // Issue an API key for the admin user — the full key is shown exactly once.
    await page.locator('.stl-table tbody tr', { hasText: 'admin' }).getByRole('button', { name: 'API keys' }).click();
    await page.getByPlaceholder('ci-integration').fill('e2e-key');
    await page.getByRole('button', { name: 'Issue key' }).click();
    await expect(page.locator('.issued-key code')).toContainText(/^stv_/);

    // The audit log records the sensitive operations.
    await expect(page.locator('.stl-table').last()).toContainText(/API_KEY|USER_/, { timeout: 15_000 });
  });

  test('command palette navigates with the keyboard', async ({ page }) => {
    await login(page);

    await page.keyboard.press('Control+K');
    const palette = page.getByRole('dialog', { name: 'Command palette' });
    await expect(palette).toBeVisible();

    // Arrow selection moves through the listbox and Enter opens the focused result.
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await expect(palette.getByRole('option').nth(2)).toHaveAttribute('aria-selected', 'true');
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'Highscores' })).toBeVisible();
  });

  test('investigation title is editable inline and links to the ego graph', async ({ page }) => {
    await login(page);

    // Create a scratch case for this run.
    await page.getByRole('link', { name: 'Investigations' }).click();
    await page.getByPlaceholder('e.g. Moon Wolves membership churn').fill('UX round 2 scratch case');
    await page.getByRole('button', { name: 'Create case' }).click();
    await expect(page.getByRole('heading', { name: /UX round 2 scratch case/ })).toBeVisible({ timeout: 15_000 });

    // Inline editing through the PATCH endpoint.
    await page.getByRole('button', { name: 'Edit title and description' }).click();
    const title = page.locator('.title-edit input');
    await title.fill('UX round 2 renamed case');
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByRole('heading', { name: /UX round 2 renamed case/ })).toBeVisible({ timeout: 15_000 });

    // The ego graph link scopes /graph to this investigation.
    await page.getByRole('link', { name: 'Open ego graph' }).click();
    await expect(page.getByRole('heading', { name: 'Graph' })).toBeVisible();
    await expect(page).toHaveURL(/graph\?investigationId=/);
  });

  test('mobile viewport collapses the sidebar into a drawer', async ({ page }) => {
    await page.setViewportSize({ width: 480, height: 800 });
    await login(page);

    const menu = page.getByRole('button', { name: 'Toggle navigation menu' });
    await expect(menu).toBeVisible();
    await menu.click();

    const nav = page.getByRole('link', { name: 'Players' });
    await expect(nav).toBeVisible();
    await nav.click();
    await expect(page.getByRole('heading', { name: 'Players' })).toBeVisible();
    // Navigating from the drawer closes it again.
    await expect(menu).toHaveAttribute('aria-expanded', 'false');
  });

  test('watchlist: star a player from the dossier and filter by watched', async ({ page }) => {
    test.setTimeout(90_000); // palette import + star + filter round-trip on a cold runner
    await login(page);

    // Open a tracked player's dossier (the palette import makes this self-sufficient).
    await page.keyboard.press('Control+k');
    const palette = page.getByRole('dialog', { name: 'Command palette' });
    await palette.getByPlaceholder(/Search…/).fill('player:nightowl');
    await expect(palette.getByRole('button', { name: /open dossier/ })).toBeVisible({ timeout: 15_000 });
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'nightowl', exact: true })).toBeVisible();

    // Star it — idempotent on re-runs of the persistent database.
    const watchToggle = page.getByRole('button', { name: /Watch/ });
    if (!(await watchToggle.getAttribute('aria-pressed'))?.includes('true')) {
      await watchToggle.click();
    }
    await expect(page.getByRole('button', { name: '★ Watching' })).toBeVisible({ timeout: 15_000 });

    // The players page shows the star and the watched-only filter keeps the row.
    await page.getByRole('link', { name: 'Players' }).click();
    await expect(page.locator('.stl-table tbody tr', { hasText: 'nightowl' }).locator('.watch-star')).toBeVisible();
    await page.locator('.watched-filter input').check();
    await expect(page.locator('.stl-table tbody tr')).toHaveCount(1, { timeout: 15_000 });
    await expect(page.locator('.stl-table tbody tr')).toContainText('nightowl');
  });

  test('erase confirmation requires a typed reason and cancels cleanly', async ({ page }) => {
    await login(page);

    await page.getByRole('link', { name: 'Players' }).click();
    const firstRow = page.locator('.stl-table tbody tr').first();
    await firstRow.waitFor({ state: 'visible', timeout: 15_000 });
    await firstRow.locator('a').first().click();
    await expect(page.getByRole('button', { name: 'Refresh from Wolvesville' })).toBeVisible({ timeout: 15_000 });

    const eraseButton = page.getByRole('button', { name: 'Erase…' });
    if (await eraseButton.count() === 0) {
      test.skip(true, 'no admin-only erase button on this row');
    }
    await eraseButton.click();

    const dialog = page.getByRole('dialog', { name: 'Confirm erasure' });
    await expect(dialog).toBeVisible();

    // A reason is mandatory: a too-short reason stays in the dialog.
    await dialog.getByRole('button', { name: 'Erase permanently' }).click();
    await expect(dialog.getByText('A reason of at least 4 characters is required.')).toBeVisible();

    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(dialog).toBeHidden();
  });
});
