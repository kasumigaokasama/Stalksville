import { expect, test } from '@playwright/test';

const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? '';

async function login(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });
}

test.describe('Stalksville smoke (mock Wolvesville)', () => {
  test('login lands on the dashboard', async ({ page }) => {
    await login(page);
    await expect(page.getByText('Players tracked')).toBeVisible();
  });

  test('settings reports mock mode and disabled write operations', async ({ page }) => {
    await login(page);
    await page.getByRole('link', { name: 'Settings' }).click();
    await expect(page.getByText('MockMode')).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Write operations' })).toBeVisible();
    await expect(page.getByText('disabled (by design — not implemented)', { exact: false })).toBeVisible();
  });

  test('player lookup → dossier → refresh detects the scripted clan change', async ({ page }) => {
    await login(page);

    await page.getByRole('link', { name: 'Players' }).click();
    await page.getByPlaceholder('Exact Wolvesville username, e.g. shadowfox').fill('shadowfox');
    await page.getByRole('button', { name: 'Look up & import' }).click();

    // Dossier opens with the observed state.
    await expect(page.getByRole('heading', { name: 'shadowfox', exact: true })).toBeVisible();
    await expect(page.getByText('Observed state')).toBeVisible();

    // Refresh: the mock dataset switches shadowfox's clan on every fetch — change detection fires.
    await page.getByRole('button', { name: 'Refresh from Wolvesville' }).click();
    await expect(page.getByText(/change\(s\) detected in this observation/)).toBeVisible({ timeout: 20_000 });

    // Changes tab shows the clan change with expandable evidence.
    await page.getByRole('tab', { name: /Changes/ }).click();
    await expect(page.getByText('Clan', { exact: true }).first()).toBeVisible();
    await page.getByRole('button', { name: 'Evidence' }).first().click();
    await expect(page.getByText(/snapshots that prove this change/)).toBeVisible();
  });

  test('command palette imports a player via player: prefix', async ({ page }) => {
    await login(page);
    await page.keyboard.press('Control+k');
    await page.getByPlaceholder(/Search…/).fill('player:nightowl');
    await expect(page.getByRole('button', { name: /open dossier/ })).toBeVisible({ timeout: 15_000 });
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'nightowl', exact: true })).toBeVisible();
  });
});
