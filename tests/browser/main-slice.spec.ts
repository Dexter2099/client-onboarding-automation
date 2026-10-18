import { expect, test } from '@playwright/test';

for (const screen of [
  { name: 'desktop', width: 1280, height: 900 },
  { name: 'phone', width: 390, height: 844 },
]) {
  test(`complete review, correction and saved kickoff brief on ${screen.name}`, async ({
    page,
  }) => {
    await page.setViewportSize({ width: screen.width, height: screen.height });
    const runtimeErrors: string[] = [];
    page.on('pageerror', (error) => runtimeErrors.push(error.message));
    await page.goto('/');
    await page.getByRole('button', { name: 'Start onboarding', exact: true }).click();
    await expect(page.getByText('Waiting for client', { exact: true })).toBeVisible();

    if (screen.name === 'phone') {
      await page.getByRole('tab', { name: 'Onboarding', exact: true }).focus();
      await page.keyboard.press('End');
      await expect(page.getByRole('tab', { name: /Outbox/ })).toBeFocused();
    } else await page.getByRole('tab', { name: /Outbox/ }).click();
    await expect(page.getByText('Simulated · prepared only', { exact: true })).toBeVisible();
    await expect(
      page.getByText('Simulated messages, prepared in this sandbox. No email is sent.'),
    ).toBeVisible();
    if (screen.name === 'phone') {
      await page.keyboard.press('Home');
      await expect(page.getByRole('tab', { name: 'Onboarding', exact: true })).toBeFocused();
    } else await page.getByRole('tab', { name: 'Onboarding', exact: true }).click();
    await page.getByRole('button', { name: 'Load Maya’s sample reply', exact: true }).click();
    await expect(
      page.getByRole('button', { name: 'Mark ready for kickoff', exact: true }),
    ).toHaveCount(0);
    await page.getByRole('button', { name: 'Approve Goals & audience', exact: true }).click();
    await page
      .getByRole('navigation', { name: 'Required answers' })
      .getByRole('button', { name: /Brand assets/ })
      .click();
    await page.getByRole('button', { name: 'Approve Brand assets', exact: true }).click();
    await page
      .getByRole('navigation', { name: 'Required answers' })
      .getByRole('button', { name: /Pages/ })
      .click();
    await expect(page.getByRole('button', { name: 'Approve Pages', exact: true })).toBeDisabled();
    await expect(
      page.getByText(
        'Shop is outside the signed five-page scope. Request a correction before approving Pages.',
      ),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Request changes', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText('Add a correction comment');
    const comment = 'Please remove Shop and confirm the five pages in our signed agreement.';
    await page.getByRole('textbox', { name: 'Correction comment', exact: true }).fill(comment);
    await page.getByRole('button', { name: 'Request changes', exact: true }).click();
    await expect(page.getByText('Waiting for correction', { exact: true })).toBeVisible();
    await page.getByRole('tab', { name: /Outbox/ }).click();
    await expect(page.getByText(comment, { exact: true })).toBeVisible();
    await expect(page.getByText('Simulated · prepared only', { exact: true })).toHaveCount(2);
    await page.getByRole('tab', { name: 'Onboarding', exact: true }).click();
    await page.getByRole('button', { name: 'Load Maya’s corrected Pages', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Approve Pages', exact: true })).toBeEnabled();
    await expect(
      page.getByRole('button', { name: 'Mark ready for kickoff', exact: true }),
    ).toHaveCount(0);
    await expect(
      page
        .getByRole('navigation', { name: 'Required answers' })
        .getByRole('button', { name: /Goals & audience Approved/ }),
    ).toBeVisible();
    await expect(
      page
        .getByRole('navigation', { name: 'Required answers' })
        .getByRole('button', { name: /Brand assets Approved/ }),
    ).toBeVisible();
    await page.getByText('Earlier revisions (1)', { exact: true }).click();
    await expect(page.getByText('Shop', { exact: true })).toBeVisible();
    await page.screenshot({
      path: `.local/reports/main-review-${screen.name}.png`,
      fullPage: true,
    });
    await page.getByRole('button', { name: 'Approve Pages', exact: true }).click();
    await expect(page.getByText('Review complete', { exact: true })).toBeVisible();
    await expect(
      page.getByRole('button', { name: 'Mark ready for kickoff', exact: true }),
    ).toBeEnabled();
    await page.getByRole('button', { name: 'Mark ready for kickoff', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Kickoff brief', exact: true })).toBeVisible();
    await expect(page.getByText('Ready for kickoff', { exact: true })).toBeVisible();
    await expect(page.getByText('Handoff: not published.', { exact: true })).toBeVisible();
    await expect(page.getByText('Shop', { exact: true })).toHaveCount(0);
    const projects = await (await page.request.get('/api/projects')).json();
    const endpoint = `/api/projects/${projects[0].id}/onboarding`;
    const saved = await (await page.request.get(endpoint)).json();
    expect(saved.brief.pages).toEqual(['Home', 'About', 'Services', 'Projects', 'Contact']);
    expect(
      saved.brief.approvals.find((approval: { kind: string }) => approval.kind === 'pages').number,
    ).toBe(2);
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Kickoff brief', exact: true })).toBeVisible();
    expect((await (await page.request.get(endpoint)).json()).brief).toEqual(saved.brief);
    await page.getByRole('tab', { name: 'Activity', exact: true }).click();
    await expect(
      page.getByRole('heading', { name: 'Corrected Pages received', exact: true }),
    ).toBeVisible();
    await page.getByRole('tab', { name: 'Kickoff brief', exact: true }).click();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({ path: `.local/reports/main-brief-${screen.name}.png`, fullPage: true });
    if (screen.name === 'desktop') {
      // Commit the handoff but lose its HTTP confirmation. The UI must read before retrying.
      let loseResponse = true;
      await page.route('**/api/projects/*/publish', async (route) => {
        if (!loseResponse) return route.continue();
        loseResponse = false;
        expect((await route.fetch()).ok()).toBe(true);
        await route.abort('failed');
      });
      await page.getByRole('button', { name: 'Publish simulated handoff', exact: true }).click();
      await expect(page.getByRole('alert')).toBeVisible();
      await expect(
        page.getByRole('heading', { name: 'Simulated handoff confirmed', exact: true }),
      ).toHaveCount(0);
      await expect(
        page.getByRole('button', { name: 'Publish simulated handoff', exact: true }),
      ).toBeDisabled();
      await page.getByRole('button', { name: 'Load saved state', exact: true }).click();
    } else {
      await page.getByRole('button', { name: 'Publish simulated handoff', exact: true }).click();
    }
    await expect(
      page.getByRole('heading', { name: 'Simulated handoff confirmed', exact: true }),
    ).toBeVisible();
    const published = await (await page.request.get(endpoint)).json();
    expect(published.registerEntry.brief).toEqual(saved.brief);
    await page.getByText('Inspect register entry', { exact: true }).click();
    await expect(page.getByText(published.registerEntry.reference, { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Repeat Publish', exact: true }).click();
    await expect(
      page.getByText('Same register entry reused. No duplicate handoff.', { exact: true }),
    ).toBeVisible();
    expect((await (await page.request.get(endpoint)).json()).registerEntry).toEqual(
      published.registerEntry,
    );
    await page.reload();
    await expect(
      page.getByRole('heading', { name: 'Simulated handoff confirmed', exact: true }),
    ).toBeVisible();
    expect(
      (await (await page.request.get(endpoint)).json()).activity.filter(
        (event: { action: string }) => event.action === 'Simulated handoff confirmed',
      ),
    ).toHaveLength(1);
    await page.screenshot({ path: `.local/reports/handoff-${screen.name}.png`, fullPage: true });
    expect(runtimeErrors).toEqual([]);
  });
}
