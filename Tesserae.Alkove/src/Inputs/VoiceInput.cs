using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Alkove
{
    /// <summary>
    /// What a call input, an agent input and a wake listener share: the microphone, the mode the models
    /// are in, the speech events, and the controls. Created by <c>AlkoveJs.Create*Async</c>; one of
    /// <see cref="CallInput"/>, <see cref="AgentInput"/> or <see cref="WakeListener"/>.
    ///
    /// The events are C# events, wired to alkove's once, as the input is created. alkove holds every
    /// event it raises while the input is being built and replays them on the next task, so subscribing
    /// right after <c>await</c> still sees the first <see cref="ModeChanged"/> - and <see cref="Mode"/>,
    /// <see cref="Status"/> are always readable as state.
    ///
    /// Creating an input only fails when there can be no sound (an <see cref="AlkoveException"/>:
    /// <c>mic.denied</c> and the like). Everything else - models still downloading, a failed download, a
    /// device too slow - leaves it running <see cref="VoiceMode.Raw"/> with a <see cref="RawReason"/>,
    /// and the SDK retries on its own.
    /// </summary>
    public abstract class VoiceInput : IDisposable
    {
        internal readonly IAlkoveInputJs Js;

        private readonly DisposableBag     _subscriptions = new DisposableBag();
        private readonly HashSet<string>   _warned        = new HashSet<string>();

        private VoiceMode   _mode;
        private RawReason   _reason;
        private InputStatus _status;
        private bool        _speaking;
        private double      _learnProgress;
        private Voiceprint  _voiceprint;
        private bool        _disposed;

        internal VoiceInput(IAlkoveInputJs js)
        {
            Js = js;

            _mode       = EnumNames.Mode(js.mode);
            _reason     = _mode == VoiceMode.Raw ? RawReason.Loading : RawReason.None;
            _status     = EnumNames.Status(js.status);
            _voiceprint = Voiceprint.Wrap(js.voiceprint);

            On("mode", e =>
            {
                var m = (IModeEventJs)e;
                _mode   = EnumNames.Mode(m.mode);
                _reason = EnumNames.Reason(m.reason);
                ModeChanged?.Invoke(_mode, _reason);
                RaiseChanged();
            });

            On("status", e =>
            {
                _status = EnumNames.Status((string)e);
                StatusChanged?.Invoke(_status);
                RaiseChanged();
            });

            On("speechstart", e =>
            {
                _speaking = true;
                SpeechStarted?.Invoke((ISpeechEvent)e);
                RaiseChanged();
            });

            On("speechend", e =>
            {
                _speaking = false;
                SpeechEnded?.Invoke((ISpeechEvent)e);
                RaiseChanged();
            });

            On("speechcancel", e =>
            {
                _speaking = false;
                SpeechCancelled?.Invoke((ISpeechEvent)e);
                RaiseChanged();
            });

            On("learn", e =>
            {
                _learnProgress = ((ILearnEventJs)e).progress;
                LearnProgress?.Invoke(_learnProgress);
                RaiseChanged();
            });

            On("voiceprint", e =>
            {
                _voiceprint = Voiceprint.Wrap((IVoiceprintJs)e);
                VoiceprintLearned?.Invoke(_voiceprint);
                RaiseChanged();
            });

            On("mic", e => MicStateChanged?.Invoke(((IMicEventJs)e).state));
            On("trackchange", e => TrackChanged?.Invoke((MediaStreamTrack)e));
            On("wake", e => Wake?.Invoke(new WakeEvent((IWakeEventJs)e)));

            // Listening to `diag` silences alkove's own console warnings, so they are written here instead,
            // once per code, for as long as nobody in C# listens - what degrades an input stays visible.
            On("diag", e =>
            {
                var d = (IAlkoveDiag)e;

                if (Diagnostic is object)
                {
                    Diagnostic(d);
                    return;
                }

                if (d.level == "warn" && _warned.Add(d.code)) console.warn("alkove: " + (d.message ?? d.code), d);
            });
        }

        /// <summary>Subscribes to one of alkove's events for as long as the input lives.</summary>
        private protected void On(string type, Action<object> handler)
        {
            var off = Js.on(type, handler);
            _subscriptions.Add(() => off());
        }

        // -----------------------------------------------------------------------------------------
        // State.

        /// <summary>What goes out right now.</summary>
        public VoiceMode Mode => _mode;

        /// <summary>Why <see cref="Mode"/> is raw; <see cref="RawReason.None"/> when it is not.</summary>
        public RawReason RawReason => _reason;

        public InputStatus Status => _status;

        /// <summary>True between a phrase's start and its end (Cue; never on a wake listener).</summary>
        public bool IsSpeaking => _speaking;

        /// <summary>Milliseconds between the microphone and the output: ~150 on a call, ~50 on an agent.</summary>
        public double LatencyMs => Js.latency;

        /// <summary>The learned (or given) voiceprint, or null.</summary>
        public Voiceprint Voiceprint => _voiceprint;

        /// <summary>How far learning the print has got, 0 to 1 (with <see cref="VoiceInputSettings.Learn"/>).</summary>
        public double LearnFraction => _learnProgress;

        /// <summary>
        /// The processed audio as a track (a call; an agent with <see cref="AgentInputSettings.WithTrack"/>),
        /// or null. On a call it can change when the audio clock stalls - see <see cref="CallInput.Bind"/>.
        /// </summary>
        public MediaStreamTrack Track => Js.track;

        public bool IsDisposed => _disposed;

        /// <summary>The input's clock in seconds: the clock every event time is on.</summary>
        public double Now() => Js.now();

        // -----------------------------------------------------------------------------------------
        // Events.

        /// <summary>Any of the state above changed - what a component redraws on.</summary>
        public event Action Changed;

        public event Action<VoiceMode, RawReason> ModeChanged;
        public event Action<InputStatus> StatusChanged;

        /// <summary>A phrase began (seconds on the input's clock).</summary>
        public event Action<ISpeechEvent> SpeechStarted;

        /// <summary>A phrase ended.</summary>
        public event Action<ISpeechEvent> SpeechEnded;

        /// <summary>The phrase under way was dropped (<see cref="Listen"/> turned off).</summary>
        public event Action<ISpeechEvent> SpeechCancelled;

        public event Action<double> LearnProgress;

        /// <summary>The print became ready, or changed meaningfully. Store it with <see cref="Voiceprint.ToJson"/>.</summary>
        public event Action<Voiceprint> VoiceprintLearned;

        /// <summary><c>lost</c>, <c>reopened</c>, <c>denied</c>, <c>muted</c> or <c>unmuted</c>.</summary>
        public event Action<string> MicStateChanged;

        public event Action<MediaStreamTrack> TrackChanged;

        /// <summary>A wake word was heard (a wake listener, or words added with <see cref="Spot"/>).</summary>
        public event Action<WakeEvent> Wake;

        /// <summary>The input's diagnostics. While nothing listens, warnings go to the console once per code.</summary>
        public event Action<IAlkoveDiag> Diagnostic;

        private void RaiseChanged() => Changed?.Invoke();

        // -----------------------------------------------------------------------------------------
        // Controls.

        /// <summary>
        /// Resolves once the models run. A failed load attempt faults it (<c>models.fetch</c>,
        /// <c>models.hash</c>, <c>engine.failed</c>); the SDK retries and a later call waits for the next
        /// attempt. A first visit downloads several MB - put your own timeout around it.
        /// </summary>
        public async Task ReadyAsync()
        {
            try
            {
                await PromiseHelper.ToTask(Js.ready);
            }
            catch (Exception e)
            {
                throw AlkoveException.FromJs(e);
            }
        }

        /// <summary>Off: the raw microphone goes out (<see cref="RawReason.Off"/>); the models keep running, so on is instant. True when they run.</summary>
        public Task<bool> SetEnabledAsync(bool on) => PromiseHelper.ToTask<bool>(Js.setEnabled(on));

        /// <summary>Off: no phrase opens, and the one under way is cancelled. The audio keeps flowing.</summary>
        public void Listen(bool on) => Js.listen(on);

        /// <summary>Hold to talk: the phrase stays open through pauses until <see cref="Release"/>.</summary>
        public void Hold() => Js.hold();

        public void Release() => Js.release();

        /// <summary>How much of what Focus removes is removed, 0 to 1; fades over 10 ms.</summary>
        public void SetStrength(double strength) => Js.setStrength(strength);

        /// <summary>Keep only this voice from the next pause on; null forgets it.</summary>
        public void SetVoiceprint(Voiceprint print)
        {
            if (print is null) Js.forgetVoiceprint();
            else Js.setVoiceprint(print.Js);
        }

        /// <summary>For <see cref="VoiceInputSettings.FarEndManual"/>: the other side (an agent) starts or stops speaking.</summary>
        public void FarEndActive(bool on) => Js.farEndActive(on);

        /// <summary>Seconds of speech, of audio, and the noise removed - with <see cref="VoiceInputSettings.Stats"/>; null otherwise.</summary>
        public async Task<InputStats> StatsAsync()
        {
            var s = await PromiseHelper.ToTask<IStatsJs>(Js.stats());
            return s is null ? null : new InputStats(s.speechSeconds, s.seconds, s.noiseRemovedDb);
        }

        /// <summary>The next <paramref name="seconds"/> (up to 600) as a stereo 16 kHz WAV: microphone left, output right.</summary>
        public async Task<Blob> RecordAsync(double seconds)
        {
            try
            {
                return await PromiseHelper.ToTask<Blob>(Js.record(seconds));
            }
            catch (Exception e)
            {
                throw AlkoveException.FromJs(e);
            }
        }

        /// <summary>The last <see cref="InputSourceSettings.KeepRecentSeconds"/>, as <see cref="RecordAsync"/> writes them; null without it.</summary>
        public Task<Blob> RecentAsync() => PromiseHelper.ToTask<Blob>(Js.recent());

        /// <summary>When <see cref="Status"/> is <see cref="InputStatus.Blocked"/>: call it inside a click.</summary>
        public Task ResumeAsync() => PromiseHelper.ToTask(Js.resume());

        /// <summary>Switches to another microphone, keeping the output track.</summary>
        public Task SetDeviceAsync(string deviceId) => PromiseHelper.ToTask(Js.setDevice(deviceId));

        /// <summary>
        /// Stops the microphone and the audio context the input opened, and the SDK's workers once no input
        /// is left. Safe to call twice.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Js.dispose();
            _subscriptions.DisposeAll();
            _status = InputStatus.Disposed;
            RaiseChanged();
        }
    }

    /// <summary>What <see cref="VoiceInput.StatsAsync"/> counted.</summary>
    public sealed class InputStats
    {
        internal InputStats(double speechSeconds, double seconds, double? noiseRemovedDb)
        {
            SpeechSeconds  = speechSeconds;
            Seconds        = seconds;
            NoiseRemovedDb = noiseRemovedDb;
        }

        public double SpeechSeconds { get; }
        public double Seconds { get; }
        public double? NoiseRemovedDb { get; }
    }

    /// <summary>
    /// A call between two people (WebRTC): <see cref="VoiceInput.Track"/> carries the user's voice only,
    /// gated between sentences, at a steady level. <c>pc.addTrack(input.Track)</c>, then
    /// <see cref="Bind"/> the sender.
    /// </summary>
    public sealed class CallInput : VoiceInput
    {
        internal CallInput(IAlkoveInputJs js) : base(js) { }

        /// <summary>The SDK swaps the raw microphone into this sender by itself if the audio clock stalls.</summary>
        public void Bind(RTCRtpSender sender) => Js.bind(sender);

        /// <summary>Wake words heard on this input's microphone, behind Focus - one microphone, run once.</summary>
        public WakeWordHandle Spot(WakeWord[] words, WakeContext context = WakeContext.Anywhere, bool onlyMyVoice = false)
            => new WakeWordHandle(Js.spot(WakeListenerSettings.WordList(words), new SpotOptions { context = EnumNames.Context(context), onlyMyVoice = onlyMyVoice }));
    }

    /// <summary>
    /// A voice agent's input: continuous <see cref="Audio"/> packets (never gated) to stream to a
    /// speech-to-text service, and one <see cref="Utterance"/> per sentence with 300 ms of pre-roll.
    /// </summary>
    public sealed class AgentInput : VoiceInput
    {
        internal AgentInput(IAlkoveInputJs js, AgentInputSettings settings) : base(js)
        {
            Format     = settings?.Format ?? PcmFormat.Float32;
            SampleRate = settings?.SampleRate ?? AudioClip.ALKOVE_RATE;

            On("audio", e => Audio?.Invoke((es5.ArrayBuffer)e));
            On("utterance", e =>
            {
                var u = (IUtteranceEventJs)e;
                Utterance?.Invoke(new Utterance(u.pcm, u.start, u.end, u.level, Format, SampleRate));
            });
        }

        public PcmFormat Format { get; }
        public int SampleRate { get; }

        /// <summary>A packet of audio, every chunk (32 ms by default), whether or not anyone speaks.</summary>
        public event Action<es5.ArrayBuffer> Audio;

        /// <summary>One sentence, from its pre-roll to its end.</summary>
        public event Action<Utterance> Utterance;

        public WakeWordHandle Spot(WakeWord[] words, WakeContext context = WakeContext.Anywhere, bool onlyMyVoice = false)
            => new WakeWordHandle(Js.spot(WakeListenerSettings.WordList(words), new SpotOptions { context = EnumNames.Context(context), onlyMyVoice = onlyMyVoice }));
    }

    /// <summary>One sentence from an <see cref="AgentInput"/>.</summary>
    public sealed class Utterance
    {
        internal Utterance(es5.ArrayBuffer pcm, double start, double end, IPhraseLevel level, PcmFormat format, int sampleRate)
        {
            Pcm        = pcm;
            Start      = start;
            End        = end;
            Level      = level;
            Format     = format;
            SampleRate = sampleRate;
        }

        /// <summary>The samples, in the input's <see cref="Format"/>.</summary>
        public es5.ArrayBuffer Pcm { get; }

        public double Start { get; }
        public double End { get; }
        public IPhraseLevel Level { get; }
        public PcmFormat Format { get; }
        public int SampleRate { get; }

        /// <summary>The sentence as a clip (float samples), whatever the format.</summary>
        public AudioClip ToClip()
        {
            if (Format == PcmFormat.Float32) return AudioClip.FromFloat32(Pcm, SampleRate);

            var ints = new es5.Int16Array(Pcm);
            var n = (int)ints.length;
            var floats = new es5.Float32Array((uint)n);
            for (var i = 0; i < n; i++) floats[(uint)i] = ints[(uint)i] / 32768f;
            return new AudioClip(floats, SampleRate);
        }
    }

    /// <summary>
    /// Listens for wake words, with Spot alone on the raw microphone - cheap enough to leave on. Raises
    /// <see cref="VoiceInput.Wake"/> with the word's audio, and <see cref="Heard"/> every 300 ms.
    /// </summary>
    public sealed class WakeListener : VoiceInput
    {
        internal WakeListener(IAlkoveInputJs js) : base(js)
        {
            On("heard", e =>
            {
                var h = (IHeardEventJs)e;
                var scores = new Dictionary<string, double>();
                var list = h.scores;
                if (list is object) for (var i = 0; i < list.Length; i++) scores[list[i].word] = list[i].score;
                Heard?.Invoke(h.speech, scores);
            });
        }

        /// <summary>Every 300 ms: the speech probability, and each word's best score - for a live meter.</summary>
        public event Action<double, Dictionary<string, double>> Heard;
    }

    /// <summary>Words added to a call or agent input with <c>Spot(...)</c>; dispose to stop hearing them.</summary>
    public sealed class WakeWordHandle : IDisposable
    {
        private readonly ISpotHandleJs _js;
        private readonly Action        _off;

        internal WakeWordHandle(ISpotHandleJs js)
        {
            _js  = js;
            _off = js.on("wake", e => Wake?.Invoke(new WakeEvent((IWakeEventJs)e)));
        }

        public event Action<WakeEvent> Wake;

        public void Dispose()
        {
            _off();
            _js.dispose();
        }
    }
}
