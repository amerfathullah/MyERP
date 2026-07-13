import { test, expect } from '@playwright/test';

test.describe('E-Invoice (LHDN)', () => {
  test('LHDN dashboard loads with charts', async ({ page }) => {
    await page.goto('/e-invoice/dashboard');

    await expect(page.locator('abp-page')).toBeVisible();
    // Dashboard should show status summary cards
    await expect(page.locator('.card')).not.toHaveCount(0, { timeout: 10000 });
  });

  test('submission logs page loads', async ({ page }) => {
    await page.goto('/e-invoice/logs');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });
});

test.describe('CRM', () => {
  test('leads list loads', async ({ page }) => {
    await page.goto('/crm/leads');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });

  test('create lead form works', async ({ page }) => {
    await page.goto('/crm/leads/new');

    await expect(page.locator('form')).toBeVisible();
    await expect(page.getByLabel(/name/i).first()).toBeVisible();
  });

  test('opportunities list loads', async ({ page }) => {
    await page.goto('/crm/opportunities');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });
});

test.describe('HR & Payroll', () => {
  test('employees list loads', async ({ page }) => {
    await page.goto('/hr/employees');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });

  test('payroll page loads', async ({ page }) => {
    await page.goto('/hr/payroll');

    await expect(page.locator('abp-page')).toBeVisible();
  });
});

test.describe('Manufacturing', () => {
  test('work orders list loads', async ({ page }) => {
    await page.goto('/manufacturing/work-orders');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });
});

test.describe('Projects', () => {
  test('projects list loads', async ({ page }) => {
    await page.goto('/projects');

    await expect(page.locator('abp-page')).toBeVisible();
    await expect(page.locator('table.table')).toBeVisible({ timeout: 10000 });
  });
});
