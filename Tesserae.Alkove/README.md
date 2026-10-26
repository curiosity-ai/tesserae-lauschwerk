# Tesserae.Alkove

On-device voice input for [Tesserae](https://github.com/curiosity-ai/tesserae) applications: keep the
user's voice and drop the rest, tell when someone speaks, hear any typed wake word, recognize a voice.
A typed C# (Transpose) wrapper around the [alkove](https://www.npmjs.com/package/alkove) web SDK, plus
ready-made Tesserae components. Everything runs in the browser, in WebAssembly - no server, no key, no
telemetry.

> An unofficial wrapper. alkove is a preview (0.x) by its own authors, under Apache-2.0 (code and
> weights); this package bundles the pinned npm release unmodified.

## Install

```xml
<PackageReference Include="Tesserae.Alkove" Version="..." />
```

The build copies the SDK into your site at `assets/js/alkove/` (about 620 KB, the engine included).
Nothing is fetched until something needs it.

**Host the models yourself** (recommended; 7.5 MB for all five, downloaded once per alkove version at
build time, sha256-checked - needs `node` on the build machine):

```xml
<PropertyGroup>
  <AlkoveSelfHostModels>true</AlkoveSelfHostModels>
  <!-- optional, default all five: -->
  <AlkoveModels>focus cue spot</AlkoveModels>
</PropertyGroup>
```

```csharp
AlkoveJs.UseSelfHostedModels();   // once, before the first input
```

Without it the SDK downloads the models from Hugging Face in the browser - fine for trying it out, but
each user then sends Hugging Face their IP.

## A call (WebRTC)

```csharp
using Tesserae.Alkove;

var input = await AlkoveJs.CreateCallInputAsync(new CallInputSettings { Learn = true, FarEnd = remoteTrack });
pc.addTrack(input.Track);             // only the user's voice goes out
input.Bind(pc.getSenders()[0]);
input.VoiceprintLearned += print => localStorage.setItem("print", print.ToJson());
```

## A voice agent

```csharp
var input = await AlkoveJs.CreateAgentInputAsync(new AgentInputSettings { FarEndManual = true, Format = PcmFormat.Int16, SampleRate = 24000 });
input.Audio += pcm => socket.send(pcm);           // continuous 32 ms packets
input.Utterance += u => Transcribe(u.Pcm);        // one per sentence, 300 ms pre-roll
input.FarEndActive(true);                         // the agent starts speaking
```

## A wake word

```csharp
var word = await AlkoveJs.CompileWordAsync("Hey Nova", SpeechLanguage.English);   // any word, 7 languages
var wake = await AlkoveJs.CreateWakeListenerAsync(new WakeListenerSettings(word));
wake.Wake += e => StartListening(e.Audio);
```

## Files

```csharp
var r = await AlkoveJs.ProcessAsync(file, new ProcessSettings { Focus = true, Cue = true });
r.Audio;   // AudioClip, 16 kHz, processed
r.Speech;  // where speech is
r.ToWav(); // Blob
```

## Components

`using static Tesserae.Alkove.AlkoveUI;`

| Factory | What it is |
|---|---|
| `CallInputPanel()` / `AgentInputPanel()` | start/stop, mode, speaking, latency, microphone and output meters, learn progress |
| `WakeWordPanel("Hey Nova")` | type a word, pick a language, listen; live closeness and a list of wakes |
| `WordEnrollmentPanel("Hey Nova")` | the user says the word three times: a calibrated word and a voiceprint |
| `VoiceprintEnrollment()` | read a text aloud, get a voiceprint; `.RememberIn(key)` keeps it |
| `AudioFileProcessor()` | pick or drop a recording: before/after waveforms with speech and wakes, playable, downloadable |
| `ModelPreloader(AlkoveUsage.Call)` | a settings row that downloads a usage's models ahead of time |
| `LevelMeter()`, `Waveform(clip)`, `SpeechIndicator()`, `ModeBadge()` | the pieces the panels are made of |

They draw with Tesserae's `--tss-*` tokens, so they follow light and dark mode and the custom themes.

## Good to know

- **A near microphone, one person.** alkove keeps the closest voice (or the voice of a print). 16 kHz
  voice: the output carries speech up to 8 kHz.
- **Only a missing microphone fails an input** (`AlkoveException.Code`: `mic.denied`, `mic.not-found`,
  `mic.busy`, `page.insecure`, `browser.unsupported`). Models still loading, a failed download, a slow
  device: the input runs `VoiceMode.Raw` with a `RawReason`, and the SDK retries.
- **Content Security Policy:** `script-src 'self' 'wasm-unsafe-eval'; worker-src 'self'` - the worker
  and worklet are same-origin files, no `blob:` needed. Add the models' origin to `connect-src` if they
  are not on yours.
- **Privacy:** no network call besides the models. A voiceprint your app stores is biometric data
  (GDPR art. 9).

A sample gallery with a page per feature lives in the repository.
