# sdk/ - Lauschwerk, built from source

The engine and the web SDK this repository builds and ships: the Rust inference engine and core, compiled
to WebAssembly, and the TypeScript web SDK around them. Copied from [`../.reference/`](../.reference)
(upstream as forked) with the same relative layout and renamed (`../scripts/upstream-names.py`), so the
two can still be compared file by file (`../scripts/diff-upstream.sh`).

| | |
|---|---|
| `crates/engine` | the inference engine and the `.alk` model format |
| `crates/core` | the features (Focus, Cue, Spot, Lex, Timbre, the pipelines), written once in Rust; `models.json` is the model manifest |
| `crates/wasm` | their C ABI: the `lauschwerk-engine.wasm` the web SDK loads |
| `packages/lauschwerk` | the web SDK (TypeScript): inputs, worker, AudioWorklet, files, the model loader |
| `Cargo.toml` | the workspace - engine, core and wasm |
| `rust-toolchain.toml` | the pinned toolchain (1.99.1) with the `wasm32-unknown-unknown` target |

## Build

```bash
cd sdk/packages/lauschwerk
npm ci
node scripts/build.mjs          # cargo -> lauschwerk-engine.wasm, esbuild -> dist/, tsc -> .d.ts
```

`rustup` installs the pinned toolchain on first use (it reads `rust-toolchain.toml`). The build uses
`+simd128,+bulk-memory` and remaps the toolchain and workspace paths out of the binary, so the output does
not depend on the machine. `dotnet build` of `Tesserae.Lauschwerk` runs it itself when a source changed
(the `BuildLauschwerkSdk` target), then stages `dist/` into the package.

## Test

```bash
cd sdk && cargo test --release -p lauschwerk-engine -p lauschwerk-core   # 34 tests

cd sdk/packages/lauschwerk
node dist/cli.js models .cache/models                                  # the five models, sha256-checked
npx vitest run                                                         # 95 tests: the wasm against the native engine, bit for bit
```

## Changing it

Change the code here, never in `.reference/`. `../scripts/diff-upstream.sh` (`-u` for the full diff) shows
what we changed against upstream, names aside. A change to the engine or the core changes the wasm's
sha256, which `scripts/build.mjs` writes into `src/engine.generated.ts`: commit that file with the change.
A model is described in `crates/core/models.json` (name, file, size, sha256, revision, download url).

To take a newer upstream: replace `.reference/` with it, run `../scripts/diff-upstream.sh -u` to list our
changes, copy the new sources here, rename them with `python3 ../scripts/upstream-names.py tree <folder>`,
and carry the changes over.
