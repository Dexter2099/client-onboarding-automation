import { expect, test } from '@playwright/test';

for (const screen of [
  { name: 'desktop', width: 1280, height: 900 },
  { name: 'phone', width: 390, height: 844 },
]) {
  test(`welcome recovery retains setup and succeeds on attempt two on ${screen.name}`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize(screen);
    await page.goto('/');
    await page.getByRole('button', { name: 'OF-1043 Website refresh', exact: true }).click();
    await expect(page.locator('details.project-context')).toHaveCount(1);
    await page.getByRole('button', { name: 'Start onboarding', exact: true }).click();
    await expect(
      page.getByRole('heading', { name: 'Welcome not prepared', exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText('1 of 3 attempts used · Request and review task saved', { exact: true }),
    ).toBeVisible();
    const projects = await (await page.request.get('/api/projects')).json();
    const endpoint = `/api/projects/${projects[1].id}/onboarding`;
    const failed = await (await page.request.get(endpoint)).json();
    await page.getByText('Demo tools · repeat protection', { exact: true }).click();
    await page.getByRole('button', { name: 'Repeat Start', exact: true }).click();
    expect((await (await page.request.get(endpoint)).json()).onboarding).toEqual(failed.onboarding);
    const restore = page.getByRole('button', { name: 'Restore simulated service', exact: true });
    await expect(restore).toBeEnabled();
    await restore.focus();
    await page.keyboard.press('Enter');
    await expect(
      page.getByRole('button', { name: 'Simulated service available', exact: true }),
    ).toBeDisabled();
    await expect(
      page.getByText('The simulated service is available. Try welcome to continue.', {
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      page.getByText('1 of 3 attempts used · Request and review task saved', { exact: true }),
    ).toBeVisible();
    await page.screenshot({
      path: `.local/reports/${testInfo.project.name}/welcome-recovery-${screen.name}.png`,
      caret: 'initial',
      fullPage: true,
    });
    if (screen.name === 'desktop') {
      await page.route('**/api/projects/*/welcome', async (route) => {
        expect((await route.fetch()).ok()).toBe(true);
        await route.abort('failed');
      });
    }
    await page.getByRole('button', { name: 'Try welcome', exact: true }).click();
    if (screen.name === 'desktop') {
      await expect(page.getByRole('alert')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Try welcome', exact: true })).toBeDisabled();
      await page.getByRole('button', { name: 'Load saved state', exact: true }).click();
    }
    await expect(page.getByText('Waiting for client', { exact: true })).toBeVisible();
    const recovered = await (await page.request.get(endpoint)).json();
    expect(recovered.onboarding.id).toBe(failed.onboarding.id);
    expect(recovered.onboarding.requestId).toBe(failed.onboarding.requestId);
    expect(recovered.onboarding.reviewTaskId).toBe(failed.onboarding.reviewTaskId);
    expect(
      recovered.onboarding.welcomeAttempts.map(
        (attempt: { prepared: boolean }) => attempt.prepared,
      ),
    ).toEqual([false, true]);
    expect(recovered.outbox).toHaveLength(1);
    await page.reload();
    await page.getByRole('button', { name: 'OF-1043 Website refresh', exact: true }).click();
    await expect(
      page.getByRole('button', { name: 'Load Maya’s sample reply', exact: true }),
    ).toBeVisible();
    expect((await (await page.request.get(endpoint)).json()).onboarding.welcomeAttempts).toEqual(
      recovered.onboarding.welcomeAttempts,
    );
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
  });
}

test('three failures remain exhausted after restoration until both sample projects are reset', async ({
  page,
}) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'OF-1043 Website refresh', exact: true }).click();
  await page.getByRole('button', { name: 'Start onboarding', exact: true }).click();
  await expect(
    page.getByText('1 of 3 attempts used · Request and review task saved', { exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Try welcome', exact: true }).click();
  await expect(
    page.getByText('2 of 3 attempts used · Request and review task saved', { exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Try welcome', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Welcome attempts exhausted', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Restore simulated service', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Try welcome', exact: true })).toBeDisabled();
  await expect(
    page.getByText('3 of 3 attempts used · Request and review task saved', { exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Reset both sample projects', exact: true }).click();
  await expect(page.getByRole('status')).toHaveText('Both sample projects have been reset.');
  await page.getByRole('button', { name: 'OF-1043 Website refresh', exact: true }).click();
  await page.getByRole('button', { name: 'Start onboarding', exact: true }).click();
  await expect(
    page.getByText('1 of 3 attempts used · Request and review task saved', { exact: true }),
  ).toBeVisible();
});
