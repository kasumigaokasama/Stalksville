import { expect, test } from '@playwright/test';

const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? '';

async function login(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });
}

test.describe('Operations: AI explain, exports, RBAC (mock Wolvesville)', () => {
  test('case explanation uses the guardrail sections', async ({ page }) => {
    await login(page);

    // Create a case via the UI.
    await page.getByRole('link', { name: 'Investigations' }).click();
    await page.getByPlaceholder('e.g. Moon Wolves membership churn').fill('Explainable case');
    await page.getByRole('button', { name: 'Create case' }).click();
    await expect(page.getByRole('heading', { name: /Explainable case/ })).toBeVisible({ timeout: 15_000 });

    await page.getByRole('button', { name: 'Explain this case' }).click();
    await expect(page.getByRole('heading', { name: 'Case explanation' })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText('deterministic', { exact: true })).toBeVisible();

    // The four guardrail sections (plan §41).
    for (const section of ['Observed', 'Derived', 'Hypothesis', 'Unknown']) {
      await expect(page.getByRole('heading', { name: section, level: 3 })).toBeVisible();
    }
    await expect(page.getByText(/No claim about real-world identity/)).toBeVisible();
  });

  test('markdown export downloads a report file', async ({ page }) => {
    await login(page);
    await page.goto('/investigations');

    // Open the first existing case.
    await page.locator('table a').first().click();
    await expect(page.getByText('Case timeline')).toBeVisible({ timeout: 15_000 });

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Export MD' }).click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/stalksville-case-\d{4}\.md/);
  });

  test('viewer role is read-only in API and UI', async ({ page }) => {
    await login(page);

    // Create a viewer through the admin API using the admin token from the session.
    const adminToken = await page.evaluate(() => sessionStorage.getItem('stv_token'));
    const viewerPassword = `viewer-${ADMIN_PASSWORD}`;
    const created = await page.request.post('/api/v1/admin/users', {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { username: 'spectator', password: viewerPassword, role: 'VIEWER' },
    });
    expect([201, 409]).toContain(created.status()); // 409 when the viewer already exists.

    // Sign out and back in as the viewer.
    await page.getByRole('button', { name: 'Sign out' }).click();
    await page.getByLabel('Username').fill('spectator');
    await page.getByLabel('Password').fill(viewerPassword);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });

    // UI hides analyst-only affordances.
    await page.getByRole('link', { name: 'Investigations' }).click();
    await expect(page.getByRole('heading', { name: 'Investigations' })).toBeVisible();
    await expect(page.getByText('New investigation')).toHaveCount(0);

    // API enforces it too.
    const viewerToken = await page.evaluate(() => sessionStorage.getItem('stv_token'));
    const forbidden = await page.request.post('/api/v1/investigations', {
      headers: { Authorization: `Bearer ${viewerToken}` },
      data: { title: 'should be forbidden' },
    });
    expect(forbidden.status()).toBe(403);
  });
});
