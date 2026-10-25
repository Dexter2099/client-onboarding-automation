import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { appendFile, readFile, writeFile } from 'node:fs/promises';
import { setTimeout as delay } from 'node:timers/promises';
import { smokeDemo } from './smoke-demo.mjs';

const apiOrigin = 'https://deplexo.com/user/api/v1';
const appId = 'b1ca1ebf-42b4-4e0c-9589-6c0aa46c562f';
const revision = process.env.GITHUB_SHA;
assert.match(revision ?? '', /^[a-f0-9]{40}$/);
assert.equal(process.env.GITHUB_REPOSITORY, 'Dexter2099/client-onboarding-automation');
assert.equal(process.env.GITHUB_REF, 'refs/heads/main', 'Only main may deploy.');
assert(process.env.DEPLEXO_TOKEN, 'Configure the production DEPLEXO_TOKEN secret.');

async function api(path, method = 'GET', body, extraHeaders = {}) {
  const headers = { Authorization: `Bearer ${process.env.DEPLEXO_TOKEN}`, ...extraHeaders };
  const response = await fetch(apiOrigin + path, {
    method,
    headers,
    body,
    redirect: 'error',
    signal: AbortSignal.timeout(30000),
  });
  // Never print response bodies or request headers: provider errors can include credentials.
  assert(response.ok, `Deplexo ${method} ${path} failed with HTTP ${response.status}.`);
  return response.json();
}

const manifest = JSON.parse(await readFile('.local/release/release-manifest.json', 'utf8'));
assert.equal(manifest.revision, revision);
assert.equal(manifest.file, `onboardflow-${revision}.zip`);
const source = await readFile(`.local/release/${manifest.file}`);
assert.equal(createHash('sha256').update(source).digest('hex'), manifest.sha256);
const app = (await api(`/apps/${appId}`)).app;
assert.equal(app.id, appId);
assert.equal(app.name, 'dexter-onboardflow');
assert.equal(app.autoDeploy, false, 'Provider push deployment must not bypass CI.');

const uploaded = await api('/sources', 'POST', source, {
  'Content-Type': 'application/zip',
  'Content-Length': String(source.length),
  'Content-Disposition': `attachment; filename="${manifest.file}"`,
});
assert.match(uploaded.id, /^[a-f0-9-]{36}$/);
assert.equal(uploaded.sha256.toLowerCase(), manifest.sha256);
// Submit once. A lost response requires inspecting deployment history, never blind replay.
const accepted = await api(
  `/apps/${appId}/restart`,
  'POST',
  JSON.stringify({ sourceId: uploaded.id }),
  {
    'Content-Type': 'application/json',
  },
);
assert.match(accepted.deploymentId, /^[a-f0-9-]{36}$/);
console.log(`Deployment ${accepted.deploymentId} accepted for revision ${revision}.`);

let complete = false;
for (let attempt = 0; attempt < 48; attempt++) {
  const history = await api(`/apps/${appId}/deployments?limit=20&offset=0`);
  const deployment = history.data.find((item) => item.id === accepted.deploymentId);
  assert(deployment, 'The accepted deployment is missing from history.');
  assert(
    !['failed', 'cancelled', 'canceled'].includes(deployment.status),
    'Deplexo deployment did not succeed.',
  );
  if (deployment.status === 'success') {
    complete = true;
    break;
  }
  await delay(10000);
}
assert(
  complete,
  'Deployment did not finish within eight minutes. Inspect its history before retrying.',
);
const smoke = await smokeDemo(revision, 'https://dexter-onboardflow.de.deplexo.com');
const result = { ...smoke, deploymentId: accepted.deploymentId, sourceSha256: manifest.sha256 };
await writeFile('.local/deployment-result.json', JSON.stringify(result, null, 2));
console.log(JSON.stringify(result));
if (process.env.GITHUB_STEP_SUMMARY) {
  await appendFile(
    process.env.GITHUB_STEP_SUMMARY,
    `## Verified production release\n\nCommit: \`${revision}\`\n\n` +
      `Source SHA-256: \`${manifest.sha256}\`\n\nDeployment: \`${accepted.deploymentId}\`\n\n` +
      `[Open demo](https://dexter-onboardflow.de.deplexo.com/) · Live revision, CSP/HSTS headers, secure cookies, CSRF, saved intake/command replay, video and 24-hour lifetime passed.\n`,
  );
}
