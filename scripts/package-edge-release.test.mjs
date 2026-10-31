import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, readFile, readdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve, sep } from 'node:path';
import { createHash } from 'node:crypto';
import { packageEdgeRelease } from './package-edge-release.mjs';

test('package contains only approved public bytes and a revision-stamped worker, never the app shell or settings', async (t) => {
  const root = await mkdtemp(resolve(tmpdir(), 'onboardflow-assets-'));
  t.after(async () => {
    assert(resolve(root).startsWith(resolve(tmpdir()) + sep + 'onboardflow-assets-'));
    await rm(root, { recursive: true, force: true });
  });
  const build = resolve(root, 'build'),
    output = resolve(root, 'output');
  await mkdir(resolve(build, 'assets'), { recursive: true });
  await writeFile(
    resolve(build, 'index.html'),
    '<script src="/assets/index-Abcd1234.js"></script><link href="/assets/index-Abcd1234.css">',
  );
  for (const file of [
    'assets/index-Abcd1234.js',
    'assets/index-Abcd1234.css',
    'assets/font-Abcd1234.woff2',
    'onboardflow-local-ai-20261025.mp4',
    'onboardflow-local-ai-20261025.webm',
    'onboardflow-local-ai-20261025.webp',
    'favicon.svg',
  ])
    await writeFile(resolve(build, file), file);
  await writeFile(resolve(build, 'assets/private.env'), 'fictional secret must not publish');
  await writeFile(resolve(build, 'assets/index-Abcd1234.js.map'), 'source map must not publish');
  const revision = 'a'.repeat(40);
  const manifest = await packageEdgeRelease(revision, output, build);
  assert.equal(manifest.revision, revision);
  assert.equal(manifest.files.length, 4);
  const worker = await readFile(resolve(output, 'site/_worker.js'), 'utf8');
  assert(worker.includes(`const ASSET_RELEASE = '${revision}';`));
  assert.match(manifest.gatewaySourceSha256, /^[a-f0-9]{64}$/);
  assert(worker.includes(`const GATEWAY_HASH = '${manifest.gatewaySourceSha256}';`));
  assert.equal(manifest.workerSha256, createHash('sha256').update(worker).digest('hex'));
  assert.deepEqual((await readdir(resolve(output, 'site'))).sort(), [
    '404.html',
    '_worker.js',
    'assets',
    'onboardflow-local-ai-20261025.webp',
  ]);
  assert.equal((await readdir(resolve(output, 'site/assets'))).length, 3);
  for (const file of manifest.files)
    assert.equal(
      createHash('sha256')
        .update(await readFile(resolve(output, 'site', '.' + file.path)))
        .digest('hex'),
      file.sha256,
    );
  await assert.rejects(packageEdgeRelease(revision, output, build), { code: 'EEXIST' });
  await assert.rejects(packageEdgeRelease('invalid', resolve(root, 'invalid'), build));
  await writeFile(
    resolve(build, 'index.html'),
    '<script src="/assets/missing-Abcd1234.js"></script>',
  );
  await assert.rejects(
    packageEdgeRelease(revision, resolve(root, 'missing'), build),
    /Missing entry asset/,
  );
});
