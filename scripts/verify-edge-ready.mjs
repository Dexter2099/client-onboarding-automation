import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';
import { origin } from './demo-target.mjs';

assert.equal(process.env.GITHUB_REPOSITORY, 'Dexter2099/client-onboarding-automation');
assert.equal(process.env.GITHUB_REF, 'refs/heads/main');
const revision = process.env.GITHUB_SHA;
assert.match(revision, /^[a-f0-9]{40}$/);
const deadline = Date.now() + 5 * 60000;
console.log(`Waiting for the checked gateway ${revision} before publishing Hostless source.`);
let ready = false;
while (Date.now() < deadline) {
  try {
    const response = await fetch(
      origin + '/onboardflow-local-ai-20261025.webp?gateway=' + revision,
      {
        redirect: 'error',
        cache: 'no-store',
        signal: AbortSignal.timeout(10000),
      },
    );
    ready =
      response.ok &&
      response.headers.get('X-OnboardFlow-Asset-Source') === 'pages' &&
      response.headers.get('X-OnboardFlow-Asset-Revision') === revision;
    await response.body?.cancel();
    if (ready) break;
  } catch {
    /* Read-only readiness may wait for the checked gateway promotion. */
  }
  await delay(5000);
}
assert(ready, 'The checked gateway is not live; Hostless source was not published.');
console.log('Checked gateway is live. The verified Hostless package may now be published.');
