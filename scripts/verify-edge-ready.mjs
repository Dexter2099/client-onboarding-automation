import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { origin } from './demo-target.mjs';

assert.equal(process.env.GITHUB_REPOSITORY, 'Dexter2099/client-onboarding-automation');
assert.equal(process.env.GITHUB_REF, 'refs/heads/main');
const revision = process.env.GITHUB_SHA;
assert.match(revision, /^[a-f0-9]{40}$/);
const expectedGateway = createHash('sha256')
  .update(await readFile(new URL('../edge/_worker.js', import.meta.url)))
  .digest('hex');
const deadline = Date.now() + 5 * 60000;
console.log(
  `Waiting for checked gateway code ${expectedGateway} before publishing Hostless source.`,
);
let ready = false;
while (Date.now() < deadline) {
  try {
    const response = await fetch(
      origin + '/onboardflow-local-ai-20261025.webp?gateway=' + expectedGateway,
      {
        redirect: 'error',
        cache: 'no-store',
        signal: AbortSignal.timeout(10000),
      },
    );
    ready =
      response.ok &&
      response.headers.get('X-OnboardFlow-Asset-Source') === 'pages' &&
      response.headers.get('X-OnboardFlow-Gateway-Hash') === expectedGateway;
    await response.body?.cancel();
    if (ready) break;
  } catch {
    /* Read-only readiness may wait for the checked gateway promotion. */
  }
  await delay(5000);
}
assert(ready, 'The checked gateway is not live; Hostless source was not published.');
console.log('Checked gateway is live. The verified Hostless package may now be published.');
