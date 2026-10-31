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

test('public assets use Pages with security/cache headers and no visitor credentials or origin request', async () => {
  globalThis.fetch = () => {
    throw new Error('must not contact origin');
  };
  let captured;
  const response = await worker.fetch(
    new Request(origin + '/assets/index-Abcd1234.js', {
      headers: {
        Cookie: 'owned=fictional',
        Authorization: 'fictional',
        'X-CSRF-TOKEN': 'fictional',
        'If-None-Match': 'etag',
      },
    }),
    {
      ...env,
      ASSETS: {
        fetch: async (request) => {
          captured = request;
          return new Response('export {}', {
            headers: {
              'Content-Type': 'application/javascript',
              ETag: 'etag',
              'Set-Cookie': 'unexpected=fictional',
            },
          });
        },
      },
    },
  );
  assert.equal(await response.text(), 'export {}');
  assert.equal(captured.headers.get('cookie'), null);
  assert.equal(captured.headers.get('authorization'), null);
  assert.equal(captured.headers.get('x-csrf-token'), null);
  assert.equal(captured.headers.get('if-none-match'), 'etag');
  assert.equal(response.headers.get('set-cookie'), null);
  assert.equal(response.headers.get('x-onboardflow-asset-source'), 'pages');
  assert.equal(response.headers.get('x-content-type-options'), 'nosniff');
  assert.equal(response.headers.get('referrer-policy'), 'no-referrer');
  assert.match(response.headers.get('cache-control'), /max-age=31536000, immutable/);
  assert.match(response.headers.get('content-security-policy'), /script-src 'self'/);
  assert.equal(response.headers.get('strict-transport-security'), 'max-age=86400');
});

test('Pages preserves HEAD, conditional responses and video byte ranges', async () => {
  globalThis.fetch = () => {
    throw new Error('must not contact origin');
  };
  const scenarios = [
    {
      method: 'HEAD',
      input: {},
      status: 200,
      output: { 'Content-Type': 'video/mp4', 'Content-Length': '100' },
      body: null,
    },
    {
      method: 'GET',
      input: { Range: 'bytes=0-2' },
      status: 206,
      output: { 'Content-Type': 'video/mp4', 'Content-Range': 'bytes 0-2/100' },
      body: 'abc',
    },
    {
      method: 'GET',
      input: { 'If-None-Match': 'etag' },
      status: 304,
      output: { ETag: 'etag' },
      body: null,
    },
    {
      method: 'GET',
      input: { Range: 'bytes=999-1000' },
      status: 416,
      output: { 'Content-Range': 'bytes */100' },
      body: null,
    },
  ];
  for (const scenario of scenarios) {
    const response = await worker.fetch(
      new Request(origin + '/onboardflow-local-ai-20261025.mp4', {
        method: scenario.method,
        headers: scenario.input,
      }),
      {
        ...env,
        ASSETS: {
          fetch: async (request) => {
            assert.equal(request.method, scenario.method);
            for (const [name, value] of Object.entries(scenario.input))
              assert.equal(request.headers.get(name), value);
            return new Response(scenario.body, {
              status: scenario.status,
              headers: scenario.output,
            });
          },
        },
      },
    );
    assert.equal(response.status, scenario.status);
    assert.equal(response.headers.get('x-onboardflow-asset-source'), 'pages');
    assert.equal(await response.text(), scenario.body ?? '');
    for (const [name, value] of Object.entries(scenario.output))
      assert.equal(response.headers.get(name), value);
    if (scenario.status === 416) assert.equal(response.headers.get('cache-control'), 'no-store');
  }
});

test('missing assets, SPA HTML and asset-store failures fall back to the signed origin once', async () => {
  for (const variant of ['missing', 'html', 'failed']) {
    let forwards = 0;
    globalThis.fetch = async (_url, init) => {
      forwards++;
      assert(init.headers.has('x-onboardflow-edge-signature'));
      return new Response('origin stylesheet', { headers: { 'Content-Type': 'text/css' } });
    };
    const response = await worker.fetch(new Request(origin + '/assets/old-Abcd1234.css'), {
      ...env,
      ASSETS: {
        fetch: async () => {
          if (variant === 'failed') throw new Error('asset store unavailable');
          return variant === 'missing'
            ? new Response(null, { status: 404 })
            : new Response('fallback', { headers: { 'Content-Type': 'text/html' } });
        },
      },
    });
    assert.equal(await response.text(), 'origin stylesheet');
    assert.equal(forwards, 1);
  }
});

test('HTML, release, health, private APIs and mutations cannot be served from Pages assets', async () => {
  let assets = 0;
  const assetEnv = {
    ...env,
    ASSETS: {
      fetch: async () => {
        assets++;
        return new Response('must not serve');
      },
    },
  };
  globalThis.fetch = async () => new Response('signed origin');
  for (const path of [
    '/',
    '/l/instructions',
    '/release.json',
    '/health/ready',
    '/api/csrf',
    '/api/intake/',
    '/assets/%2Fapi%2Fsecret.js',
  ]) {
    assert.equal(
      await (await worker.fetch(new Request(origin + path), assetEnv)).text(),
      'signed origin',
    );
  }
  assert.equal(
    await (
      await worker.fetch(
        new Request(origin + '/assets/index-Abcd1234.js', { method: 'POST', body: '{}' }),
        assetEnv,
      )
    ).text(),
    'signed origin',
  );
  assert.equal(assets, 0);
  globalThis.fetch = async () => {
    throw new Error('origin down');
  };
  assert.equal((await worker.fetch(new Request(origin + '/'), assetEnv)).status, 503);
  assert.equal(assets, 0);
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
