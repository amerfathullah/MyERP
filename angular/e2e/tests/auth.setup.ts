import { test as setup, expect } from '@playwright/test';
import path from 'path';

const authFile = path.join(__dirname, '../.auth/user.json');

setup('authenticate', async ({ page }) => {
  await page.goto('/');

  // ABP OpenIddict login flow — redirects to login page
  await page.waitForURL('**/Account/Login**', { timeout: 15000 });

  await page.getByLabel('Username or email address').fill('admin');
  await page.getByLabel('Password').fill('1q2w3E*');
  await page.getByRole('button', { name: 'Login' }).click();

  // Wait for redirect back to the app
  await page.waitForURL('**/home**', { timeout: 15000 });
  await expect(page.locator('abp-dynamic-layout')).toBeVisible();

  // Save authenticated state
  await page.context().storageState({ path: authFile });
});
