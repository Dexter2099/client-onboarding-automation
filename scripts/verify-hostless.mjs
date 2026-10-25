import assert from 'node:assert/strict';
import { writeFile, appendFile } from 'node:fs/promises';
import { smokeDemo } from './smoke-demo.mjs';
import { origin } from './demo-target.mjs';

assert.equal(process.env.GITHUB_REPOSITORY, 'Dexter2099/client-onboarding-automation');
assert.equal(process.env.GITHUB_REF, 'refs/heads/main');
const result = await smokeDemo(process.env.GITHUB_SHA);
await writeFile('.local/deployment-result.json', JSON.stringify({ ...result, origin }, null, 2));
console.log(JSON.stringify(result));
if (process.env.GITHUB_STEP_SUMMARY)
  await appendFile(
    process.env.GITHUB_STEP_SUMMARY,
    `Hostless served verified revision ${process.env.GITHUB_SHA}. HTTPS, secure cookies, CSRF, saved request/command replay, expiry and video range checks passed. [Open demo](${origin}).\n`,
  );
