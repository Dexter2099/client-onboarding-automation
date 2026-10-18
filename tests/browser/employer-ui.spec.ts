import { expect, test } from '@playwright/test';

for (const screen of [
  { name: 'desktop', width: 1280, height: 900 },
  { name: 'phone', width: 390, height: 844 },
]) {
  test(`employer sees a readable next action and reaches review in two actions on ${screen.name}`, async ({
    page,
  }) => {
    await page.setViewportSize(screen);
    await page.goto('/');
    await expect(
      page.getByRole('heading', { name: 'Get Juniper ready for kickoff', exact: true }),
    ).toBeVisible();
    const start = page.getByRole('button', { name: 'Start onboarding', exact: true });
    await expect(start).toBeInViewport();
    await expect(page.locator('button.primary:visible')).toHaveCount(1);
    await expect(page.getByText('Maya Ellis', { exact: true })).toBeHidden();
    const details = page.locator('.project-context > summary');
    await details.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByText('Maya Ellis', { exact: true })).toBeVisible();
    await page.keyboard.press('Enter');
    await expect(page.getByText('Maya Ellis', { exact: true })).toBeHidden();
    expect(
      await start.evaluate((element) => parseFloat(getComputedStyle(element).fontSize)),
    ).toBeGreaterThanOrEqual(16);
    const smallText = await page.evaluate(() => {
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      const undersized: string[] = [];
      while (walker.nextNode()) {
        const node = walker.currentNode;
        if (!node.textContent?.trim() || !node.parentElement) continue;
        const range = document.createRange();
        range.selectNodeContents(node);
        if (
          range.getBoundingClientRect().height > 0 &&
          parseFloat(getComputedStyle(node.parentElement).fontSize) < 14
        )
          undersized.push(node.textContent.trim());
      }
      return undersized;
    });
    expect(smallText).toEqual([]);
    await start.focus();
    await page.screenshot({
      path: `.local/reports/employer-entry-${screen.name}.png`,
      fullPage: true,
    });

    await start.click();
    const reply = page.getByRole('button', { name: 'Load Maya’s sample reply', exact: true });
    await expect(reply).toBeInViewport();
    await expect(page.locator('button.primary:visible')).toHaveCount(1);
    await expect(page.getByText('Demo · Sample client reply', { exact: true })).toBeVisible();
    await reply.click();
    await expect(
      page.getByRole('heading', { name: 'Goals & audience', exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole('heading', { name: 'Goals & audience', exact: true }),
    ).toBeFocused();
    await expect(
      page.getByRole('button', { name: 'Approve Goals & audience', exact: true }),
    ).toBeEnabled();
    await expect(
      page.getByRole('button', { name: 'Mark ready for kickoff', exact: true }),
    ).toHaveCount(0);
    await expect(page.locator('button.primary:visible')).toHaveCount(1);
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({
      path: `.local/reports/employer-review-${screen.name}.png`,
      fullPage: true,
    });
    if (screen.name === 'phone') {
      await page.setViewportSize({ width: 320, height: 844 });
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      ).toBe(true);
    }
  });
}

test('an uncertain review response keeps reload available when another answer is selected', async ({
  page,
}) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Start onboarding', exact: true }).click();
  await page.getByRole('button', { name: 'Load Maya’s sample reply', exact: true }).click();
  await page.route('**/api/projects/*/review', async (route) => {
    expect((await route.fetch()).ok()).toBe(true);
    await route.abort('failed');
  });
  await page.getByRole('button', { name: 'Approve Goals & audience', exact: true }).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await page
    .getByRole('navigation', { name: 'Required answers' })
    .getByRole('button', { name: /Brand assets/ })
    .click();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Approve Brand assets', exact: true }),
  ).toBeDisabled();
  await page.getByRole('button', { name: 'Load saved state', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(
    page.getByRole('button', { name: 'Approve Brand assets', exact: true }),
  ).toBeEnabled();
  await expect(
    page
      .getByRole('navigation', { name: 'Required answers' })
      .getByRole('button', { name: /Goals & audience Approved/ }),
  ).toBeVisible();
});
