const PUBLIC_ORIGIN = 'https://dexter-onboardflow.pages.dev';
const UPSTREAM_ORIGIN = 'https://onboardflow.hostless.app';
const MAX_BODY_BYTES = 16384;
let importedKey;

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
