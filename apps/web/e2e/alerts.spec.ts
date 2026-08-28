import { expect, test } from '@playwright/test';

const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? '';

async function login(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });
}

test.describe('Alerts inbox (mock Wolvesville)', () => {
  test('scripted clan change → unread badge → inbox with evidence → mark read', async ({ page }) => {
    await login(page);

    // The e2e database persists across runs, so the inbox may hold older alerts —
    // this spec only asserts on the alert it creates itself.

    // Import shadowfox and refresh: the mock switches its clan on every fetch.
    await page.getByRole('link', { name: 'Players' }).click();
    await page.getByPlaceholder('Exact Wolvesville username, e.g. shadowfox').fill('shadowfox');
    await page.getByRole('button', { name: 'Look up & import' }).click();
    await expect(page.getByRole('heading', { name: 'shadowfox', exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Refresh from Wolvesville' }).click();
    await expect(page.getByText(/change\(s\) detected in this observation/)).toBeVisible({ timeout: 20_000 });

    // The shell badge now shows at least one unread alert.
    await page.getByRole('link', { name: 'Analytics' }).click(); // navigation triggers the badge poll
    await expect(page.locator('.nav-badge')).toBeVisible({ timeout: 20_000 });

    // Inbox lists the alert with its evidence block.
    await page.getByRole('link', { name: 'Alerts' }).click();
    const row = page.locator('.alert-item', { hasText: 'shadowfox changed clan' }).first();
    await expect(row).toBeVisible({ timeout: 20_000 });
    await expect(row.locator('.stl-tag--derived', { hasText: 'evidence' })).toBeVisible();
    await expect(row.locator('.evidence-change').first()).toContainText('clanId:');

    // Mark that alert read: its row loses the unread markers (badge may keep older unread alerts).
    await row.getByRole('button', { name: 'Mark read' }).click();
    await expect(row.locator('.read-at')).toBeVisible({ timeout: 20_000 });
    await expect(row.locator('.unread-dot')).toHaveCount(0);
  });
});
