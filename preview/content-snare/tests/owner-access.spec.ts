import { test, expect } from '@playwright/test';

test('private owner form submits under the enforced browser policy', async ({ page }) => {
  test.skip(process.env.ONBOARDFLOW_BROWSER_FIXTURE !== '1', 'Requires the C# API fixture.');
  const initial = await page.goto('/api/owner-access');
  expect(initial?.headers()['content-security-policy']).toContain("form-action 'self'");
  await page.getByLabel('Owner pass', { exact: true }).fill('invalid-test-owner-pass');
  const submitted = page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === '/api/owner-access' &&
      response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Enable owner access', exact: true }).click();
  const rejected = await submitted;
  expect(rejected.status()).toBe(403);
  // Firefox replaces top-level 403 navigation with its error document; use the HTTP evidence.
  expect(rejected.headers()['content-security-policy']).toContain("form-action 'self'");
  const publicPage = await page.goto('/');
  expect(publicPage?.headers()['content-security-policy']).toContain("form-action 'none'");
});
