const PUBLIC_ORIGIN = 'https://dexter-onboardflow.pages.dev';
const UPSTREAM_ORIGIN = 'https://onboardflow.hostless.app';
const MAX_BODY_BYTES = 16384;
const ASSET_RELEASE = 'unpackaged';
const ASSET_POLICY =
  "default-src 'none'; script-src 'self'; script-src-attr 'none'; " +
  "style-src 'self'; style-src-attr 'none'; img-src 'self'; font-src 'self'; media-src 'self'; connect-src 'self'; " +
  "base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'none'";
let importedKey;
let callerHashKey;
const NETWORK_QUERY = '__of_network';

export function networkAddress(address) {
  if (address.length > 64) return null;
  if (/^\d{1,3}(\.\d{1,3}){3}$/.test(address) && address.split('.').every((n) => Number(n) < 256))
    return address;
  // Cloudflare supplies a canonical client address. Group IPv6 privacy addresses by /64.
  if (!/^[0-9a-f:]+$/i.test(address) || !address.includes(':')) return null;
  const halves = address.toLowerCase().split('::');
  if (halves.length > 2) return null;
  const left = halves[0] ? halves[0].split(':') : [];
  const right = halves.length === 2 && halves[1] ? halves[1].split(':') : [];
  const expanded =
    halves.length === 2
      ? [...left, ...Array(Math.max(0, 8 - left.length - right.length)).fill('0'), ...right]
      : left;
  if (expanded.length !== 8 || expanded.some((part) => !/^[0-9a-f]{1,4}$/.test(part))) return null;
  return (
    expanded
      .slice(0, 4)
      .map((part) => part.padStart(4, '0'))
      .join(':') + '::/64'
  );
}

async function networkBucket(request, env) {
  const address = networkAddress(request.headers.get('CF-Connecting-IP') ?? '');
  if (!address) return null;
  // HKDF derives an independent HMAC key from the existing protected gateway secret.
  // No new provider, secret binding or raw-IP database storage is introduced.
  callerHashKey ??= crypto.subtle
    .importKey(
      'raw',
      Uint8Array.from(atob(env.EDGE_SIGNING_KEY), (c) => c.charCodeAt(0)),
      'HKDF',
      false,
      ['deriveKey'],
    )
    .then((key) =>
      crypto.subtle.deriveKey(
        {
          name: 'HKDF',
          hash: 'SHA-256',
          salt: new Uint8Array(),
          info: new TextEncoder().encode('onboardflow-network-v1'),
        },
        key,
        { name: 'HMAC', hash: 'SHA-256', length: 256 },
        false,
        ['sign'],
      ),
    );
  const value = new TextEncoder().encode(new Date().toISOString().slice(0, 10) + '\n' + address);
  const digest = new Uint8Array(await crypto.subtle.sign('HMAC', await callerHashKey, value));
  return Array.from(digest, (b) => b.toString(16).padStart(2, '0')).join('');
}

export function publicAssetType(path) {
  const extension = /^\/assets\/[A-Za-z0-9_-]+-[A-Za-z0-9_-]{8,}\.(js|css|woff2)$/.exec(path)?.[1];
  if (extension) return { js: 'javascript', css: 'text/css', woff2: 'font/woff2' }[extension];
  return {
    '/onboardflow-local-ai-20261025.webp': 'image/webp',
  }[path];
}

async function publicAsset(request, env, expectedType) {
  if (!env.ASSETS) return null;
  try {
    // Public immutable files never need visitor credentials or forwarding headers.
    const headers = new Headers();
    for (const name of ['If-None-Match', 'If-Modified-Since', 'Accept-Encoding']) {
      if (request.headers.has(name)) headers.set(name, request.headers.get(name));
    }
    const asset = await env.ASSETS.fetch(
      new Request(request.url, { method: request.method, headers }),
    );
    const type = asset.headers.get('content-type') ?? '';
    const valid = asset.status === 200 && type.includes(expectedType);
    const conditional =
      asset.status === 304 && (headers.has('If-None-Match') || headers.has('If-Modified-Since'));
    // A missing file or SPA fallback must never be returned as JavaScript. Old hashes
    // can still use the signed origin, including while release versions overlap.
    if (!valid && !conditional) return null;
    const responseHeaders = new Headers(asset.headers);
    responseHeaders.delete('Set-Cookie');
    responseHeaders.set('Cache-Control', 'public, max-age=31536000, immutable');
    responseHeaders.set('Content-Security-Policy', ASSET_POLICY);
    responseHeaders.set('X-Content-Type-Options', 'nosniff');
    responseHeaders.set('X-Frame-Options', 'DENY');
    responseHeaders.set('Referrer-Policy', 'no-referrer');
    responseHeaders.set('Strict-Transport-Security', 'max-age=86400');
    responseHeaders.set('X-OnboardFlow-Asset-Source', 'pages');
    responseHeaders.set('X-OnboardFlow-Asset-Revision', ASSET_RELEASE);
    return new Response(asset.body, {
      status: asset.status,
      headers: responseHeaders,
    });
  } catch {
    return null; // Only a public GET can fall back; never replay an API mutation.
  }
}

const unavailable = () =>
  new Response('The demo is temporarily unavailable. Please try again shortly.', {
    status: 503,
    headers: {
      'Content-Type': 'text/plain; charset=utf-8',
      'Cache-Control': 'no-store',
      'Retry-After': '60',
    },
  });

async function readBody(request) {
  if (!request.body || request.method === 'GET' || request.method === 'HEAD')
    return new Uint8Array();
  const reader = request.body.getReader();
  const chunks = [];
  let length = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    length += value.byteLength;
    if (length > MAX_BODY_BYTES) {
      await reader.cancel();
      return null;
    }
    chunks.push(value);
  }
  const result = new Uint8Array(length);
  let offset = 0;
  for (const chunk of chunks) {
    result.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return result;
}

export default {
  async fetch(request, env) {
    const incoming = new URL(request.url);
    if (incoming.origin !== PUBLIC_ORIGIN) {
      return Response.redirect(PUBLIC_ORIGIN + incoming.pathname + incoming.search, 308);
    }
    if (!env.EDGE_SIGNING_KEY) return unavailable();
    // Pages' binding does not preserve metadata/range semantics. Retain the
    // existing origin handler for those requests and for the lazy-loaded video.
    const assetType =
      request.method === 'GET' &&
      !request.headers.has('Range') &&
      publicAssetType(incoming.pathname);
    if (assetType) {
      const asset = await publicAsset(request, env, assetType);
      if (asset) return asset;
    }
    // The hosting CDN rewrites cold public-file HEAD to GET even with cache bypass.
    // Sign the actual upstream GET and return its native metadata without a body.
    // Private/API HEAD and all range GETs retain their original methods and handlers.
    const metadataOnly =
      request.method === 'HEAD' &&
      (publicAssetType(incoming.pathname) ||
        ['/onboardflow-local-ai-20261025.mp4', '/onboardflow-local-ai-20261025.webm'].includes(
          incoming.pathname,
        ));
    const upstreamMethod = metadataOnly ? 'GET' : request.method;
    const body = await readBody(request);
    if (body === null) return new Response('Request too large.', { status: 413 });
    try {
      if (
        /^\/api(?:\/|$)/i.test(incoming.pathname) ||
        incoming.pathname.replace(/\/+$/, '').toLowerCase() === '/health/ready'
      ) {
        const caller = await networkBucket(request, env);
        if (!caller) return unavailable();
        for (const name of Array.from(incoming.searchParams.keys()))
          if (name.toLowerCase() === NETWORK_QUERY) incoming.searchParams.delete(name);
        incoming.searchParams.set(NETWORK_QUERY, caller);
      } else incoming.searchParams.delete(NETWORK_QUERY);
      // Only this edge holds the private key. The app receives a public verification key.
      importedKey ??= crypto.subtle.importKey(
        'pkcs8',
        Uint8Array.from(atob(env.EDGE_SIGNING_KEY), (c) => c.charCodeAt(0)),
        { name: 'ECDSA', namedCurve: 'P-256' },
        false,
        ['sign'],
      );
      const timestamp = Math.floor(Date.now() / 1000).toString();
      const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', body));
      const bodyHash = Array.from(digest, (b) => b.toString(16).padStart(2, '0')).join('');
      const canonical = [
        'v1',
        timestamp,
        PUBLIC_ORIGIN,
        upstreamMethod,
        incoming.pathname + incoming.search,
        bodyHash,
      ].join('\n');
      const signature = await crypto.subtle.sign(
        { name: 'ECDSA', hash: 'SHA-256' },
        await importedKey,
        new TextEncoder().encode(canonical),
      );
      const headers = new Headers(request.headers);
      if (metadataOnly) {
        headers.delete('Range');
        headers.delete('If-Range');
        headers.set('Accept-Encoding', 'identity');
      }
      for (const name of Array.from(headers.keys())) {
        if (/^(host|forwarded|x-forwarded-.*|cf-visitor|x-onboardflow-.*)$/i.test(name))
          headers.delete(name);
      }
      headers.set('X-OnboardFlow-Edge-Time', timestamp);
      headers.set(
        'X-OnboardFlow-Edge-Signature',
        btoa(String.fromCharCode(...new Uint8Array(signature))),
      );
      // Set components on a fixed origin: a path beginning // must never become an open proxy.
      const upstream = new URL(UPSTREAM_ORIGIN);
      upstream.pathname = incoming.pathname;
      upstream.search = incoming.search;
      const response = await fetch(upstream, {
        method: upstreamMethod,
        headers,
        body: body.byteLength ? body : undefined,
        redirect: 'manual',
        // Avoid cached signed-origin responses; Pages assets retain native caching.
        cache: 'no-store',
        signal: request.signal,
      });
      // A failed origin must fail visibly rather than redirect visitors into a loop.
      if (response.status >= 300 && response.status < 400 && response.headers.has('Location'))
        return unavailable();
      if (metadataOnly) {
        await response.body?.cancel().catch(() => {});
        return new Response(null, { status: response.status, headers: response.headers });
      }
      return response;
    } catch {
      // Never retry a mutation or inference after an uncertain upstream result.
      return unavailable();
    }
  },
};
