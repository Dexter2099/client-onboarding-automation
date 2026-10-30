import test from 'node:test';
import assert from 'node:assert/strict';
import { webcrypto } from 'node:crypto';
import worker from './_worker.js';

const origin = 'https://dexter-onboardflow.pages.dev';
const pair = await webcrypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, true, [
  'sign',
  'verify',
]);
const env = {
  EDGE_SIGNING_KEY: Buffer.from(
    await webcrypto.subtle.exportKey('pkcs8', pair.privateKey),
  ).toString('base64'),
};
const originalFetch = globalThis.fetch;

test.after(() => {
  globalThis.fetch = originalFetch;
});

test('edge binds method, encoded target and body, preserving session and CSRF headers', async () => {
  let captured;
  globalThis.fetch = async (url, init) => {
    captured = { url: url.toString(), ...init };
    return new Response('ok');
  };
  const body = '{"commandId":"fictional","operation":"edit"}';
  const request = new Request(origin + '/api/intake/command?encoded=a%2Fb', {
    method: 'POST',
    body,
    headers: {
      Cookie: 'owned=fictional',
      'X-CSRF-TOKEN': 'fictional-csrf',
      'X-OnboardFlow-Proto': 'http',
      'X-OnboardFlow-Edge-Signature': 'forged',
      'X-Forwarded-Proto': 'http',
    },
  });
  assert.equal((await worker.fetch(request, env)).status, 200);
  assert.equal(captured.url, 'https://onboardflow.hostless.app/api/intake/command?encoded=a%2Fb');
  assert.equal(captured.headers.get('cookie'), 'owned=fictional');
  assert.equal(captured.headers.get('x-csrf-token'), 'fictional-csrf');
  assert.equal(captured.headers.get('x-forwarded-proto'), null);
  assert.equal(captured.headers.get('x-onboardflow-proto'), null);
  assert.equal(new TextDecoder().decode(captured.body), body);
  const hash = Buffer.from(await webcrypto.subtle.digest('SHA-256', captured.body)).toString('hex');
  const canonical = [
    'v1',
    captured.headers.get('X-OnboardFlow-Edge-Time'),
    origin,
    'POST',
    '/api/intake/command?encoded=a%2Fb',
    hash,
  ].join('\n');
  const signature = Buffer.from(captured.headers.get('X-OnboardFlow-Edge-Signature'), 'base64');
  assert.equal(signature.length, 64);
  assert(
    await webcrypto.subtle.verify(
      { name: 'ECDSA', hash: 'SHA-256' },
      pair.publicKey,
      signature,
      new TextEncoder().encode(canonical),
    ),
  );
});

test('HTTP and alternate hostnames redirect to canonical HTTPS without touching the origin', async () => {
  globalThis.fetch = () => {
    throw new Error('must not forward');
  };
  for (const url of [
    'http://dexter-onboardflow.pages.dev/l/instructions',
    'https://preview.example/l/instructions',
  ]) {
    const r = await worker.fetch(new Request(url), env);
    assert.equal(r.status, 308);
    assert.equal(r.headers.get('location'), origin + '/l/instructions');
    assert.equal(r.headers.get('set-cookie'), null);
  }
});

test('double-slash paths cannot change the fixed upstream hostname', async () => {
  let destination;
  globalThis.fetch = async (url) => {
    destination = url;
    return new Response('ok');
  };
  await worker.fetch(new Request(origin + '//attacker.example/secret'), env);
  assert.equal(destination.origin, 'https://onboardflow.hostless.app');
});

test('oversized mutations are rejected without forwarding', async () => {
  globalThis.fetch = () => {
    throw new Error('must not forward');
  };
  const r = await worker.fetch(
    new Request(origin + '/api/intake/command', { method: 'POST', body: 'x'.repeat(16385) }),
    env,
  );
  assert.equal(r.status, 413);
});

test('origin redirects and uncertain mutations fail visibly, without a loop or automatic retry', async () => {
  let calls = 0;
  globalThis.fetch = async () => {
    calls++;
    return Response.redirect('https://onboardflow.hostless.app/', 308);
  };
  assert.equal((await worker.fetch(new Request(origin), env)).status, 503);
  assert.equal(calls, 1);
  calls = 0;
  globalThis.fetch = async () => {
    calls++;
    throw new Error('connection lost after sending');
  };
  const r = await worker.fetch(
    new Request(origin + '/api/intake/command', { method: 'POST', body: '{}' }),
    env,
  );
  assert.equal(r.status, 503);
  assert.equal(r.headers.get('location'), null);
  assert.equal(calls, 1);
});

test('missing signing configuration fails closed', async () => {
  assert.equal((await worker.fetch(new Request(origin), {})).status, 503);
});
