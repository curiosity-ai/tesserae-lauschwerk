# TODO

## Package

- [ ] `Alkove.stream()` (an async generator over long recordings, part by part) is not wrapped:
      `ProcessAsync` holds the whole file in memory, fine to about an hour of 16 kHz audio.
- [ ] `restart`, `setStream` and `recent` exist on `VoiceInput` only partly (`RecentAsync` yes, the other
      two not); `AudioContext` injection (`audioContext` option) is not exposed.
- [ ] `trace` (per-frame model scores) is not exposed; a debugging page drawing it on the `Waveform`
      would make threshold tuning visible.
- [ ] `AgentInputSettings` has no `output.port` (a `MessagePort` that keeps audio off the main thread).
- [ ] A `MicrophonePicker` component (enumerate devices, `SetDeviceAsync`, follow the default).
- [ ] Localization: the components' strings are English literals. Route them through Tesserae's TNT
      table (as tesserae-pdf does) or a text object the host supplies (as tesserae-monaco's settings do).
- [ ] `Waveform` draws the whole clip; long recordings want a zoomable view or a min/max pyramid.
- [ ] Theme files: the components use `--tss-*` tokens and look right in Default and Curiosity; check the
      other Tesserae themes (Stratus, Rechenwerk, Weniger, ...) and add `--tss-alkove-*` overrides where a
      theme's own look calls for it.

## Tests

- [ ] A check for `OnlyMyVoice` on a live wake listener (`wake-other.wav` should not wake it once the
      user's print is set) and for `Near` on a call input.
- [ ] Teardown counts: after leaving a page, no `AudioContext`, worker or microphone track left open
      (Chromium exposes neither directly; count `MediaStreamTrack.readyState` on streams the page held).
- [ ] Run the specs in CI on every push (a workflow like pages.yml, without the publish).
- [ ] A packaged-consumer check in CI: pack, restore from a local feed, build a project whose only
      reference is the package, confirm `assets/js/alkove/` lands in its output.

## Upstream and sdk/

- [ ] Taking a newer upstream: replace `.reference/`, `scripts/diff-upstream.sh -u` for our changes,
      re-copy into `sdk/`, carry them over. Then re-check the dist layout (`bundle-alkove.mjs` fails
      loudly if a required file moved), the event and option names in `src/Interop/` against
      `dist/*.d.ts`, the model manifest, and re-run the fixtures through `tests/` - the thresholds in
      `primitives.spec.js` are model values. Bump the npm fallback pin in `Tesserae.Alkove/package.json`.
- [ ] The wasm32-wasip1 run of the cargo tests (upstream's CI runs them under wasmtime with SIMD on and
      off) is not in our pipeline yet: it checks the engine on the target we ship, not only natively.
- [ ] A check in the pipeline that `sdk/` built from source still matches the npm release while we have
      not changed it (the two are byte-identical for 0.3.2) - it catches a toolchain drift early.
