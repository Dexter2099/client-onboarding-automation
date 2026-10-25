import { expect, test } from '@playwright/test';

test('opens the main walkthrough and preserves separate work across optional recovery and refresh', async ({
  page,
}, testInfo) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Website redesign' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Back to main walkthrough' })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Website refresh' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Start onboarding', exact: true }).click();
  await expect(page.getByText('Waiting for client', { exact: true })).toBeVisible();
  await page.getByText('Project details', { exact: false }).click();
  await expect(page.getByText('Maya Ellis', { exact: true })).toBeVisible();
  const session = await (await page.request.get('/api/session')).json();
  const projects = await (await page.request.get('/api/projects')).json();
  const mainRoute = `/api/projects/${projects[0].id}/onboarding`;
  const savedMain = await (await page.request.get(mainRoute)).json();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Website redesign' })).toBeVisible();
  expect(await (await page.request.get('/api/session')).json()).toEqual(session);
  expect(await (await page.request.get('/api/projects')).json()).toEqual(projects);
  await page.getByRole('button', { name: 'Try failure recovery', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Recover a failed welcome' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Website refresh' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Website refresh' })).toBeFocused();
  await expect(page.locator('details.project-context')).toHaveCount(1);
  await expect(page.getByText('Maya Ellis', { exact: true })).toBeHidden();
  await page.getByRole('button', { name: 'Start onboarding', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Welcome not prepared', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Back to main walkthrough', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Website redesign' })).toBeFocused();
  await expect(page.getByText('Waiting for client', { exact: true })).toBeVisible();
  expect(await (await page.request.get(mainRoute)).json()).toEqual(savedMain);
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Website redesign' })).toBeVisible();
  await page.getByRole('button', { name: 'Try failure recovery', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Welcome not prepared', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Back to main walkthrough', exact: true }).click();
  await page.screenshot({
    path: `.local/reports/${testInfo.project.name}/foundation-desktop.png`,
    caret: 'initial',
    fullPage: true,
  });
});

test('reset replaces both projects while another visitor retains their workspace', async ({
  browser,
  page,
}) => {
  const otherContext = await browser.newContext();
  try {
    const other = await otherContext.newPage();
    await Promise.all([page.goto('/'), other.goto('/')]);
    await expect(page.getByRole('heading', { name: 'Website redesign' })).toBeVisible();
    await expect(other.getByRole('heading', { name: 'Website redesign' })).toBeVisible();
    const before = await (await page.request.get('/api/projects')).json();
    const otherBefore = await (await other.request.get('/api/projects')).json();
    expect((await other.request.get(`/api/projects/${before[0].id}`)).status()).toBe(404);
    await page.getByRole('button', { name: 'Reset both sample projects' }).click();
    await expect(page.getByRole('status')).toHaveText('Both sample projects have been reset.');
    const after = await (await page.request.get('/api/projects')).json();
    expect(after.map((p: { id: string }) => p.id)).not.toEqual(
      before.map((p: { id: string }) => p.id),
    );
    expect(after.map((p: { reference: string }) => p.reference)).toEqual(['OF-1042', 'OF-1043']);
    expect(await (await other.request.get('/api/projects')).json()).toEqual(otherBefore);
  } finally {
    await otherContext.close();
  }
});

test('phone layout fits and optional recovery navigation works by keyboard', async ({
  page,
}, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  const recovery = page.getByRole('button', { name: 'Try failure recovery', exact: true });
  await expect(recovery).toBeVisible();
  await recovery.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Website refresh' })).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.getByRole('button', { name: 'Back to main walkthrough', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Website redesign' })).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({
    path: `.local/reports/${testInfo.project.name}/foundation-mobile.png`,
    caret: 'initial',
    fullPage: true,
  });
});
