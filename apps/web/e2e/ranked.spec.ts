import { expect, test } from '@playwright/test';

const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? '';

async function login(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });
}

test.describe('Ranked (mock Wolvesville)', () => {
  test('season card renders, capture fills the board, tracked rows deep-link', async ({ page }) => {
    await login(page);

    await page.getByRole('link', { name: 'Ranked' }).click();
    await expect(page.getByRole('heading', { name: 'Ranked', exact: true })).toBeVisible();

    // Season context from GET /ranked/season.
    await expect(page.getByRole('heading', { name: 'Season 21' })).toBeVisible({ timeout: 20_000 });

    await page.getByRole('button', { name: 'Capture now' }).click();
    await expect(page.getByText(/Captured 3 entries/)).toBeVisible({ timeout: 20_000 });

    // The mock board ranks talon first.
    const firstRow = page.locator('.stl-table tbody tr').first();
    await expect(firstRow.locator('td').nth(1)).toContainText('talon');

    // Track imports an untracked mock player and re-resolves the tracked flag. The e2e database
    // is persistent, so a player tracked by an earlier run must already satisfy the assertion.
    const talonRow = page.locator('.stl-table tbody tr', { hasText: 'talon' });
    const trackButton = talonRow.getByRole('button', { name: 'Track' });
    if (await trackButton.count() > 0) {
      await trackButton.click();
      await expect(page.getByText(/Imported talon/)).toBeVisible({ timeout: 20_000 });
    }
    await expect(talonRow.getByText('tracked')).toBeVisible({ timeout: 15_000 });
  });

  test('hall of fame lists finished-season winners with tracked links', async ({ page }) => {
    test.setTimeout(90_000); // view switch + capture + optional import on a cold runner
    await login(page);

    await page.getByRole('link', { name: 'Ranked' }).click();
    await page.getByRole('tab', { name: 'Hall of fame' }).click();

    await page.getByRole('button', { name: 'Capture now' }).click();
    await expect(page.getByText(/Captured 3 season-20 winners/)).toBeVisible({ timeout: 20_000 });

    // The mock season 20 includes talon among the winners; tracked winners deep-link.
    const talonRow = page.locator('.stl-table tbody tr', { hasText: 'talon' });
    const trackButton = talonRow.getByRole('button', { name: 'Track' });
    if (await trackButton.count() > 0) {
      await trackButton.click();
      await expect(page.getByText(/Imported talon/)).toBeVisible({ timeout: 20_000 });
    }
    await expect(talonRow.getByText('tracked')).toBeVisible({ timeout: 15_000 });

    // Untracked winners keep their identity row with an avatar.
    const frostRow = page.locator('.stl-table tbody tr', { hasText: 'FrostReign' });
    await expect(frostRow.locator('img.winner-avatar')).toBeVisible();
  });
});
