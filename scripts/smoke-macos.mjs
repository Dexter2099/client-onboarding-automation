import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { appendFile, mkdir, writeFile } from 'node:fs/promises';
import { release } from 'node:os';
import { origin } from './demo-target.mjs';

assert.equal(process.platform, 'darwin', 'Native macOS verification requires its macOS runner.');
const revision = process.env.GITHUB_SHA;
assert.match(revision ?? '', /^[a-f0-9]{40}$/);
const output = '.local/macos-browser-evidence';
await mkdir(output, { recursive: true });
const marker = await fetch(`${origin}/release.json?revision=${revision}`, {
  redirect: 'manual',
  signal: AbortSignal.timeout(20000),
  cache: 'no-store',
});
const location = marker.headers.get('location');
const redirect = location ? new URL(location, origin) : null;
assert.equal(
  marker.status,
  200,
  `Release marker status ${marker.status}; redirect ${redirect ? redirect.origin + redirect.pathname : 'none'}`,
);
assert.equal((await marker.json()).revision, revision);
// One real public visitor, using the same persisted request/source/reply/submission journey.
// This has no provider key or privileged production credential. Inference uses the app's shared cap.
execFileSync(
  process.execPath,
  [
    'node_modules/@playwright/test/cli.js',
    'test',
    '--config=preview/content-snare/playwright.config.ts',
    '--grep=no-typing request',
  ],
  {
    stdio: 'inherit',
    timeout: 180000,
    env: {
      ...process.env,
      ONBOARDFLOW_LIVE_ORIGIN: origin,
      ONBOARDFLOW_BROWSER: 'webkit',
      ONBOARDFLOW_EVIDENCE_DIR: output,
    },
  },
);
const result = {
  revision,
  platform: process.platform,
  architecture: process.arch,
  darwinRelease: release(),
  browser: 'Playwright WebKit',
  realHostedAi: true,
  sourcesAndClarification: true,
  humanOnlyManualActionsAndSubmissions: true,
  savedProgressAndReload: true,
  originalLocalRecordingPlayback: true,
  brandedSafariOrPhysicalIosTested: false,
};
await writeFile(`${output}/result.json`, JSON.stringify(result, null, 2));
console.log(JSON.stringify(result));
if (process.env.GITHUB_STEP_SUMMARY)
  await appendFile(
    process.env.GITHUB_STEP_SUMMARY,
    `## Native macOS hosted intake verification\n\nRevision: ${revision}\n\n` +
      'WebKit verified real hosted AI, missing-detail clarification, source inspection, manual actions, human submission and saved reload. ' +
      'This is WebKit on macOS; branded Safari and a physical iPhone were not tested.\n',
  );
