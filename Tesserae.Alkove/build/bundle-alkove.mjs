/**
 * Stages alkove's browser build into what Tesserae.Alkove ships (assets/js/alkove/).
 *
 *   node build/bundle-alkove.mjs          from sdk/packages/alkove/dist - built from source by
 *                                         sdk/packages/alkove/scripts/build.mjs (the default, and CI)
 *   node build/bundle-alkove.mjs npm      from node_modules/alkove/dist - the pinned npm release, for a
 *                                         machine without Rust, or to compare against upstream
 *
 * The two are byte-identical for 0.3.2 (checked file by file, the wasm's sha256 included): upstream's
 * build is reproducible - paths remapped, one pinned toolchain - and our copy of its source is unmodified.
 *
 * alkove's `dist/` is already a browser build: an ES module entry (`index.js`) with two shared
 * chunks, a chunk only Node ever imports, the realtime worker and the AudioWorklet as plain scripts,
 * and the WebAssembly engine. So unlike Monaco or pdf.js there is nothing to bundle: the files are
 * copied as they are, never patched, and two small files of our own are written beside them.
 *
 *   assets/js/alkove/
 *     alkove.js           the entry Tesserae.Alkove fetches (RequireKind.Module). Ours: imports the
 *                         SDK, points assetBaseUrl at the versioned folder, publishes window.alkove.
 *     version.txt         the bundled alkove version; also MSBuild's Inputs/Outputs marker.
 *     v<version>/         alkove's dist, verbatim: index.js, chunk-*.js, api-*.js, assets-*.js,
 *                         node-*.js, alkove-worker.js, alkove-worklet.js, alkove-engine.wasm, and
 *                         cli.js (the self-hosting model downloader - never served to a page, see
 *                         buildTransitive/Tesserae.Alkove.targets).
 *
 * Why a versioned folder. alkove creates its worker with `new Worker(url)` and its worklet with
 * `audioWorklet.addModule(url)` from inside the SDK, so they are not fetched through Transpose's
 * loader and get no cache-busting token. Unversioned, the first load after an upgrade could pair a
 * new SDK with the previous worker out of the browser cache - they speak a private message
 * protocol, and nothing checks they match. A folder named after the version changes every URL the
 * SDK builds whenever the pin moves.
 *
 * Why assetBaseUrl. Without it alkove hands the worker and worklet over as blob: URLs made from
 * strings inside `assets-*.js`, which needs `blob:` in a host's script-src and worker-src. With it
 * they are same-origin files, so `'self'` (plus `'wasm-unsafe-eval'`) is the whole policy.
 */
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');
const fromNpm = process.argv[2] === 'npm';
const pkgDir = fromNpm ? join(root, 'node_modules', 'alkove') : join(root, '..', 'sdk', 'packages', 'alkove');
const dist = join(pkgDir, 'dist');
const out = join(root, 'assets', 'js', 'alkove');

if (!existsSync(join(dist, 'index.js'))) {
  console.error(fromNpm
    ? 'node_modules/alkove is missing: run npm install in Tesserae.Alkove first.'
    : 'sdk/packages/alkove/dist is missing: build it with `npm ci && node scripts/build.mjs` in sdk/packages/alkove (needs Rust, see sdk/rust-toolchain.toml), or stage the npm release with `npm run bundle -- npm`.');
  process.exit(1);
}

const { version } = JSON.parse(readFileSync(join(pkgDir, 'package.json'), 'utf8'));
const versioned = join(out, `v${version}`);

rmSync(out, { recursive: true, force: true });
mkdirSync(versioned, { recursive: true });

let bytes = 0;
const copied = [];
for (const file of readdirSync(dist)) {
  // Declarations are for TypeScript consumers; the C# side declares the surface itself.
  if (file.endsWith('.d.ts') || !/\.(js|wasm)$/.test(file)) continue;
  cpSync(join(dist, file), join(versioned, file));
  bytes += readFileSync(join(dist, file)).length;
  copied.push(file);
}

for (const required of ['index.js', 'alkove-worker.js', 'alkove-worklet.js', 'alkove-engine.wasm', 'cli.js']) {
  if (!copied.includes(required)) {
    console.error(`alkove ${version}: dist/${required} is missing - the package layout changed, re-check build/bundle-alkove.mjs.`);
    process.exit(1);
  }
}

// Where this build came from, for AlkoveJs.Source and AlkoveJs.EngineSha256 - what an application (or a
// bug report) quotes to say which engine it ran.
const engineSha256 = createHash('sha256').update(readFileSync(join(versioned, 'alkove-engine.wasm'))).digest('hex');

// The entry. A module, because alkove's dist is one (it resolves its chunks with static imports).
// assetBaseUrl is set only when the host has not set one already, so a host serving alkove's files
// from somewhere else (a CDN) keeps that choice; configure() merges, so the models' base is untouched.
writeFileSync(join(out, 'alkove.js'), `// Written by Tesserae.Alkove's build/bundle-alkove.mjs - do not edit.
import * as alkove from './v${version}/index.js';

const base = new URL('./v${version}/', import.meta.url).href;
alkove.Alkove.configure({ assetBaseUrl: base });

globalThis.alkove = Object.freeze(Object.assign({}, alkove, { version: '${version}', assetBaseUrl: base, source: '${fromNpm ? 'npm' : 'sdk'}', engineSha256: '${engineSha256}' }));
`);

writeFileSync(join(out, 'version.txt'), version + '\n');

console.log(`alkove ${version} (${fromNpm ? 'npm release' : 'built from sdk/'}): ${copied.length} files, ${(bytes / 1024).toFixed(0)} KB -> ${out}`);
