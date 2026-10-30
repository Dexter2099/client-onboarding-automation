import assert from 'node:assert/strict';
import { origin } from './demo-target.mjs';

export async function verifyHostlessIngress(revision) {
  assert.match(revision, /^[a-f0-9]{40}$/);
  const forged = (scheme) => ({
    'X-Forwarded-Proto': scheme,
    'CF-Visitor': JSON.stringify({ scheme }),
    'X-OnboardFlow-Proto': scheme,
  });
  const cases = [
    { name: 'https', secure: true, headers: {} },
    { name: 'https-forged', secure: true, headers: forged('http') },
    { name: 'http', secure: false, headers: {} },
    { name: 'http-forged', secure: false, headers: forged('https') },
  ];
  for (const probe of cases) {
    const url = probe.secure
      ? `${origin}/release.json?revision=${revision}&ingress=${probe.name}`
      : origin.replace('https:', 'http:') + '/api/csrf';
    // No credentials or cookies are sent; observe redirects without following around the gate.
    const response = await fetch(url, {
      headers: probe.headers,
      redirect: 'manual',
      cache: 'no-store',
      signal: AbortSignal.timeout(20000),
    });
    const location = response.headers.get('location');
    const target = location ? new URL(location, origin) : null;
    // Pages redirects HTTP before the Worker runs; HTTPS must reach the app without a redirect.
    assert(
      probe.secure ? response.status === 200 : response.status === 301,
      `${probe.name}: status ${response.status}, redirect ${target ? target.origin + target.pathname : 'none'}`,
    );
    if (probe.secure) {
      const marker = await response.json();
      assert.equal(marker.revision, revision);
      assert.equal(marker.repository, 'https://github.com/Dexter2099/client-onboarding-automation');
    } else {
      assert.equal(location, origin + '/api/csrf');
      assert.equal(response.headers.get('set-cookie'), null);
    }
  }
  return cases.length;
}
