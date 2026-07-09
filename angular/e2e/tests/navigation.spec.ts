import { test, expect } from '@playwright/test';

test.describe('Navigation & Menu', () => {
  test('sidebar menu has all module groups', async ({ page }) => {
    await page.goto('/');
    await page.waitForURL('**/home**');

    const sidebar = page.locator('abp-dynamic-layout nav, .lpx-sidemenu, [role="navigation"]');
    await expect(sidebar).toBeVisible();

    // Core menu groups should be present
    const menuItems = ['Sales', 'Purchasing', 'Accounting', 'Inventory'];
    for (const item of menuItems) {
      await expect(page.getByText(item, { exact: false }).first()).toBeVisible();
    }
  });

  test('unauthorized user cannot access admin pages', async ({ browser }) => {
    // Use a fresh context without auth state
    const context = await browser.newContext();
    const page = await context.newPage();

    await page.goto('/sales/invoices');
    // Should redirect to login
    await page.waitForURL('**/Account/Login**', { timeout: 15000 });
    await context.close();
  });
});

test.describe('Purchasing Flow', () => {
  test('purchase orders list loads', async ({ page }) => {
    await page.goto('/purchasing/orders');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });

  test('purchase invoices list loads', async ({ page }) => {
    await page.goto('/purchasing/invoices');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });
});

test.describe('Tax Management', () => {
  test('tax categories page loads', async ({ page }) => {
    await page.goto('/tax/categories');

    await expect(page.locator('abp-page')).toBeVisible();
  });
});

test.describe('Settings & Admin', () => {
  test('company list loads', async ({ page }) => {
    await page.goto('/companies');

    await expect(page.locator('abp-page')).toBeVisible();
  });

  test('import/export page loads', async ({ page }) => {
    await page.goto('/import-export');

    await expect(page.locator('abp-page')).toBeVisible();
  });
});
