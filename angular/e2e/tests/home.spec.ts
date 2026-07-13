import { test, expect } from '@playwright/test';

test.describe('Home Dashboard', () => {
  test('displays KPI cards after login', async ({ page }) => {
    await page.goto('/');
    await page.waitForURL('**/home**');

    // Dashboard loads with KPI cards
    await expect(page.locator('.card')).not.toHaveCount(0, { timeout: 10000 });
    await expect(page.getByText('Customers')).toBeVisible();
    await expect(page.getByText('Items')).toBeVisible();
  });

  test('quick action buttons navigate correctly', async ({ page }) => {
    await page.goto('/');
    await page.waitForURL('**/home**');

    // Click "New Invoice" quick action
    const newInvoiceBtn = page.getByRole('button', { name: /new invoice/i });
    if (await newInvoiceBtn.isVisible()) {
      await newInvoiceBtn.click();
      await expect(page).toHaveURL(/.*sales\/invoices\/new.*/);
    }
  });
});
