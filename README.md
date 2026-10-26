# Tesserae.Alkove

On-device voice input for [Tesserae](https://github.com/curiosity-ai/tesserae) applications, built on the
[alkove](https://www.npmjs.com/package/alkove) web SDK: keep only the user's voice, tell when they speak,
hear any typed wake word, recognize a voice - in the browser, in WebAssembly, with no server and no key.

> **Unofficial.** A wrapper that bundles the pinned `alkove` npm release unmodified (Apache-2.0, code and
> weights). alkove itself is a preview (0.x). This repository started as a fork of alkove's monorepo;
> that source is kept, unbuilt, in [`.reference/`](./.reference) for reading.

## What is in it

| | |
|---|---|
| [`Tesserae.Alkove`](./Tesserae.Alkove) | the NuGet package: typed C# over the SDK, and Tesserae components ([its README](./Tesserae.Alkove/README.md)) |
| [`Tesserae.Alkove.Sample`](./Tesserae.Alkove.Sample) | the gallery: one page per feature |
| [`tests`](./tests) | Playwright checks - Chromium plays synthesized speech as the microphone |
| [`tools/Alkove.TestAudio`](./tools/Alkove.TestAudio) | synthesizes that speech with [Voxwerk](https://github.com/curiosity-ai/voxwerk) |

## Quick look

```csharp
using Tesserae.Alkove;
using static Tesserae.Alkove.AlkoveUI;

AlkoveJs.UseSelfHostedModels();                       // with <AlkoveSelfHostModels>true</AlkoveSelfHostModels>

// a call: only the user's voice goes out
var call = await AlkoveJs.CreateCallInputAsync(new CallInputSettings { Learn = true });
pc.addTrack(call.Track);

// any word is a wake word
var word = await AlkoveJs.CompileWordAsync("Hey Nova");
var wake = await AlkoveJs.CreateWakeListenerAsync(new WakeListenerSettings(word));
wake.Wake += e => console.log("woke", e.Score);

// or the ready-made components
VStack().Children(CallInputPanel(), WakeWordPanel("Hey Nova"), VoiceprintEnrollment(), AudioFileProcessor());
```

## Build, run, test

```bash
dotnet tool update --global Transpose.Compiler
dotnet build Tesserae.Alkove.slnx                      # needs node: stages the SDK, downloads the models once

cd Tesserae.Alkove.Sample/bin/Debug/netstandard2.0/tps && dotnet serve --port 5000

cd tests && npm ci && npx playwright test             # the gallery must be built first
```

[CLAUDE.md](./CLAUDE.md) has how it is put together and why; [TODO.md](./TODO.md) what is left.

## License

Apache-2.0, like alkove. See [LICENSE](./LICENSE) and [NOTICE](./NOTICE). The Apache license grants no
right to the Alkove name or logo.
