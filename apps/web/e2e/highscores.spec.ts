import { expect, test } from '@playwright/test';

const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? '';

async function login(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });
}

test.describe('Highscores (mock Wolvesville)', () => {
  test('capture fills the board and tracked players link to dossiers', async ({ page }) => {
    await login(page);

    await page.getByRole('link', { name: 'Highscores' }).click();
    await expect(page.getByRole('heading', { name: 'Highscores' })).toBeVisible();

    // First capture: the mock boards land in the table.
    await page.getByRole('button', { name: 'Capture now' }).click();
    await expect(page.getByText(/Captured \d+ entries/)).toBeVisible({ timeout: 20_000 });

    const firstRow = page.locator('.stl-table tbody tr').first();
    await expect(firstRow.locator('td').nth(1)).toContainText('shadowfox');

    // Switching periods swaps the board.
    await page.locator('.filters select').selectOption('weekly');
    await expect(page.locator('.stl-table tbody tr').first()).toContainText('nightowl', { timeout: 15_000 });
  });
});
