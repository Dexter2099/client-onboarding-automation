import { expect, test } from '@playwright/test';

test('CSP blocks injected scripts, eval and external connections while the app stays usable', async ({
  page,
}) => {
  let externalRequested = false;
  await page.route('https://example.invalid/**', async (route) => {
    externalRequested = true;
    await route.abort();
  });
  // DevTools evaluation can bypass eval restrictions. Exercise a normally loaded script instead.
  await page.route('**/csp-probe.js', (route) =>
    route.fulfill({
      contentType: 'application/javascript',
      body: `try {
        new Function('return 1')();
        document.documentElement.dataset.evalProbe = 'allowed';
      } catch (error) {
        document.documentElement.dataset.evalProbe = error instanceof EvalError ? 'blocked' : 'unexpected';
      }`,
    }),
  );
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Start onboarding', exact: true })).toBeVisible();
  const result = await page.evaluate(async () => {
    const script = document.createElement('script');
    script.textContent = "document.documentElement.dataset.injectedScript = 'executed'";
    document.head.append(script);
    const probe = document.createElement('script');
    probe.src = '/csp-probe.js';
    const loaded = new Promise<void>((resolve, reject) => {
      probe.onload = () => resolve();
      probe.onerror = () => reject(new Error('The same-origin CSP probe could not load.'));
    });
    document.head.append(probe);
    await loaded;
    let connectionBlocked = false;
    try {
      await fetch('https://example.invalid/csp-probe');
    } catch {
      connectionBlocked = true;
    }
    return {
      inlineBlocked: !document.documentElement.dataset.injectedScript,
      evalBlocked: document.documentElement.dataset.evalProbe === 'blocked',
      connectionBlocked,
    };
  });
  expect(result).toEqual({ inlineBlocked: true, evalBlocked: true, connectionBlocked: true });
  expect(externalRequested).toBe(false);
  await page.getByRole('button', { name: 'Start onboarding', exact: true }).click();
  await expect(page.getByText('Waiting for client', { exact: true })).toBeVisible();
});
