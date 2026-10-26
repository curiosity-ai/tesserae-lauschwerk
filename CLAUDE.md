# CLAUDE.md

Guidance for working in this repository.

## What this is

This repository ships **Lauschwerk** - on-device voice isolation (Focus), voice activity (Cue), typed
wake words (Spot + Lex) and voiceprints (Timbre), a Rust engine compiled to WebAssembly with a TypeScript
web SDK around it - and **Tesserae.Lauschwerk**, the [Tesserae](https://github.com/curiosity-ai/tesserae)
(Transpose C#-to-JavaScript) package that ships it: a typed `[External]` interop layer, components built
from Tesserae's own, and a sample gallery. It is shaped like its siblings
[tesserae-monaco](https://github.com/curiosity-ai/tesserae-monaco) and
[tesserae-pdf](https://github.com/curiosity-ai/tesserae-pdf), and named in the direction of
[Voxwerk](https://github.com/curiosity-ai/voxwerk): Voxwerk speaks, Lauschwerk listens (*lauschen*, to
listen closely). The package depends on **Tesserae only**.

The repository started as a fork of another project. Its source lives on, unchanged, in **`.reference/`**
- "upstream" everywhere below - to diff against; nothing builds from it. The part we build and ship (the
engine, the core, their wasm module and the web SDK) was copied into **`sdk/`** with the same relative
layout and renamed to Lauschwerk (`scripts/upstream-names.py`), and that copy is what gets built: Rust to
WebAssembly, then the SDK (see "Lauschwerk is built from source"). Change code in `sdk/`, never in
`.reference/`. Upstream's GitHub workflows are kept, disabled, in `.reference/.github/workflows/`.

**The original name appears in exactly three places on our side**, and should stay out of everything
else: the license files (`LICENSE`, `NOTICE`, and the copies in `sdk/packages/lauschwerk/` - Apache-2.0
requires them unchanged), the README's credit, and the five model download URLs in
`sdk/crates/core/models.json` (the weights are hosted upstream; see "Models"). The rename tooling
(`scripts/upstream-names.py`) spells it once, split, to map between the two.

```
Tesserae.Lauschwerk/                    the package
  src/LauschwerkJs.Runtime.cs           loading the SDK, models' base URL, support, preload, global diagnostics
  src/LauschwerkJs.cs                   factories: Create{Call,Agent}InputAsync, CreateWakeListenerAsync,
                                    CompileWordAsync, EnrollWordAsync, ProcessAsync / ProcessUrlAsync / DecodeAsync
  src/Inputs/                       VoiceInput (+ CallInput, AgentInput, WakeListener, WakeWordHandle) and settings
  src/Types/                        Voiceprint, WakeWord, WakeEvent, AudioClip (+ WAV encoder), enums,
                                    Timbre, Lex, VoiceprintLearner, WordEnrollment, LauschwerkException, PromiseHelper
  src/Interop/                      [External] declarations of the `lauschwerk` global, events, [ObjectLiteral] options
  src/Components/                   the Tesserae components (panels, meters, waveform, enrollment...)
  src/LauschwerkUI.cs                   the component factories (using static Tesserae.Lauschwerk.LauschwerkUI)
  build/bundle-lauschwerk.mjs       stages sdk/packages/lauschwerk/dist into assets/js/lauschwerk
  buildTransitive/*.targets         copies it into a consumer's output; opt-in model self-hosting
Tesserae.Lauschwerk.Sample/             the gallery: a sidebar of features, one page each
  assets/audio/                     the speech fixtures, synthesized by tools/Lauschwerk.TestAudio
tests/                              Playwright specs (fake microphone = the fixtures), a static server
tools/Lauschwerk.TestAudio/             writes the fixtures with Voxwerk (Qwen3-TTS in C#), like Neko's voice-overs
scripts/stage-samples.mjs           stages the Release build into _site/ for GitHub Pages
scripts/diff-upstream.sh            what sdk/ changes against .reference/, names mapped
scripts/upstream-names.py           the name mapping between upstream and sdk/
sdk/                                Lauschwerk itself: crates/{engine,core,wasm}, packages/lauschwerk (the web SDK)
.reference/                         upstream as forked, unchanged, for diffing
```

## Build

```bash
dotnet tool update --global Transpose.Compiler
dotnet tool update --global dotnet-serve
export PATH="$PATH:$HOME/.dotnet/tools"

dotnet build Tesserae.Lauschwerk.slnx
```

The first build compiles Lauschwerk from `sdk/` (the `BuildLauschwerkSdk` target: `npm ci` and
`scripts/build.mjs` in `sdk/packages/lauschwerk` - cargo to wasm32, then esbuild), stages it
(`BundleLauschwerk`), and the sample's first build downloads the five models (7.5 MB) once per version into
`obj/lauschwerk-models/`. So **Rust** (rustup; the toolchain comes from `sdk/rust-toolchain.toml`) and
**node** are prerequisites. To see the gallery:

```bash
cd Tesserae.Lauschwerk.Sample/bin/Debug/netstandard2.0/tps && dotnet serve --port 5000
```

`localhost` is a secure context; the microphone and Cache Storage need one.

## Lauschwerk is built from source

`sdk/` holds the engine and core (Rust), their wasm module and the web SDK (TypeScript), copied from
`.reference/` with the same layout (`sdk/crates/{engine,core,wasm}`, `sdk/packages/lauschwerk`). Python,
Android, the integrations and the benchmarks stay only in `.reference/`; the `sdk/Cargo.toml` workspace
has the three crates as members, so its `Cargo.lock` is upstream's minus theirs. The crates have no
dependencies from crates.io: the build needs the toolchain and nothing else.

The build is `sdk/packages/lauschwerk/scripts/build.mjs`, upstream's script: `cargo build --release -p
lauschwerk-wasm --target wasm32-unknown-unknown` with `+simd128,+bulk-memory` and the toolchain and
workspace paths remapped out of the binary, then esbuild for the SDK, the worker and the worklet, then
`tsc` for the declarations. The build is reproducible - one pinned toolchain (`sdk/rust-toolchain.toml`,
1.99.1), no machine paths in the output.

**How we know the copy is right.** Two checks, both worth repeating after any upstream merge:

- *Before the rename*, the copy built here was byte-identical to upstream's published release, file for
  file, engine included (sha256 `c22e900a...`).
- *After the rename* the engine's bytes differ - crate names and the remapped workspace path end up in its
  panic messages, which moves its data around - but what it **computes** does not: with our names mapped
  back to upstream's inside the digests, all 95 of the SDK's tests pass against upstream's own golden
  values. Seven golden hashes in `test/fixtures/core.json` were then regenerated, each shown first to
  differ by the name alone. A scratch copy of `sdk/` with the names put back builds upstream's engine
  bit for bit (`c22e900a...`), so our other source changes do not touch the engine either.

`BuildLauschwerkSdk` (csproj) runs the build when a Rust or TypeScript source, a manifest or the
toolchain file is newer than `dist/lauschwerk-engine.wasm`, and fails with a clear message when there is no
`cargo`. A change to the engine or the core changes the wasm's sha256, which the build writes into
`src/engine.generated.ts` (the loader checks the engine against it): commit it with the change.

The SDK's own tests run here: `cargo test --release -p lauschwerk-engine -p lauschwerk-core` (34) and, with
the models in `.cache/models` (`node dist/cli.js models .cache/models`), `npx vitest run` in
`sdk/packages/lauschwerk` (95 - the wasm's outputs against the native engine's, bit for bit). The pipeline
runs both before it packs.

**Diffing against upstream.** `scripts/diff-upstream.sh` copies the kept parts of `.reference/` to a
temporary folder, renames them with `scripts/upstream-names.py`, and diffs that against `sdk/`, so it
lists only the changes we made: today `crates/engine/README.md`, the SDK's `package.json`,
`scripts/build.mjs` (`tsc` run through node, not `npx`, which Windows cannot spawn), `scripts/models.mjs`, `src/loader.ts`, `src/manifest.ts`, `test/browser/network.spec.ts`, and the expected
`Cargo.toml`, `Cargo.lock`, `models.json` and golden hashes. Taking a newer upstream: replace
`.reference/`, run it with `-u`, re-copy, `python3 scripts/upstream-names.py tree <copied folder>`, carry
our changes over.

## The staged SDK

lauschwerk's `dist/` is already a browser build - an ES module entry (`index.js`), two shared chunks, a
chunk only Node imports (behind an `isNode()` guard and a dynamic `import()`, so a browser never fetches
it), the realtime worker and the AudioWorklet as plain scripts, and the engine `.wasm`.
`build/bundle-lauschwerk.mjs` copies those files **verbatim** from `sdk/packages/lauschwerk/dist` and writes
two of its own:

- **`lauschwerk.js`**, the entry Tesserae.Lauschwerk loads: it imports the SDK, calls
  `Lauschwerk.configure({ assetBaseUrl })` with its own folder, and publishes the module namespace as
  `window.lauschwerk` (plus `version`, `assetBaseUrl` and the engine's `engineSha256`, which
  `LauschwerkJs.EngineSha256` reads and the gallery's Models page shows).
- **`version.txt`**, the MSBuild `Inputs`/`Outputs` marker and what the targets read the version from
  (the `version` of `sdk/packages/lauschwerk/package.json`).

Two decisions in that layout, each load-bearing:

- **The SDK lives in `v<version>/`.** lauschwerk creates its worker (`new Worker(url)`) and worklet
  (`audioWorklet.addModule(url)`) itself, so those are never fetched through Transpose's loader and get
  no cache-busting token. They speak a private message protocol with the SDK, and nothing checks the two
  match - so an unversioned folder would let the first load after an upgrade pair a new SDK with a cached
  old worker. A folder named after the version changes every URL whenever the pin moves.
- **`assetBaseUrl` is always set.** Without it lauschwerk hands the worker and the worklet over as `blob:`
  URLs made from strings in `assets-*.js`, which needs `blob:` in a host's `script-src` and `worker-src`.
  With it they are same-origin files: `script-src 'self' 'wasm-unsafe-eval'; worker-src 'self'` is the
  whole policy.

The entry is fetched with **`Transpose.Require.RequireAsync(Transpose.RequireKind.Module, ...)`** -
fully qualified, because the enclosing `Tesserae` namespace has a `Require` of its own, and with
`RequireKind.Module` said out loud, because the URL ends in `.js` and the loader's sniffing would pick a
classic script, on which the entry's `import` is a syntax error. A failed load is forgotten
(`_loading = null`) so the next call retries.

**Not a Transpose resource**, for the same reasons as Monaco and pdf.js (see `tps.json`): every `.js`
resource gets a `<script>` tag, resources are renamed to `.min.js`, and the versioned folder cannot be
named in a single-level glob. The package carries it under `lauschwerk/` and
`buildTransitive/Tesserae.Lauschwerk.targets` copies it to `assets/js/lauschwerk/` after the build (`cli.js`
excluded - it is the Node model downloader, never something a page loads).

### Models: self-hosted by an opt-in MSBuild property

The models (the `.alk` weights) are not in the package. Each entry of `sdk/crates/core/models.json` has our
`name` and `file` (what the model is called here, and what a voiceprint or a word is bound to:
`name@sha256-prefix`), its `bytes` and `sha256`, and the `url` the default download reads - upstream's
Hugging Face repository at a pinned revision, the one place their name is spelled in our code. The `.alk`
format and its magic bytes (`ALK\0`) are part of the weights and keep their name; the sha256 is checked
on every download, whoever serves the file. By default the SDK downloads from `url` and keeps the files in
Cache Storage. `LauschwerkSelfHostModels=true` makes the targets run the SDK's own `cli.js models <dir> [names]` - from the staged `v<version>/` folder,
where it finds the engine, worker and worklet it also copies - into `obj/lauschwerk-models/<version>/`, once
per version and model set (a marker file is touched only after it succeeded), and copy the `.alk` files
to `assets/js/lauschwerk/models/`. The host then calls `LauschwerkJs.UseSelfHostedModels()` before the first
input. The gallery does both.

**`_LauschwerkSelfHostModels` runs `AfterTargets="_CopyLauschwerkAssets"`, not through `CallTarget`.** A called
target does not see properties the caller set while running: the first version ran
`node "v/cli.js"`, the version property empty. One that runs after the caller does see them.

A host that serves the models with `Cache-Control: no-store` still works, but Chromium reports the
streamed downloads as `net::ERR_ABORTED` in DevTools and to Playwright's `requestfailed`. Found through
the test server, which sent exactly that; it now sends a `Content-Length` and no cache header.

## lauschwerk is declared, not scripted

There is **no `Script.Write`** in this package. `src/Interop/LauschwerkLib.cs` declares the global `lauschwerk`
with `[External]`, `[Convention(Notation.None)]` and `[Name("lauschwerk")]`; payloads are `[External]`
interfaces read by direct cast; options are `[ObjectLiteral]` classes, which emit only the fields
assigned - so an unset option keeps lauschwerk's default instead of being overwritten by a C# `false` or `0`.
The public surface is plain C# (`CallInputSettings` with nullable properties, enums, wrappers) that writes
those literals; nothing `[External]` or `[ObjectLiteral]` is public except the read-only event payload
interfaces (`ISpeechEvent`, `IPhraseLevel`, `ILauschwerkDiag`, `IThresholds`).

Rules learned wiring it up, each found by a failure:

- **`IPromise` is in `System.Threading.Tasks`.** A file without that `using` fails with CS0246 on every
  `IPromise`, which reads like a missing package rather than a missing using.
- **Never `await` an `IPromise`** - `PromiseHelper` (copied from tesserae-pdf) is the one place a promise
  becomes a `Task`, and its selector form is how a wrapper is built *inside* the promise's callback.
- **A DOM `es5.Promise<T>` is not an `IPromise`, and the cast is not erased.** `(IPromise)(object)
  window.fetch(url)` compiles and throws `Unable to cast type Promise to type Transpose.IPromise` at run
  time - `IPromise` is a real BCL interface with metadata. Use Transpose.Core's own `.ToTask()` on it.
- **Arrays that reach lauschwerk's worker go through `Script.ToArray`.** The SDK posts words, languages and
  options to its worker, and a C# array carries a `$type` function `postMessage` cannot clone: the whole
  call fails with a `DataCloneError` quoting a constructor body. `WakeListenerSettings.WordList` and
  `EnumNames.Codes` do it; anything new that crosses must too.
- **A `DOMException.code` is a number.** `IJsError.code` is typed `object` and only a string is taken as
  lauschwerk's code (`Script.TypeOf`); reading a `DataCloneError`'s `25` as a string crashed in `StartsWith`.
- **Never `as`/`is` on an `[External]` type** - it throws instead of answering false. A direct cast emits
  nothing. (`is AgentInput` on the C# wrapper is fine.)
- **Transpose.Core's `MediaTrackConstraints` has no `noiseSuppression` / `autoGainControl`.** They are
  set with `Script.Set` in `Microphone.OpenAsync` - the one string-keyed write in the package. Everything
  else of the platform comes from `Transpose.Core` (`AudioContext`, `AnalyserNode`, `MediaStream`,
  `es5.Float32Array`, `es5.DataView`...): declare lauschwerk, not the browser.

## Behaviour worth preserving

- **lauschwerk holds the events it raises while an input is created, and replays them on the next task.**
  `LauschwerkJs.Create` builds the C# wrapper inside the promise's own callback (a microtask), so its
  handlers are attached before that replay; building it after an `await` could miss the first `mode`
  and `status`. The wrapper also seeds `Mode`/`Status` from the getters, so they are right regardless.
- **Subscribing to `diag` silences lauschwerk's own console warnings.** `VoiceInput` always subscribes (to
  forward them), so it writes `warn`-level ones to the console itself, once per code, while no C#
  handler listens. Global ones go through `LauschwerkJs.Diagnostic`, subscribed lazily.
- **Only a missing microphone fails an input.** Models loading or failing, a slow device: the input
  runs `VoiceMode.Raw` with a `RawReason` and lauschwerk retries. The components show the mode; they do not
  treat raw as an error.
- **Focus without a print keeps the closest voice - and a voice talking alone is the closest.** mix.wav's
  other speaker, who talks in the user's pause, loses under 1 dB without a print and 43 dB with the
  user's (measured in Node and in the gallery). That is lauschwerk's documented behaviour, not a bug: the
  Process File page demonstrates it with the "keep only the user's voice" toggle, and `near` is the
  print-free answer for a live input.
- **The components stop their microphone when they leave the DOM** (`DomObserver.WhenRemoved`), and the
  panels open the microphone themselves (`Microphone.OpenAsync`, lauschwerk's constraints) so they can meter
  the raw track beside the processed one; lauschwerk never stops a stream it did not open, so they stop it.
- **One shared `AudioContext` for every `LevelMeter`,** and meters and waveforms animate only while on
  screen (`MountedLoop`). A canvas component reads colours from the `--tss-*` tokens when it draws and
  watches `LauschwerkStyles.ThemeGeneration` rather than subscribing to the static `Theme.OnThemeChanged`
  per instance, which would keep every component ever built alive.
- **`LauschwerkStyles`** injects the components' few rules once; every colour is a token, and the three with
  no token are `--tss-lw-*` variables a theme can restate.

## The sample gallery

Shaped like tesserae-monaco's: `App.cs` finds every `ISample` by reflection (`tps.json` needs
`"reflection": { "target": "inline" }`), `[SampleDetails]` gives group, order and icon, the class name
is the page name and route (`CallWakeWordsSample` → `#/view/Call%20Wake%20Words`), pages are built when
opened and torn down when left. Every page works with a real microphone and with the recordings: the
Process File page plays them to the models as files, and the browser checks play them as the microphone.
Read-outs the checks rely on carry stable ids (`#alk-...`) and `data-*` attributes on the components
(`data-mode`, `data-wakes`, `data-phrases`, `data-progress`, `data-enrolled`, `data-ready`, `data-state`).

## The speech fixtures

`tools/Lauschwerk.TestAudio` synthesizes them with [Voxwerk](https://github.com/curiosity-ai/voxwerk)
(`Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice`, ~1.8 GB, downloaded once to `~/.cache/voxwerk`) - two built-in
speakers, serena as the user and ryan as someone else, fixed seeds, laid out on a 16 kHz timeline with
seeded noise. `dotnet run -c Release --project tools/Lauschwerk.TestAudio` rewrites
`Tesserae.Lauschwerk.Sample/assets/audio/`; commit the WAVs. A change to one layout can shift the noise of
the files after it (one generator), which is harmless.

What each is for, verified against lauschwerk in Node before the gallery used them: `speech.wav` → Cue
phrases 1.06-3.58 s and 4.93-8.20 s; `wake.wav` → one "Hey Nova" wake at 1.69 s (after a pause);
`wake-takes.wav` → three takes; `enroll.wav` → a print that verifies `voice-a.wav` (0.83, same) and
rejects `voice-b.wav` (0.06); `noise.wav` → nothing; `mix.wav` → see Focus above.

## Verifying changes

`dotnet build` proves it compiles, not that it works - nearly every failure here is a runtime one, and
the DataCloneError above compiled, built, loaded and only failed on a click. Run the browser checks:

```bash
dotnet build Tesserae.Lauschwerk.Sample/Tesserae.Lauschwerk.Sample.csproj
cd tests && npm ci && npx playwright test          # 15 checks, ~1.5 min
LAUSCHWERK_SITE=../Tesserae.Lauschwerk.Sample/bin/Release/netstandard2.0/tps LAUSCHWERK_PORT=5005 npx playwright test
```

Chromium is preinstalled at `/opt/pw-browsers/` in the cloud sessions (`@playwright/test` is pinned to
the matching 1.63.0); do not run `playwright install`. Each spec launches its own Chromium with
`--use-fake-device-for-media-stream --use-file-for-fake-audio-capture=<fixture>`; the file **loops**, so
a wake listener on `wake.wav` wakes again 7.23 s later. Every spec asserts the console free of errors and
warnings and no failed request.

Build in **Release** at least once before shipping: only a Release site is chunked (`outputBy: "Module"`;
the package itself defers all its chunks), so a Debug-only pass never loads the module entry. And test in
both themes and modes the gallery offers (default and Curiosity, light and dark - `?theme=curiosity`,
the sun/moon command): the components draw with tokens, and the Curiosity theme restyles the Tesserae
pieces they are made of.

## Publishing

- **NuGet:** `.azure-devops/build-nuget-h5.yml`, shaped like Tesserae's pipeline of the same name: on
  `main`, versions the package **`yy.M.<build id>`** (as Tesserae's newer packages, the themes, do),
  installs the pinned Rust toolchain, builds Lauschwerk from source, runs the cargo and vitest suites, builds
  the package and the sample in Release, runs the Playwright checks against it, and pushes. It builds on
  **Linux**: on Windows the engine's bytes move (cargo spells the source paths in its panic messages with
  backslashes, and autocrlf rewrites the `models.json` the core embeds), so the hash would not be the one
  committed - a step fails the build if `engine.generated.ts` changes. Only the push runs on Windows, in a
  second job, because `NuGetCommand@2` needs Mono and nuget.org's API-key service connection cannot be
  handed to `dotnet nuget push`. The version
  variable is `buildVersion`, passed as `/p:Version` - never name a pipeline variable after an MSBuild
  property, since every variable is also an environment variable and MSBuild reads those as properties
  (Tesserae's `packageVersion` incident).
- **GitHub Pages:** `.github/workflows/pages.yml` builds the gallery in Release (models self-hosted) and
  publishes `_site/` to `gh-pages`. Everything in the site is relative, so it works under a sub-path.

## Naming

The project is **Lauschwerk** (German *lauschen*, to listen closely, and *Werk*, works - after Voxwerk,
which speaks where this listens). The package is `Tesserae.Lauschwerk`, the namespace
`Tesserae.Lauschwerk`, the C# facade `LauschwerkJs`, the component factory `LauschwerkUI`, the JavaScript
global `window.lauschwerk`, the crates `lauschwerk-engine` / `-core` / `-wasm`, the engine file
`lauschwerk-engine.wasm`, the CSS prefix `tss-lw-` (variables `--tss-lw-*`), the gallery's ids `lw-*`.

Upstream's license grants no right to its name or logo, and the README credits the origin. Keep the old
name out of everything but the three places listed under "What this is"; `scripts/upstream-names.py`
(`text` mode) renames anything copied from `.reference/`. The GitHub repository itself is still named
after the fork until it is renamed; the code already links to `theolivenbaum/lauschwerk` (GitHub redirects
the old name to the new one after a rename, not the other way round).
