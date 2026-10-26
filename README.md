# Lauschwerk

On-device voice input for the browser, and for [Tesserae](https://github.com/curiosity-ai/tesserae)
applications: keep only the user's voice, tell when they speak, hear any typed wake word, recognize a
voice. A Rust engine compiled to WebAssembly, a TypeScript web SDK around it, and a Tesserae package with
typed C# and ready-made components. No server, no key.

*Lauschen*, to listen closely - the counterpart of [Voxwerk](https://github.com/curiosity-ai/voxwerk),
which speaks.

## What is in it

| | |
|---|---|
| [`sdk`](./sdk) | Lauschwerk itself: the Rust engine and core, built to WebAssembly, and the TypeScript web SDK ([its README](./sdk/README.md)) |
| [`Tesserae.Lauschwerk`](./Tesserae.Lauschwerk) | the NuGet package: typed C# over the SDK, and Tesserae components ([its README](./Tesserae.Lauschwerk/README.md)) |
| [`Tesserae.Lauschwerk.Sample`](./Tesserae.Lauschwerk.Sample) | the gallery: one page per feature |
| [`tests`](./tests) | Playwright checks - Chromium plays synthesized speech as the microphone |
| [`tools/Lauschwerk.TestAudio`](./tools/Lauschwerk.TestAudio) | synthesizes that speech with Voxwerk |

## Quick look

```csharp
using Tesserae.Lauschwerk;
using static Tesserae.Lauschwerk.LauschwerkUI;

// a call: only the user's voice goes out
var call = await LauschwerkJs.CreateCallInputAsync(new CallInputSettings { Learn = true });
pc.addTrack(call.Track);

// any word is a wake word
var word = await LauschwerkJs.CompileWordAsync("Hey Nova");
var wake = await LauschwerkJs.CreateWakeListenerAsync(new WakeListenerSettings(word));
wake.Wake += e => console.log("woke", e.Score);

// or the ready-made components
VStack().Children(CallInputPanel(), WakeWordPanel("Hey Nova"), VoiceprintEnrollment(), AudioFileProcessor());
```

## Build, run, test

```bash
dotnet tool update --global Transpose.Compiler
dotnet build Tesserae.Lauschwerk.slnx                 # needs Rust (rustup) and node: builds the wasm and the SDK, downloads the models once

cd Tesserae.Lauschwerk.Sample/bin/Debug/netstandard2.0/tps && dotnet serve --port 5000

cd tests && npm ci && npx playwright test             # the gallery must be built first
```

[CLAUDE.md](./CLAUDE.md) has how it is put together and why; [TODO.md](./TODO.md) what is left. Packages
are versioned `yy.M.<build>` by [the pipeline](./.azure-devops/build-nuget-h5.yml).

## Origin and license

Lauschwerk began as a fork of [alkove](https://github.com/alkove-ai/alkove) by Eddy Bordi, and its engine,
core, web SDK and model weights are that project's work, under Apache-2.0. This repository renamed it, builds
it from source, and adds the Tesserae package, the gallery and the tests. Upstream as forked is kept,
unchanged and unbuilt, in [`.reference/`](./.reference), so `scripts/diff-upstream.sh` can show what we
changed. The five models are upstream's, fetched once from its Hugging Face repositories
(`sdk/crates/core/models.json`) and committed under `Tesserae.Lauschwerk/models/`, so the package carries them.

Apache-2.0, like upstream. See [LICENSE](./LICENSE) and [NOTICE](./NOTICE). The license grants no right to
upstream's name or logo, which this project does not use.
