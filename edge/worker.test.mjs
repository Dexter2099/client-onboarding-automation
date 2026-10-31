import test from 'node:test';
import assert from 'node:assert/strict';
import { webcrypto } from 'node:crypto';
import worker, { networkAddress } from './_worker.js';

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
function edgeRequest(url, init = {}) {
  const headers = new Headers(init.headers);
  if (!headers.has('CF-Connecting-IP')) headers.set('CF-Connecting-IP', '203.0.113.9');
  return new Request(url, { ...init, headers });
}

test.after(() => {
  globalThis.fetch = originalFetch;
});

test('public assets use Pages with security/cache headers and no visitor credentials or origin request', async () => {
  globalThis.fetch = () => {
    throw new Error('must not contact origin');
  };
  let captured;
  const response = await worker.fetch(
    edgeRequest(origin + '/assets/index-Abcd1234.js', {
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
  assert.equal(response.headers.get('x-onboardflow-gateway-hash'), 'unpackaged');
  assert.equal(response.headers.get('x-content-type-options'), 'nosniff');
  assert.equal(response.headers.get('referrer-policy'), 'no-referrer');
  assert.match(response.headers.get('cache-control'), /max-age=31536000, immutable/);
  assert.match(response.headers.get('content-security-policy'), /script-src 'self'/);
  assert.equal(response.headers.get('strict-transport-security'), 'max-age=86400');
});

test('metadata and video ranges retain the signed origin handler; Pages serves conditional public GETs', async () => {
  const scenarios = [
    {
      method: 'HEAD',
      input: {},
      status: 200,
      output: { 'Content-Type': 'video/mp4', 'Content-Length': '100' },
      body: 'upstream body must not reach a HEAD client',
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
    let forwards = 0;
    globalThis.fetch = async (_url, init) => {
      forwards++;
      assert(init.headers.has('x-onboardflow-edge-signature'));
      for (const [name, value] of Object.entries(scenario.input))
        assert.equal(init.headers.get(name), value);
      assert.equal(init.method, 'GET');
      const target = new URL(_url);
      const canonical = [
        'v1',
        init.headers.get('x-onboardflow-edge-time'),
        origin,
        init.method,
        target.pathname + target.search,
        'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
      ].join('\n');
      assert(
        await webcrypto.subtle.verify(
          { name: 'ECDSA', hash: 'SHA-256' },
          pair.publicKey,
          Buffer.from(init.headers.get('x-onboardflow-edge-signature'), 'base64'),
          new TextEncoder().encode(canonical),
        ),
      );
      // The hosting CDN rewrites cold HEAD even when the caller requests cache bypass.
      if (init.method === 'HEAD')
        return new Response(null, { status: 308, headers: { Location: origin } });
      assert.equal(init.cache, 'no-store');
      return new Response(scenario.body, { status: scenario.status, headers: scenario.output });
    };
    const response = await worker.fetch(
      edgeRequest(
        origin +
          (scenario.status === 304
            ? '/assets/index-Abcd1234.js'
            : '/onboardflow-local-ai-20261025.mp4'),
        {
          method: scenario.method,
          headers: scenario.input,
        },
      ),
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
    assert.equal(
      response.headers.get('x-onboardflow-asset-source'),
      scenario.status === 304 ? 'pages' : null,
    );
    assert.equal(forwards, scenario.status === 304 ? 0 : 1);
    assert.equal(await response.text(), scenario.method === 'HEAD' ? '' : (scenario.body ?? ''));
    for (const [name, value] of Object.entries(scenario.output))
      assert.equal(response.headers.get(name), value);
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
    const response = await worker.fetch(edgeRequest(origin + '/assets/old-Abcd1234.css'), {
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
    '/HeAlTh/ReAdY/',
    '/api/csrf',
    '/api/intake/',
    '/assets/%2Fapi%2Fsecret.js',
  ]) {
    assert.equal(
      await (await worker.fetch(edgeRequest(origin + path), assetEnv)).text(),
      'signed origin',
    );
  }
  assert.equal(
    await (
      await worker.fetch(
        edgeRequest(origin + '/assets/index-Abcd1234.js', { method: 'POST', body: '{}' }),
        assetEnv,
      )
    ).text(),
    'signed origin',
  );
  assert.equal(assets, 0);
  for (const request of [
    edgeRequest(origin + '/assets/index-Abcd1234.js', { method: 'HEAD' }),
    edgeRequest(origin + '/assets/index-Abcd1234.js', { headers: { Range: 'bytes=0-15' } }),
  ])
    assert.equal(
      await (await worker.fetch(request, assetEnv)).text(),
      request.method === 'HEAD' ? '' : 'signed origin',
    );
  assert.equal(assets, 0);
  globalThis.fetch = async () => {
    throw new Error('origin down');
  };
  assert.equal((await worker.fetch(edgeRequest(origin + '/'), assetEnv)).status, 503);
  assert.equal(assets, 0);
});

test('public HEAD ignores range while private HEAD keeps its original method and status', async () => {
  globalThis.fetch = async (_url, init) => {
    assert.equal(init.method, 'GET');
    assert.equal(init.headers.get('accept-encoding'), 'identity');
    assert.equal(init.headers.has('range'), false);
    assert.equal(init.headers.has('if-range'), false);
    assert.equal(init.headers.get('if-none-match'), 'etag');
    return new Response(null, { status: 304, headers: { ETag: 'etag' } });
  };
  const metadata = await worker.fetch(
    edgeRequest(origin + '/onboardflow-local-ai-20261025.mp4', {
      method: 'HEAD',
      headers: { Range: 'bytes=0-1', 'If-Range': 'old', 'If-None-Match': 'etag' },
    }),
    env,
  );
  assert.equal(metadata.status, 304);
  assert.equal(metadata.headers.get('etag'), 'etag');
  assert.equal(metadata.body, null);
  globalThis.fetch = async (_url, init) => {
    assert.equal(init.method, 'HEAD');
    return new Response(null, { status: 405 });
  };
  assert.equal(
    (await worker.fetch(edgeRequest(origin + '/api/intake/', { method: 'HEAD' }), env)).status,
    405,
  );
});

test('edge binds method, encoded target and body, preserving session and CSRF headers', async () => {
  let captured;
  globalThis.fetch = async (url, init) => {
    captured = { url: url.toString(), ...init };
    return new Response('ok');
  };
  const body = '{"commandId":"fictional","operation":"edit"}';
  const request = edgeRequest(origin + '/api/intake/command?encoded=a%2Fb', {
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
  const upstream = new URL(captured.url);
  assert.equal(upstream.origin, 'https://onboardflow.hostless.app');
  assert.equal(upstream.searchParams.get('encoded'), 'a/b');
  assert.match(upstream.searchParams.get('__of_network'), /^[a-f0-9]{64}$/);
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
    upstream.pathname + upstream.search,
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
    const r = await worker.fetch(edgeRequest(url), env);
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
  await worker.fetch(edgeRequest(origin + '//attacker.example/secret'), env);
  assert.equal(destination.origin, 'https://onboardflow.hostless.app');
});

test('oversized mutations are rejected without forwarding', async () => {
  globalThis.fetch = () => {
    throw new Error('must not forward');
  };
  const r = await worker.fetch(
    edgeRequest(origin + '/api/intake/command', { method: 'POST', body: 'x'.repeat(16385) }),
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
  assert.equal((await worker.fetch(edgeRequest(origin), env)).status, 503);
  assert.equal(calls, 1);
  calls = 0;
  globalThis.fetch = async () => {
    calls++;
    throw new Error('connection lost after sending');
  };
  const r = await worker.fetch(
    edgeRequest(origin + '/api/intake/command', { method: 'POST', body: '{}' }),
    env,
  );
  assert.equal(r.status, 503);
  assert.equal(r.headers.get('location'), null);
  assert.equal(calls, 1);
});

test('missing signing configuration fails closed', async () => {
  assert.equal((await worker.fetch(edgeRequest(origin), {})).status, 503);
});

test('network buckets are signed, keyed, spoof-resistant and group IPv6 privacy addresses', async () => {
  const captured = [];
  globalThis.fetch = async (url, init) => {
    captured.push({ url: new URL(url), init });
    return new Response('ok');
  };
  for (const address of [
    '2001:db8:abcd:1234::1',
    '2001:db8:abcd:1234::9999',
    '2001:db8:abcd:5678::1',
  ]) {
    const r = await worker.fetch(
      edgeRequest(origin + '/api/csrf?__OF_NETWORK=' + 'f'.repeat(64), {
        headers: { 'CF-Connecting-IP': address },
      }),
      env,
    );
    assert.equal(r.status, 200);
  }
  const ids = captured.map((x) => x.url.searchParams.get('__of_network'));
  assert.equal(ids[0], ids[1]);
  assert.notEqual(ids[0], ids[2]);
  assert.notEqual(ids[0], 'f'.repeat(64));
  assert.equal(captured[0].url.searchParams.getAll('__of_network').length, 1);
  assert.equal(captured[0].url.searchParams.has('__OF_NETWORK'), false);
  assert(!captured[0].url.href.includes('2001:'));
  const target = captured[0].url;
  const canonical = [
    'v1',
    captured[0].init.headers.get('X-OnboardFlow-Edge-Time'),
    origin,
    'GET',
    target.pathname + target.search,
    Buffer.from(await webcrypto.subtle.digest('SHA-256', new Uint8Array())).toString('hex'),
  ].join('\n');
  assert(
    await webcrypto.subtle.verify(
      { name: 'ECDSA', hash: 'SHA-256' },
      pair.publicKey,
      Buffer.from(captured[0].init.headers.get('X-OnboardFlow-Edge-Signature'), 'base64'),
      new TextEncoder().encode(canonical),
    ),
  );
  assert.equal(networkAddress('2001:db8:abcd:1234::1'), '2001:0db8:abcd:1234::/64');
  assert.equal(networkAddress('not-an-ip'), null);
  assert.equal((await worker.fetch(new Request(origin + '/api/csrf'), env)).status, 503);
});
