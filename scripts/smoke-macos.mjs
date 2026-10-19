import assert from 'node:assert/strict';
import { appendFile, mkdir, writeFile } from 'node:fs/promises';
import { release } from 'node:os';
import { expect as playwrightExpect, webkit } from '@playwright/test';

// A bounded live visitor check, separate from the disposable-database regression suite.
assert.equal(process.platform, 'darwin', 'This evidence must come from native macOS.');
const revision = process.env.GITHUB_SHA;
assert.match(revision ?? '', /^[a-f0-9]{40}$/);
const origin = 'https://dexter-onboardflow.de.deplexo.com';
const output = '.local/macos-browser-evidence';
const expect = playwrightExpect.configure({ timeout: 20000 });
await mkdir(output, { recursive: true });
const browser = await webkit.launch();
const context = await browser.newContext({
  viewport: { width: 1280, height: 900 },
  deviceScaleFactor: 2,
});
context.setDefaultTimeout(20000);
const page = await context.newPage();
const errors = [];
const policyErrors = [];
page.on('pageerror', (error) => errors.push(error.message));
page.on('console', (message) => {
  if (/content[- ]security[- ]policy/i.test(message.text())) policyErrors.push(message.text());
});

async function readJson(path) {
  const response = await context.request.get(origin + path);
  assert.equal(response.status(), 200);
  return response.json();
}

try {
  const marker = await readJson(`/release.json?revision=${revision}`);
  assert.equal(marker.revision, revision);
  const response = await page.goto(origin, { waitUntil: 'domcontentloaded' });
  assert.equal(response.status(), 200);
  assert.match(response.headers()['content-security-policy'] ?? '', /script-src 'self'/);
  assert.equal(response.headers()['strict-transport-security'], 'max-age=86400');

  await page.getByRole('button', { name: 'Start onboarding', exact: true }).click();
  await page.getByRole('button', { name: 'Load Maya’s sample reply', exact: true }).click();
  await page.getByRole('button', { name: 'Approve Goals & audience', exact: true }).click();
  await page.getByRole('button', { name: 'Pages Awaiting review', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Approve Pages', exact: true })).toBeDisabled();
  await page
    .getByRole('textbox', { name: 'Correction comment', exact: true })
    .fill('Please remove Shop and confirm the five signed pages.');
  await page.getByRole('button', { name: 'Request changes', exact: true }).click();
  await page.getByRole('button', { name: 'Load Maya’s corrected Pages', exact: true }).click();
  await page.getByRole('button', { name: 'Approve Pages', exact: true }).click();
  await page.getByRole('button', { name: 'Brand assets Awaiting review', exact: true }).click();
  await page.getByRole('button', { name: 'Approve Brand assets', exact: true }).click();
  await page.getByRole('button', { name: 'Mark ready for kickoff', exact: true }).click();
  await page.getByRole('button', { name: 'Publish simulated handoff', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Simulated handoff confirmed', exact: true }),
  ).toBeVisible();
  const project = (await readJson('/api/projects')).find((item) => item.reference === 'OF-1042');
  assert(project);
  const route = `/api/projects/${project.id}/onboarding`;
  const published = await readJson(route);
  assert(published.registerEntry);
  assert.deepEqual(published.brief.pages, ['Home', 'About', 'Services', 'Projects', 'Contact']);
  await page.getByText('Inspect register entry', { exact: true }).click();
  await page.getByRole('button', { name: 'Repeat Publish', exact: true }).click();
  await expect(
    page.getByText('Same register entry reused. No duplicate handoff.', { exact: true }),
  ).toBeVisible();
  await page.reload();
  await expect(
    page.getByRole('heading', { name: 'Simulated handoff confirmed', exact: true }),
  ).toBeVisible();
  assert.deepEqual((await readJson(route)).registerEntry, published.registerEntry);

  for (const width of [1280, 390]) {
    await page.setViewportSize({ width, height: 900 });
    assert(
      await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
      `Horizontal overflow at ${width}px`,
    );
    await page.getByRole('button', { name: 'Try failure recovery', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Website refresh', exact: true })).toBeFocused();
    await expect(page.getByRole('heading', { name: 'Recover a failed welcome' })).toBeVisible();
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    await page.getByRole('button', { name: 'Back to main walkthrough', exact: true }).click();
    await expect(
      page.getByRole('heading', { name: 'Website redesign', exact: true }),
    ).toBeFocused();
    await expect(
      page.getByRole('heading', { name: 'Simulated handoff confirmed', exact: true }),
    ).toBeVisible();
    assert.deepEqual((await readJson(route)).registerEntry, published.registerEntry);
    await page.getByRole('tab', { name: 'Activity', exact: true }).click();
    await page.getByRole('tab', { name: 'Kickoff brief', exact: true }).click();
  }
  assert.deepEqual(errors, []);
  assert.deepEqual(policyErrors, []);
  assert.equal((await readJson(`/release.json?revision=${revision}`)).revision, revision);
  // WebKit's screenshot-only inline synchronization style follows all policy assertions.
  await page.screenshot({ path: `${output}/phone-width.png`, fullPage: true, caret: 'initial' });
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.screenshot({ path: `${output}/desktop.png`, fullPage: true, caret: 'initial' });
  const result = {
    revision,
    platform: process.platform,
    architecture: process.arch,
    darwinRelease: release(),
    browser: 'Playwright WebKit',
    browserVersion: browser.version(),
    liveRevisionVerified: true,
    correctionApprovalAndHandoff: true,
    repeatAndRefresh: true,
    desktopAndPhoneWidth: true,
    deviceScaleFactor: 2,
    applicationCspErrorsDuringWorkflow: 0,
    brandedSafariOrPhysicalIosTested: false,
  };
  await writeFile(`${output}/result.json`, JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
  if (process.env.GITHUB_STEP_SUMMARY)
    await appendFile(
      process.env.GITHUB_STEP_SUMMARY,
      `## Native macOS browser verification\n\nRevision: \`${revision}\`\n\n` +
        `WebKit ${result.browserVersion} on macOS (${process.arch}; Darwin ${result.darwinRelease}). ` +
        `The live correction/approval/handoff, repeat, refresh, desktop/phone-width controls and CSP checks passed. ` +
        `A fresh fictional visitor uses the normal 24-hour sandbox; no privileged credentials or real external writes.\n\n` +
        `This tests WebKit on macOS, not branded Safari or a physical iPhone. Reputation classifications remain separate.\n`,
    );
} catch (error) {
  await page
    .screenshot({ path: `${output}/failure.png`, fullPage: true, caret: 'initial' })
    .catch(() => {});
  throw error;
} finally {
  await context.close();
  await browser.close();
}
