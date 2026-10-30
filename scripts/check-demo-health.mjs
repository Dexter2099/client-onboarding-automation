import assert from 'node:assert/strict';
import { origin } from './demo-target.mjs';

const paths = ['/', '/release.json', '/api/csrf'];
if (process.argv.includes('--database')) paths.push('/health/ready');
for (const path of paths) {
  const response = await fetch(origin + path, {
    redirect: 'manual',
    cache: 'no-store',
    signal: AbortSignal.timeout(20000),
  });
  assert.equal(response.status, 200, `${path}: expected 200, received ${response.status}`);
  assert.match(response.headers.get('strict-transport-security') ?? '', /^max-age=86400$/);
  if (path === '/api/csrf') {
    assert.equal(typeof (await response.json()).token, 'string');
    const cookie = response.headers.get('set-cookie') ?? '';
    assert.match(cookie, /; secure/i);
    assert.match(cookie, /; httponly/i);
    assert.match(cookie, /; samesite=strict/i);
  } else if (path === '/release.json') {
    const marker = await response.json();
    assert.match(marker.revision, /^[a-f0-9]{40}$/);
    assert.equal(marker.repository, 'https://github.com/Dexter2099/client-onboarding-automation');
  } else if (path === '/') assert.match(await response.text(), /OnboardFlow/);
}
console.log(
  JSON.stringify({
    origin,
    checkedAt: new Date().toISOString(),
    paths,
    redirectLoops: false,
    aiCalls: 0,
  }),
);
