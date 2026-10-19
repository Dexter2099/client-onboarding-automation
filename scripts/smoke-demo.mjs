import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';

const origin = 'https://dexter-onboardflow.de.deplexo.com';
const repository = 'https://github.com/Dexter2099/client-onboarding-automation';

export async function smokeDemo(revision) {
  assert.match(revision, /^[a-f0-9]{40}$/);
  let markerVerified = false;
  for (let attempt = 0; attempt < 12; attempt++) {
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
  async function call(path, method = 'GET', csrf = true, expected = 200) {
    const headers = { Cookie: [...cookies].map(([name, value]) => `${name}=${value}`).join('; ') };
    if (method === 'POST' && csrf) headers['X-CSRF-TOKEN'] = token;
    const response = await fetch(origin + path, {
      method,
      headers,
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
  const projects = await call('/api/projects');
  assert.equal(projects.length, 2);
  const project = projects.find((item) => item.reference === 'OF-1042');
  assert(project, 'The prepared project is missing.');
  const route = `/api/projects/${project.id}`;
  const started = await call(`${route}/start`, 'POST');
  const repeated = await call(`${route}/start`, 'POST');
  assert.equal(repeated.onboarding.id, started.onboarding.id);
  assert.equal(repeated.onboarding.welcomeAttempts.length, 1);
  const saved = await call(`${route}/onboarding`);
  assert.equal(saved.onboarding.id, started.onboarding.id);
  assert.equal(saved.outbox.length, 1);
  return {
    revision,
    liveRevisionVerified: true,
    secureCookies: true,
    csrf: true,
    savedWorkflowAndRepeat: true,
    fixedLifetime: true,
    browserSecurityHeaders: true,
    requests,
  };
}
