import assert from 'node:assert/strict';
import { chromium } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import { performance } from 'node:perf_hooks';
import { origin } from './demo-target.mjs';

const phase = process.env.ONBOARDFLOW_PERFORMANCE_PHASE ?? 'warm';
assert(['warm', 'after-idle'].includes(phase));
const output = `.local/hostless-performance/${phase}`;
await mkdir(output, { recursive: true });
const browser = await chromium.launch();
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const startedAt = new Date().toISOString();
  const start = performance.now();
  const initialData = page.waitForResponse(
    (response) => new URL(response.url()).pathname === '/api/intake/' && response.status() === 200,
    { timeout: 30000 },
  );
  const navigation = await page.goto(origin + '/l/instructions', { waitUntil: 'domcontentloaded' });
  assert.equal(navigation.status(), 200);
  const startButton = page.getByRole('button', { name: "LET'S GET STARTED", exact: true });
  await startButton.waitFor({ state: 'visible' });
  const shellVisibleMs = performance.now() - start;
  const initialResponse = await initialData;
  await initialResponse.finished();
  assert.equal((await initialResponse.json()).answers.legal, undefined);
  await startButton.click();
  const legal = page.getByRole('textbox', { name: 'Legal Company Name', exact: true });
  await legal.waitFor({ state: 'visible' });
  assert(await legal.isEnabled());
  const usableDataMs = performance.now() - start;
  const commandStart = performance.now();
  const command = page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === '/api/intake/command' &&
      response.request().method() === 'POST',
  );
  const value = 'Fictional employer demo speed check LLC';
  await legal.fill(value);
  const saved = await command;
  assert.equal(saved.status(), 200);
  assert.equal((await saved.json()).answers.legal.value, value);
  const firstSavedActionMs = performance.now() - commandStart;
  const timings = await page.evaluate(() => {
    const entry = performance.getEntriesByType('navigation')[0];
    return {
      ttfbMs: entry.responseStart - entry.startTime,
      documentCompleteMs: entry.responseEnd - entry.startTime,
      domContentLoadedMs: entry.domContentLoadedEventEnd - entry.startTime,
    };
  });
  await page.screenshot({ path: `${output}/usable-request.png`, fullPage: true });
  assert.deepEqual(errors, []);
  await context.close();
  const result = {
    phase,
    origin,
    startedAt,
    lastVisitorTrafficAt: new Date().toISOString(),
    freshBrowserContext: true,
    cachedAssets: false,
    aiCalls: 0,
    shellVisibleMs,
    usableDataMs,
    firstSavedActionMs,
    ...timings,
    databaseBackedActionVerified: true,
  };
  await writeFile(`${output}/result.json`, JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} finally {
  await browser.close();
}
