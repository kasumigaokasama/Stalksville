import { expect, test } from '@playwright/test';

const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? '';

test.describe('Investigations (mock Wolvesville)', () => {
  test('create case → add player target → notes + aggregated timeline', async ({ page }) => {
    // Import a player first so the case has a tracked entity to target.
    await page.goto('/login');
    await page.getByLabel('Username').fill('admin');
    await page.getByLabel('Password').fill(ADMIN_PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });

    await page.getByRole('link', { name: 'Players' }).click();
    await page.getByPlaceholder('Exact Wolvesville username, e.g. shadowfox').fill('talon');
    await page.getByRole('button', { name: 'Look up & import' }).click();
    await expect(page.getByRole('heading', { name: 'talon', exact: true })).toBeVisible({ timeout: 15_000 });

    // Create the case.
    await page.getByRole('link', { name: 'Investigations' }).click();
    await page.getByPlaceholder('e.g. Moon Wolves membership churn').fill('Talon network');
    await page.getByRole('button', { name: 'Create case' }).click();
    await expect(page.getByRole('heading', { name: /Talon network/ })).toBeVisible({ timeout: 15_000 });

    // Add the tracked player as a target.
    await page.locator('.add-target select').selectOption('player');
    await page.getByPlaceholder('Search tracked entities…').fill('talon');
    await page.getByRole('button', { name: /talon/ }).first().click();
    await expect(page.getByRole('link', { name: 'talon', exact: true }).first()).toBeVisible({ timeout: 15_000 });

    // The aggregated case timeline includes the discovery event.
    await expect(page.getByText('Case timeline')).toBeVisible();
    await expect(page.getByText('PlayerDiscovered').first()).toBeVisible();

    // Add a note.
    await page.getByPlaceholder('Observation, hypothesis, next steps…').fill('Talon is clanmates with flex.');
    await page.getByRole('button', { name: 'Add note' }).click();
    await expect(page.getByText('Talon is clanmates with flex.')).toBeVisible({ timeout: 15_000 });

    // Archive locks the case.
    await page.getByRole('button', { name: 'Archive' }).click();
    await expect(page.getByText('archived', { exact: false }).first()).toBeVisible({ timeout: 15_000 });
  });

  test('timeline page lists events with filters', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Username').fill('admin');
    await page.getByLabel('Password').fill(ADMIN_PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });

    await page.getByRole('link', { name: 'Timeline' }).click();
    await expect(page.getByRole('heading', { name: 'Timeline' })).toBeVisible();
    await expect(page.locator('.timeline-item').first()).toBeVisible({ timeout: 15_000 });

    // Derived-only filter narrows to derived events.
    await page.locator('.filters select').nth(1).selectOption('derived');
    await expect(page.locator('.timeline-item .stl-tag--derived').first()).toBeVisible({ timeout: 15_000 });
  });
  test('command palette finds cases by prose and workspace shows tags', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Username').fill('admin');
    await page.getByLabel('Password').fill(ADMIN_PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible({ timeout: 20_000 });

    // Create a case with distinctive prose the palette can find via full-text search.
    await page.getByRole('link', { name: 'Investigations' }).click();
    await page.getByPlaceholder('e.g. Moon Wolves membership churn').fill('Zephyr cliff investigation');
    await page.getByRole('button', { name: 'Create case' }).click();
    await expect(page.getByRole('heading', { name: /Zephyr cliff investigation/ })).toBeVisible({ timeout: 15_000 });

    // Tag the case from the workspace.
    const tagInput = page.locator('.tag-input');
    await tagInput.fill('high-priority');
    await tagInput.press('Enter');
    await expect(page.locator('.tag-chip', { hasText: 'high-priority' })).toBeVisible({ timeout: 15_000 });

    // The palette finds the case by a word from its title.
    await page.keyboard.press('Control+k');
    await page.getByPlaceholder(/Search…/).fill('zephyr');
    await expect(page.locator('.stl-palette-panel .result', { hasText: 'Zephyr cliff investigation' })).toBeVisible({ timeout: 15_000 });
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: /Zephyr cliff investigation/ })).toBeVisible({ timeout: 15_000 });
  });
});

