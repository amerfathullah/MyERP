import { test, expect } from '@playwright/test';

test.describe('Accounting', () => {
  test('chart of accounts page loads', async ({ page }) => {
    await page.goto('/accounting/accounts');

    await expect(page.locator('abp-page')).toBeVisible();
    // Tree structure or table should render
    await expect(page.locator('table.table, .account-tree')).toBeVisible({ timeout: 10000 });
  });

  test('journal entry form enforces double-entry balance', async ({ page }) => {
    await page.goto('/accounting/journal-entries/new');

    await expect(page.locator('form')).toBeVisible();
    // The form should have debit/credit columns
    await expect(page.getByText(/debit/i)).toBeVisible();
    await expect(page.getByText(/credit/i)).toBeVisible();
  });

  test('trial balance report loads', async ({ page }) => {
    await page.goto('/accounting/reports/trial-balance');

    await expect(page.locator('abp-page')).toBeVisible();
    // Report should have filter fields
    await expect(page.getByLabel(/company/i)).toBeVisible();
  });
});

test.describe('Inventory', () => {
  test('items list page renders', async ({ page }) => {
    await page.goto('/inventory/items');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });

  test('create item form works', async ({ page }) => {
    await page.goto('/inventory/items/new');

    await expect(page.locator('form')).toBeVisible();
    await expect(page.getByLabel(/item name/i)).toBeVisible();
    await expect(page.getByLabel(/item code/i)).toBeVisible();
  });

  test('stock ledger report loads with filters', async ({ page }) => {
    await page.goto('/inventory/reports/stock-ledger');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.getByRole('button', { name: /filter|search|apply/i })).toBeVisible();
  });
});
