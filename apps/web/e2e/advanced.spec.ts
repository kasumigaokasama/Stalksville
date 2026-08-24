import { expect, test } from '@playwright/test';

const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? '';

async function login(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });
}

test.describe('Advanced intelligence (mock Wolvesville)', () => {
  test('graph page renders nodes, edges and connection paths', async ({ page }) => {
    await login(page);

    await page.getByRole('link', { name: 'Graph' }).click();
    await expect(page.getByRole('heading', { name: 'Graph' })).toBeVisible();
    await expect(page.getByText(/nodes ·/)).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('.canvas canvas').first()).toBeVisible();
    await expect(page.getByText('MEMBER_OF (current)').first()).toBeVisible(); // legend

    // Path finding between two clanmates.
    await page.locator('.path-finder select').nth(0).selectOption({ label: 'shadowfox' });
    await page.locator('.path-finder select').nth(1).selectOption({ label: 'nightowl' });
    await expect(page.getByRole('heading', { name: 'Connection paths' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.path').first()).toContainText('shadowfox');
    await expect(page.locator('.path').first()).toContainText('nightowl');
  });

  test('dossier Intelligence tab shows explainable exposure and insights', async ({ page }) => {
    await login(page);

    await page.keyboard.press('Control+k');
    await page.getByPlaceholder(/Search…/).fill('player:shadowfox');
    await expect(page.getByRole('button', { name: /open dossier/ })).toBeVisible({ timeout: 15_000 });
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'shadowfox', exact: true })).toBeVisible();

    await page.getByRole('tab', { name: /Intelligence/ }).click();
    await expect(page.getByText('Exposure score')).toBeVisible();
    await expect(page.locator('.exposure-total').first()).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText('Identity surface', { exact: true })).toBeVisible();
    await expect(page.getByText('Why? — every point and its evidence')).toBeVisible();

    // shadowfox has scripted clan switches → volatility insight with evidence.
    await expect(page.getByText('Membership volatility').first()).toBeVisible();
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
