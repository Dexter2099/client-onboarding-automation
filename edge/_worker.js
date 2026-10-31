const PUBLIC_ORIGIN = 'https://dexter-onboardflow.pages.dev';
const UPSTREAM_ORIGIN = 'https://onboardflow.hostless.app';
const MAX_BODY_BYTES = 16384;
const ASSET_RELEASE = 'unpackaged';
const ASSET_POLICY =
  "default-src 'none'; script-src 'self'; script-src-attr 'none'; " +
  "style-src 'self'; style-src-attr 'none'; img-src 'self'; font-src 'self'; media-src 'self'; connect-src 'self'; " +
  "base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'none'";
let importedKey;

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
    const body = await readBody(request);
    if (body === null) return new Response('Request too large.', { status: 413 });
    try {
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
        request.method,
        incoming.pathname + incoming.search,
        bodyHash,
      ].join('\n');
      const signature = await crypto.subtle.sign(
        { name: 'ECDSA', hash: 'SHA-256' },
        await importedKey,
        new TextEncoder().encode(canonical),
      );
      const headers = new Headers(request.headers);
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
        method: request.method,
        headers,
        body: body.byteLength ? body : undefined,
        redirect: 'manual',
        signal: request.signal,
      });
      // A failed origin must fail visibly rather than redirect visitors into a loop.
      if (response.status >= 300 && response.status < 400 && response.headers.has('Location'))
        return unavailable();
      return response;
    } catch {
      // Never retry a mutation or inference after an uncertain upstream result.
      return unavailable();
    }
  },
};
