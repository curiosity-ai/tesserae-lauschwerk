# sdk/ - alkove, built from source

The part of [alkove](https://github.com/alkove-ai/alkove) this repository builds and ships: the Rust
inference engine and core, compiled to WebAssembly, and the TypeScript web SDK around them. Copied out of
[`../.reference/`](../.reference) with the **same relative layout**, so the two can be compared
file by file; `.reference/` stays as upstream was when forked.

| | |
|---|---|
| `crates/engine` | the inference engine and the `.alk` model format |
| `crates/core` | the features (Focus, Cue, Spot, Lex, Timbre, the pipelines), written once in Rust |
| `crates/wasm` | their C ABI: the `alkove-engine.wasm` the web SDK loads |
| `packages/alkove` | the web SDK (TypeScript): inputs, worker, AudioWorklet, files, the model loader |
| `Cargo.toml` | the workspace - engine, core and wasm only (upstream's also lists Python and Android) |
| `rust-toolchain.toml` | the pinned toolchain (1.99.1) with the `wasm32-unknown-unknown` target |

Not copied, and only in `.reference/`: the Python package and its LiveKit and Pipecat plugins, the
Android library, the benchmarks, the docs site.

## Build

```bash
cd sdk/packages/alkove
npm ci
node scripts/build.mjs          # cargo -> alkove-engine.wasm, esbuild -> dist/, tsc -> .d.ts
```

`rustup` installs the pinned toolchain on first use (it reads `rust-toolchain.toml`). The script is
upstream's, unchanged: it builds with `+simd128,+bulk-memory` and remaps the toolchain and workspace
paths out of the binary, so the output does not depend on the machine. For 0.3.2 every file of `dist/`
is byte-identical to the published npm package (the wasm's sha256 is
`c22e900af5f401b29689b3b882f7245feefe4dea617ffdb3046a1d4f5771e543`).

`dotnet build` of `Tesserae.Alkove` runs this itself when a source changed (the `BuildAlkoveSdk`
target), then stages `dist/` into the package. `/p:AlkoveSource=npm` stages the published release
instead, for a machine without Rust.

## Test

```bash
cd sdk && cargo test --release -p alkove-engine -p alkove-core     # 34 tests

cd sdk/packages/alkove
node dist/cli.js models .cache/models                              # the five models, sha256-checked
npx vitest run                                                     # 95 tests: the wasm against the native engine, bit for bit
```

## Changing it

Change the code here, never in `.reference/`. `scripts/diff-upstream.sh` (`-u` for the full diff) shows
what we changed against upstream. A change to the engine or the core changes the wasm's sha256, which
`scripts/build.mjs` writes into `src/engine.generated.ts`: commit that file with the change. A change to a
model's bytes goes into `crates/core/models.json` (name, size, sha256, Hugging Face revision).

To take a newer upstream: replace `.reference/` with it, run `scripts/diff-upstream.sh -u` to list our
changes, copy the new sources here and carry the changes over.
