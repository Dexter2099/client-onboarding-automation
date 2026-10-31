import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, sep } from 'node:path';

const evidence = process.env.ONBOARDFLOW_EVIDENCE_DIR
  ? resolve(process.env.ONBOARDFLOW_EVIDENCE_DIR) + sep
  : fileURLToPath(new URL('../../../.local/content-snare-evidence/', import.meta.url));
mkdirSync(evidence, { recursive: true });
const saved = async (page: Page) => (await page.request.get('/api/intake/')).json();
async function fill(page: Page) {
  await page.getByRole('button', { name: 'FILL WITH AI', exact: true }).click();
  await expect(page.getByText('Not found in the client documents.', { exact: true })).toBeVisible({
    timeout: 120000,
  });
}
async function reply(page: Page) {
  await page.getByRole('button', { name: 'USE SAMPLE CLIENT REPLY', exact: true }).click();
  await page.getByRole('button', { name: 'CONTINUE AI FILLING', exact: true }).click();
  await expect
    .poll(async () => (await saved(page)).answers.tax?.value, { timeout: 120000 })
    .toBe('DEMO-EIN-1042');
  await page.getByRole('button', { name: /^Tax ID \(EIN\) draft/ }).click();
  await expect(page.getByRole('textbox', { name: 'Tax ID (EIN)', exact: true })).toHaveValue(
    'DEMO-EIN-1042',
  );
}
async function noOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(
    true,
  );
}

test('startup requests session information while the CSRF request is still pending', async ({
  page,
}) => {
  let release!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  let sessionRequested = false;
  await page.route('**/api/csrf', async (route) => {
    await held;
    await route.continue();
  });
  await page.route('**/api/session', async (route) => {
    if (route.request().method() === 'GET') sessionRequested = true;
    await route.continue();
  });
  try {
    await page.goto('/l/instructions');
    await expect.poll(() => sessionRequested).toBe(true);
  } finally {
    release();
  }
  await expect.poll(async () => (await saved(page)).version).toBeGreaterThan(0);
});

test('rapid edits retain the latest value and finish saving before submission', async ({
  page,
}) => {
  await page.goto('/l/general-information');
  await expect.poll(async () => (await saved(page)).version).toBeGreaterThan(0);
  let release!: () => void;
  let entered!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const started = new Promise<void>((resolve) => {
    entered = resolve;
  });
  const edits: string[] = [];
  await page.route('**/api/intake/command', async (route) => {
    const command = route.request().postDataJSON();
    if (command.operation === 'edit') {
      edits.push(command.value);
      if (edits.length === 1) {
        entered();
        await held;
      }
    }
    await route.continue();
  });
  const input = page.getByRole('textbox', { name: 'Legal Company Name', exact: true });
  await input.fill('J');
  await started;
  await input.pressSequentially('uniper Review Studio');
  await page.getByRole('button', { name: 'SUBMIT FOR REVIEW', exact: true }).click();
  release();
  await expect.poll(async () => (await saved(page)).answers.legal?.status).toBe('submitted');
  expect(edits).toEqual(['J', 'Juniper Review Studio']);
  expect((await saved(page)).answers.legal.value).toBe('Juniper Review Studio');
  await expect(page.getByRole('textbox', { name: 'DBA Name', exact: true })).toBeVisible();
});

test('unchanged AI polls do not add duplicate navigation history', async ({ page }) => {
  await page.goto('/l/general-information');
  await expect.poll(async () => (await saved(page)).version).toBeGreaterThan(0);
  let release!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  let progress: Record<string, unknown> | undefined;
  let polls = 0;
  await page.route('**/api/intake/agent', async (route) => {
    const response = await route.fetch();
    const final = await response.json();
    progress = {
      ...final,
      version: final.version - 1,
      ai: 'filling',
      answers: Object.fromEntries(Object.entries(final.answers).slice(0, 8)),
      run: { ...final.run, status: 'filling', question: null },
    };
    await held;
    await route.fulfill({ response, json: final });
  });
  await page.route('**/api/intake/', async (route) => {
    if (!progress) return route.continue();
    polls++;
    await route.fulfill({ json: progress });
  });
  try {
    await page.getByRole('button', { name: 'FILL WITH AI', exact: true }).click();
    await expect.poll(() => polls).toBeGreaterThanOrEqual(1);
    await expect(
      page.getByRole('article', { name: 'Date Business Started', exact: true }),
    ).toBeVisible();
    const history = await page.evaluate(() => window.history.length);
    await expect.poll(() => polls).toBeGreaterThanOrEqual(3);
    expect(await page.evaluate(() => window.history.length)).toBe(history);
  } finally {
    progress = undefined;
    release();
  }
  await expect(page.getByText('Not found in the client documents.', { exact: true })).toBeVisible();
});

test('reset is available before completion on desktop and mobile and cancel preserves saved work', async ({
  page,
}) => {
  await page.goto('/l/instructions');
  await expect(page.getByRole('button', { name: 'Reset demo', exact: true })).toBeVisible();
  await page.getByRole('button', { name: "LET'S GET STARTED", exact: true }).click();
  await page
    .getByRole('textbox', { name: 'Legal Company Name', exact: true })
    .fill('Fictional reset example');
  await expect
    .poll(async () => (await saved(page)).answers?.legal?.value)
    .toBe('Fictional reset example');
  await page.getByRole('button', { name: 'Reset demo', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Reset demo?', exact: true });
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  expect((await saved(page)).answers.legal.value).toBe('Fictional reset example');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole('button', { name: 'Reset demo', exact: true }).click();
  await dialog.getByRole('button', { name: 'Reset demo', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Welcome to your new client onboarding!', exact: true }),
  ).toBeVisible();
  await expect.poll(async () => Object.keys((await saved(page)).answers).length).toBe(0);
  await expect(page.getByRole('alert')).toHaveCount(0);
  await noOverflow(page);
});

test('reset interrupts a held save and discards queued edits without restoring old answers', async ({
  page,
}) => {
  await page.goto('/l/general-information');
  await saved(page);
  let release!: () => void;
  let entered!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const started = new Promise<void>((resolve) => {
    entered = resolve;
  });
  let sentEdits = 0;
  await page.route('**/api/intake/command', async (route) => {
    if (route.request().postDataJSON().operation !== 'edit') return route.continue();
    sentEdits++;
    entered();
    await held;
    await route.abort().catch(() => {});
  });
  const input = page.getByRole('textbox', { name: 'Legal Company Name', exact: true });
  await input.fill('Old value');
  await started;
  await input.fill('Queued old value');
  await page.getByRole('button', { name: 'Reset demo', exact: true }).click();
  await page
    .getByRole('dialog', { name: 'Reset demo?', exact: true })
    .getByRole('button', { name: 'Reset demo', exact: true })
    .click();
  await expect(
    page.getByRole('heading', { name: 'Welcome to your new client onboarding!', exact: true }),
  ).toBeVisible();
  release();
  await expect.poll(async () => Object.keys((await saved(page)).answers).length).toBe(0);
  expect(sentEdits).toBe(1);
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('no-typing request: supported drafts, explicit clarification, manual actions and submission', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/l/instructions');
  await expect(
    page.getByRole('img', {
      name: 'Source-linked answers in the recorded local walkthrough',
      exact: true,
    }),
  ).toHaveAttribute('src', '/onboardflow-local-ai-20261025.webp');
  await page.evaluate(() => document.fonts.ready);
  await page.screenshot({
    path: evidence + 'instructions-desktop.png',
    fullPage: true,
    caret: 'initial',
  });
  await page.getByRole('button', { name: 'Play walkthrough', exact: true }).click();
  await page.locator('video').evaluate(async (video: HTMLVideoElement) => {
    await video.play();
    video.pause();
  });
  await page.getByRole('button', { name: "LET'S GET STARTED", exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('textbox', { name: 'Legal Company Name', exact: true })).toHaveValue(
    '',
  );
  await expect(page.getByRole('button', { name: /3\. Onboarding Task List/ })).toBeInViewport();
  await page.getByRole('button', { name: 'Client information pack', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Selected passage', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Onboarding notes', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Client onboarding notes', exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'Client reply', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Close source document', exact: true }).click();
  await page.screenshot({ path: evidence + 'general-empty.png', fullPage: true, caret: 'initial' });
  await fill(page);
  const drafts = await saved(page);
  expect(Object.keys(drafts.answers)).toHaveLength(18);
  expect(
    Object.values(drafts.answers).every(
      (answer: unknown) => (answer as { status: string }).status === 'draft',
    ),
  ).toBe(true);
  for (const id of ['tax', 'tax-return', 'folder', 'payment', 'ach'])
    expect(drafts.answers[id]).toBeUndefined();
  await expect(page.getByRole('textbox', { name: 'Tax ID (EIN)', exact: true })).toHaveValue('');
  await page.screenshot({
    path: evidence + 'missing-information.png',
    fullPage: true,
    caret: 'initial',
  });
  await reply(page);
  expect((await saved(page)).answers.tax.evidence.document).toBe('reply');
  await page.getByRole('button', { name: /^Legal Company Name draft/ }).click();
  await page.getByRole('button', { name: /Client company profile.*View source/ }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.locator('.source-passage.highlighted')).toHaveText(
    /Our legal company name is Juniper Studio LLC/,
  );
  await page.screenshot({
    path: evidence + 'source-document.png',
    fullPage: true,
    caret: 'initial',
  });
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await page.getByRole('button', { name: 'Page Summary', exact: true }).click();
  await page.getByRole('button', { name: 'SUBMIT PAGE FOR REVIEW', exact: true }).click();
  await expect(page.getByRole('button', { name: /2\. General Information 11\/11/ })).toBeVisible();
  await page.getByRole('button', { name: /Go to next page/ }).click();
  await page.getByRole('button', { name: 'CONTINUE', exact: true }).click();
  await page.screenshot({ path: evidence + 'tasks-desktop.png', fullPage: true, caret: 'initial' });
  await page
    .getByRole('button', { name: /^Please upload your most recent business tax return empty/ })
    .click();
  await page.getByRole('button', { name: 'Use sample attachment', exact: true }).click();
  await page.getByRole('button', { name: /^Access Your Shared Google Drive Folder empty/ }).click();
  await page
    .getByRole('checkbox', { name: 'I have accessed the sample client folder.', exact: true })
    .check();
  await page.getByRole('button', { name: /^Complete First Payment empty/ }).click();
  await page
    .getByRole('checkbox', { name: 'Mark the fictional first payment as complete.', exact: true })
    .check();
  await page.getByRole('button', { name: /^Authorize ACH Payments empty/ }).click();
  for (const name of [
    'Sample email opened',
    'Sample account step completed',
    'Sample verification completed',
  ]) {
    await page.getByRole('checkbox', { name, exact: true }).check();
  }
  await page.getByRole('button', { name: 'Page Summary', exact: true }).click();
  await page.getByRole('button', { name: 'SUBMIT PAGE FOR REVIEW', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Your request is ready for review' }),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: /3\. Onboarding Task List 12\/12/ })).toBeVisible();
  const completed = await saved(page);
  expect(Object.keys(completed.answers)).toHaveLength(23);
  expect(
    Object.values(completed.answers).every(
      (answer: unknown) => (answer as { status: string }).status === 'submitted',
    ),
  ).toBe(true);
  await page.screenshot({
    path: evidence + 'completed-desktop.png',
    fullPage: true,
    caret: 'initial',
  });
  await page.reload();
  await expect(page.getByRole('button', { name: /3\. Onboarding Task List 12\/12/ })).toBeVisible();
  await expect(page.getByRole('button', { name: 'FILL WITH AI', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Page Summary', exact: true }).click();
  await page.getByRole('button', { name: 'Restart fictional request', exact: true }).click();
  await page
    .getByRole('dialog', { name: 'Reset demo?', exact: true })
    .getByRole('button', { name: 'Reset demo', exact: true })
    .click();
  // Navigation follows the committed reset; the click alone only dispatches its async command.
  await expect(page).toHaveURL(/\/l\/instructions$/);
  expect(Object.keys((await saved(page)).answers)).toHaveLength(0);
  expect(errors).toEqual([]);
});

test('editing clears provenance and submission; invalid values and questions remain local', async ({
  page,
}) => {
  await page.goto('/l/general-information');
  await fill(page);
  await page.getByRole('button', { name: /^Legal Company Name draft/ }).click();
  await page
    .getByRole('textbox', { name: 'Legal Company Name', exact: true })
    .fill('Reviewer correction LLC');
  await expect(page.locator('.source-chip')).toHaveCount(0);
  await expect.poll(async () => (await saved(page)).answers.legal.evidence).toBeNull();
  await page.getByRole('button', { name: 'SUBMIT FOR REVIEW', exact: true }).click();
  await page.getByRole('button', { name: /^Legal Company Name submitted/ }).click();
  await page.getByRole('button', { name: 'EDIT ANSWER', exact: true }).click();
  await expect.poll(async () => (await saved(page)).answers.legal.status).toBe('draft');
  await page.getByRole('button', { name: /^Email draft/ }).click();
  await page.getByRole('textbox', { name: 'Email', exact: true }).fill('invalid');
  await page.getByRole('button', { name: 'SUBMIT FOR REVIEW', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveText('Add a valid answer before submitting.');
  await page.getByRole('button', { name: 'Save draft and continue', exact: true }).click();
  expect((await saved(page)).answers.email.status).toBe('draft');
  await page.getByRole('button', { name: 'ASK A QUESTION', exact: true }).click();
  await page.getByLabel('Question about this item', { exact: true }).fill('Which entity applies?');
  await page.getByRole('button', { name: 'SAVE QUESTION', exact: true }).click();
  await expect(
    page.getByText('Question saved in this demo. No message was sent.', { exact: true }),
  ).toBeVisible();
  await page.reload();
  await page.getByRole('button', { name: /^Entity draft/ }).click();
  await expect(page.getByText('Which entity applies?', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: /^Legal Company Name draft/ }).click();
  await expect(page.getByRole('textbox', { name: 'Legal Company Name', exact: true })).toHaveValue(
    'Reviewer correction LLC',
  );
});

for (const width of [390, 320]) {
  test('same request and source inspection fit ' + width + 'px', async ({ page }) => {
    await page.setViewportSize({ width, height: 844 });
    await page.goto('/l/instructions');
    await noOverflow(page);
    await page.getByRole('button', { name: "LET'S GET STARTED", exact: true }).click();
    await fill(page);
    await noOverflow(page);
    await page.screenshot({
      path: evidence + 'missing-phone-' + width + '.png',
      fullPage: true,
      caret: 'initial',
    });
    await page.getByRole('button', { name: 'Open request navigation', exact: true }).click();
    await page.getByRole('button', { name: /^Legal Company Name draft/ }).click();
    await expect(
      page.getByRole('textbox', { name: 'Legal Company Name', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: /Client company profile.*View source/ }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await noOverflow(page);
    await page.screenshot({
      path: evidence + 'source-phone-' + width + '.png',
      fullPage: true,
      caret: 'initial',
    });
    await page.getByRole('button', { name: 'Close source document', exact: true }).click();
    expect(
      await page
        .getByRole('textbox', { name: 'Legal Company Name', exact: true })
        .evaluate((input) => parseFloat(getComputedStyle(input).fontSize)),
    ).toBeGreaterThanOrEqual(16);
  });
}
