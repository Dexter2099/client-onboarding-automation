import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile, writeFile, mkdir, readdir, lstat } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { publicAssetType } from '../edge/_worker.js';

const root = fileURLToPath(new URL('../', import.meta.url));

export async function packageEdgeRelease(
  revision,
  output,
  build = resolve(root, 'src/OnboardFlow.Api/wwwroot'),
) {
  assert.match(revision, /^[a-f0-9]{40}$/);
  const outputPath = resolve(output);
  await mkdir(dirname(outputPath), { recursive: true });
  await mkdir(outputPath); // A fresh output prevents stale/secret files entering the upload.
  const site = resolve(outputPath, 'site');
  await mkdir(resolve(site, 'assets'), { recursive: true });
  const files = [];
  async function copyPublic(path) {
    assert(publicAssetType(path), `Not an approved public asset: ${path}`);
    const source = resolve(build, '.' + path);
    assert((await lstat(source)).isFile(), 'Only regular public files may be uploaded.');
    const bytes = await readFile(source);
    await writeFile(resolve(site, '.' + path), bytes);
    files.push({
      path,
      bytes: bytes.length,
      sha256: createHash('sha256').update(bytes).digest('hex'),
    });
  }
  for (const name of (await readdir(resolve(build, 'assets'))).sort()) {
    if (publicAssetType('/assets/' + name)) await copyPublic('/assets/' + name);
  }
  for (const path of ['/onboardflow-local-ai-20261025.webp']) await copyPublic(path);
  const html = await readFile(resolve(build, 'index.html'), 'utf8');
  for (const [, path] of html.matchAll(/(?:src|href)="(\/assets\/[^"?#]+)"/g)) {
    assert(
      files.some((file) => file.path === path),
      `Missing entry asset: ${path}`,
    );
  }
  assert(
    files.some((file) => file.path.endsWith('.js')) &&
      files.some((file) => file.path.endsWith('.css')),
  );
  const sourceWorker = await readFile(resolve(root, 'edge/_worker.js'), 'utf8');
  const stamp = "const ASSET_RELEASE = 'unpackaged';";
  assert.equal(sourceWorker.split(stamp).length, 2);
  const worker = sourceWorker.replace(stamp, `const ASSET_RELEASE = '${revision}';`);
  await writeFile(resolve(site, '_worker.js'), worker);
  // No index.html: an asset miss must be a 404, never an SPA fallback. HTML and
  // release/readiness/API responses continue to require the actual application.
  await writeFile(resolve(site, '404.html'), '<!doctype html><title>Not found</title>');
  const manifest = {
    revision,
    workerSha256: createHash('sha256').update(worker).digest('hex'),
    files,
  };
  await writeFile(resolve(outputPath, 'manifest.json'), JSON.stringify(manifest, null, 2));
  return manifest;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const manifest = await packageEdgeRelease(
    process.argv[2],
    process.argv[3] ?? '.local/edge-release',
  );
  console.log(JSON.stringify(manifest));
}
