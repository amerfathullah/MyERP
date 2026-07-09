import { test, expect } from '@playwright/test';

test.describe('Sales Invoices', () => {
  test('list page loads with table', async ({ page }) => {
    await page.goto('/sales/invoices');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });

  test('create invoice form renders correctly', async ({ page }) => {
    await page.goto('/sales/invoices/new');

    // Form fields should be visible
    await expect(page.locator('form')).toBeVisible();
    await expect(page.getByLabel(/customer/i)).toBeVisible();
    await expect(page.getByLabel(/issue date/i)).toBeVisible();

    // Save button exists
    await expect(page.getByRole('button', { name: /save/i })).toBeVisible();
  });

  test('create and submit a sales invoice', async ({ page }) => {
    await page.goto('/sales/invoices/new');

    // Fill mandatory fields
    await page.getByLabel(/customer/i).selectOption({ index: 1 });
    await page.getByLabel(/issue date/i).fill('2026-07-09');

    // Save the invoice
    await page.getByRole('button', { name: /save/i }).click();

    // Should navigate to detail or list
    await page.waitForURL(/.*sales\/invoices.*/);

    // Verify toast notification
    await expect(page.locator('.abp-toast')).toBeVisible({ timeout: 5000 });
  });

  test('invoice detail shows workflow actions', async ({ page }) => {
    await page.goto('/sales/invoices');
    await page.waitForLoadState('networkidle');

    // Click first invoice row if any exist
    const firstRow = page.locator('table.table tbody tr').first();
    if (await firstRow.isVisible()) {
      await firstRow.locator('a, button').first().click();
      await page.waitForURL(/.*sales\/invoices\/.*/);

      // Workflow component should be visible
      await expect(page.locator('app-document-workflow, .badge')).toBeVisible();
    }
  });
});
