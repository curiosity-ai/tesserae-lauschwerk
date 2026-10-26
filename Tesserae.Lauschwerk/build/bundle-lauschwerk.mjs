/**
 * Stages the web SDK built from sdk/ (sdk/packages/lauschwerk/dist, by its scripts/build.mjs) into what
 * Tesserae.Lauschwerk ships (assets/js/lauschwerk/).
 *
 *   node build/bundle-lauschwerk.mjs
 *
 * lauschwerk's `dist/` is already a browser build: an ES module entry (`index.js`) with two shared
 * chunks, a chunk only Node ever imports, the realtime worker and the AudioWorklet as plain scripts,
 * and the WebAssembly engine. So unlike Monaco or pdf.js there is nothing to bundle: the files are
 * copied as they are, never patched, and two small files of our own are written beside them.
 *
 *   assets/js/lauschwerk/
 *     lauschwerk.js           the entry Tesserae.Lauschwerk fetches (RequireKind.Module). Ours: imports the
 *                         SDK, points assetBaseUrl at the versioned folder, publishes window.lauschwerk.
 *     version.txt         the bundled lauschwerk version; also MSBuild's Inputs/Outputs marker.
 *     v<version>/         lauschwerk's dist, verbatim: index.js, chunk-*.js, api-*.js, assets-*.js,
 *                         node-*.js, lauschwerk-worker.js, lauschwerk-worklet.js, lauschwerk-engine.wasm, and
 *                         cli.js (the self-hosting model downloader - never served to a page, see
 *                         buildTransitive/Tesserae.Lauschwerk.targets).
 *
 * Why a versioned folder. lauschwerk creates its worker with `new Worker(url)` and its worklet with
 * `audioWorklet.addModule(url)` from inside the SDK, so they are not fetched through Transpose's
 * loader and get no cache-busting token. Unversioned, the first load after an upgrade could pair a
 * new SDK with the previous worker out of the browser cache - they speak a private message
 * protocol, and nothing checks they match. A folder named after the version changes every URL the
 * SDK builds whenever the pin moves.
 *
 * Why assetBaseUrl. Without it lauschwerk hands the worker and worklet over as blob: URLs made from
 * strings inside `assets-*.js`, which needs `blob:` in a host's script-src and worker-src. With it
 * they are same-origin files, so `'self'` (plus `'wasm-unsafe-eval'`) is the whole policy.
 */
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');
const pkgDir = join(root, '..', 'sdk', 'packages', 'lauschwerk');
const dist = join(pkgDir, 'dist');
const out = join(root, 'assets', 'js', 'lauschwerk');

if (!existsSync(join(dist, 'index.js'))) {
  console.error('sdk/packages/lauschwerk/dist is missing: build it with `npm ci && node scripts/build.mjs` in sdk/packages/lauschwerk (needs Rust, see sdk/rust-toolchain.toml).');
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

for (const required of ['index.js', 'lauschwerk-worker.js', 'lauschwerk-worklet.js', 'lauschwerk-engine.wasm', 'cli.js']) {
  if (!copied.includes(required)) {
    console.error(`lauschwerk ${version}: dist/${required} is missing - the package layout changed, re-check build/bundle-lauschwerk.mjs.`);
    process.exit(1);
  }
}

// Where this build came from, for LauschwerkJs.Source and LauschwerkJs.EngineSha256 - what an application (or a
// bug report) quotes to say which engine it ran.
const engineSha256 = createHash('sha256').update(readFileSync(join(versioned, 'lauschwerk-engine.wasm'))).digest('hex');

// The entry. A module, because lauschwerk's dist is one (it resolves its chunks with static imports).
// assetBaseUrl is set only when the host has not set one already, so a host serving lauschwerk's files
// from somewhere else (a CDN) keeps that choice; configure() merges, so the models' base is untouched.
writeFileSync(join(out, 'lauschwerk.js'), `// Written by Tesserae.Lauschwerk's build/bundle-lauschwerk.mjs - do not edit.
import * as lauschwerk from './v${version}/index.js';

const base = new URL('./v${version}/', import.meta.url).href;
lauschwerk.Lauschwerk.configure({ assetBaseUrl: base });

globalThis.lauschwerk = Object.freeze(Object.assign({}, lauschwerk, { version: '${version}', assetBaseUrl: base, engineSha256: '${engineSha256}' }));
`);

writeFileSync(join(out, 'version.txt'), version + '\n');

console.log(`lauschwerk ${version}: ${copied.length} files, ${(bytes / 1024).toFixed(0)} KB -> ${out}`);
