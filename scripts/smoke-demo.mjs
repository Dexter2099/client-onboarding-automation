import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';
import { randomUUID } from 'node:crypto';
import { checkedOrigin } from './demo-target.mjs';

const repository = 'https://github.com/Dexter2099/client-onboarding-automation';

export async function smokeDemo(revision, target) {
  const origin = checkedOrigin(target);
  assert.match(revision, /^[a-f0-9]{40}$/);
  let markerVerified = false;
  const deadline = Date.now() + 8 * 60000;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${origin}/release.json?revision=${revision}`, {
        signal: AbortSignal.timeout(10000),
        redirect: 'error',
        cache: 'no-store',
      });
      if (response.ok) {
        const marker = await response.json();
        if (marker.revision === revision && marker.repository === repository) {
          markerVerified = true;
          break;
        }
      }
    } catch {
      /* Read-only readiness probes may retry while the container is replaced. */
    }
    await delay(5000);
  }
  assert(markerVerified, 'The expected Git revision is not live.');

  const cookies = new Map();
  let token;
  let requests = 0;
  function checkBrowserHeaders(response) {
    const policy = response.headers.get('content-security-policy') ?? '';
    for (const rule of [
      "default-src 'none'",
      "script-src 'self'",
      "connect-src 'self'",
      "media-src 'self'",
      "frame-ancestors 'none'",
      "base-uri 'none'",
    ])
      assert(
        policy.split(';').some((part) => part.trim() === rule),
        `Missing CSP directive: ${rule}`,
      );
    assert(!/unsafe-inline|unsafe-eval/.test(policy));
    assert.match(response.headers.get('strict-transport-security') ?? '', /^max-age=86400$/);
  }
  const document = await fetch(origin + '/', {
    redirect: 'error',
    signal: AbortSignal.timeout(20000),
  });
  requests++;
  assert.equal(document.status, 200);
  checkBrowserHeaders(document);
  assert.match(document.headers.get('cache-control') ?? '', /no-transform/);
  const html = await document.text();
  for (const [, source] of html.matchAll(/<script\b[^>]*\bsrc=["']([^"']+)["']/gi))
    assert.equal(
      new URL(source, origin).origin,
      origin,
      'An external script was injected into the document.',
    );
  async function call(path, method = 'GET', csrf = true, expected = 200, body) {
    const headers = { Cookie: [...cookies].map(([name, value]) => `${name}=${value}`).join('; ') };
    if (method === 'POST' && csrf) headers['X-CSRF-TOKEN'] = token;
    if (body) headers['Content-Type'] = 'application/json';
    const response = await fetch(origin + path, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
      redirect: 'error',
      signal: AbortSignal.timeout(20000),
    });
    requests++;
    checkBrowserHeaders(response);
    for (const cookie of response.headers.getSetCookie()) {
      const [pair] = cookie.split(';');
      const separator = pair.indexOf('=');
      if (pair.slice(separator + 1)) {
        assert.match(cookie, /; secure/i);
        assert.match(cookie, /; httponly/i);
        assert.match(cookie, /; samesite=strict/i);
      }
      cookies.set(pair.slice(0, separator), pair.slice(separator + 1));
    }
    assert.equal(response.status, expected, `${method} ${path}: unexpected status`);
    assert.match(response.headers.get('cache-control'), /no-store/);
    return response.json();
  }
  token = (await call('/api/csrf')).token;
  await call('/api/session', 'POST', false, 403);
  const session = await call('/api/session', 'POST', true, 201);
  assert.equal(Date.parse(session.expiresAt) - Date.parse(session.createdAt), 86400000);
  const intake = await call('/api/intake/');
  assert.deepEqual(intake.answers, {});
  const command = {
    commandId: randomUUID(),
    expectedVersion: intake.version,
    operation: 'edit',
    field: 'dba',
    value: 'Fictional release verification',
  };
  const edited = await call('/api/intake/command', 'POST', true, 200, command);
  const repeated = await call('/api/intake/command', 'POST', true, 200, command);
  assert.equal(edited.version, repeated.version);
  const saved = await call('/api/intake/');
  assert.equal(saved.answers.dba.value, command.value);
  assert.equal(saved.answers.dba.author, 'you');
  assert.equal(saved.answers.dba.evidence, null);
  const media = await fetch(origin + '/onboardflow-local-ai-20261025.mp4', {
    headers: { Range: 'bytes=0-15' },
    signal: AbortSignal.timeout(20000),
    redirect: 'error',
  });
  assert.equal(media.status, 206);
  assert.match(media.headers.get('content-type') ?? '', /^video\/mp4/);
  return {
    revision,
    liveRevisionVerified: true,
    secureCookies: true,
    csrf: true,
    savedIntakeAndCommandReplay: true,
    localVideoAndRangeRequests: true,
    fixedLifetime: true,
    browserSecurityHeaders: true,
    requests,
  };
}
