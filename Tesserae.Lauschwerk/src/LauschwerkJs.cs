using System;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// The factories: the three inputs, files, words. Each loads the SDK first (<see cref="LoadAsync"/>),
    /// so they can be called straight from a click handler.
    /// </summary>
    public static partial class LauschwerkJs
    {
        /// <summary>
        /// A call input: opens the microphone (the browser asks the user) and gives a track with the user's
        /// voice only. Resolves as soon as the audio runs - the models load in the background, raw until then.
        /// </summary>
        public static async Task<CallInput> CreateCallInputAsync(CallInputSettings settings = null)
        {
            await LoadAsync();
            return await Create(LauschwerkLib.createCallInput((settings ?? new CallInputSettings()).ToJs()), js => new CallInput(js));
        }

        /// <summary>An agent input: continuous audio packets and one utterance per sentence. See <see cref="AgentInput"/>.</summary>
        public static async Task<AgentInput> CreateAgentInputAsync(AgentInputSettings settings = null)
        {
            await LoadAsync();
            return await Create(LauschwerkLib.createAgentInput((settings ?? new AgentInputSettings()).ToJs()), js => new AgentInput(js, settings));
        }

        /// <summary>A wake word listener. Compile its words first with <see cref="CompileWordAsync"/>.</summary>
        public static async Task<WakeListener> CreateWakeListenerAsync(WakeListenerSettings settings)
        {
            if (settings?.Words is null || settings.Words.Length == 0) throw new ArgumentException("A wake listener needs at least one word.", nameof(settings));
            await LoadAsync();
            return await Create(LauschwerkLib.createWakeListener(settings.ToJs()), js => new WakeListener(js));
        }

        /// <summary>
        /// The wrapper is built inside the promise's own callback, a microtask, rather than after the await:
        /// lauschwerk replays the events it held during creation on the next task, and the wrapper's handlers
        /// have to be attached by then or the first mode and status are lost.
        /// </summary>
        private static async Task<T> Create<T>(IPromise promise, Func<ILauschwerkInputJs, T> wrap)
        {
            try
            {
                return await PromiseHelper.ToTask(promise, resolved => wrap((ILauschwerkInputJs)resolved));
            }
            catch (Exception e)
            {
                throw LauschwerkException.FromJs(e);
            }
        }

        /// <summary>
        /// Compiles a typed word for Spot - no recording, no training. Lex reads it in each language given
        /// (English by default). Text between slashes is read as IPA (<c>/heɪ ˈnoʊvə/</c>) and skips Lex.
        /// </summary>
        public static async Task<WakeWord> CompileWordAsync(string text, params SpeechLanguage[] languages)
        {
            await LoadAsync();

            try
            {
                return await PromiseHelper.ToTask(LauschwerkLib.Spot.compile(text, new CompileOptions { lang = EnumNames.Codes(languages) }), w => new WakeWord((IWordJs)w));
            }
            catch (Exception e)
            {
                throw LauschwerkException.FromJs(e);
            }
        }

        /// <summary>
        /// Enrolls a wake word: opens the microphone, and the user says the word <paramref name="takes"/>
        /// times. Its thresholds are then set on their voice, and a voiceprint comes with it. Follow it with
        /// <see cref="WordEnrollment.Take"/> and await <see cref="WordEnrollment.DoneAsync"/>.
        /// </summary>
        public static async Task<WordEnrollment> EnrollWordAsync(string text, SpeechLanguage language = SpeechLanguage.English, int takes = 3, InputSourceSettings source = null)
        {
            await LoadAsync();
            var options = new EnrollWordOptions { lang = EnumNames.Codes(new[] { language }), takes = takes };
            if (source?.Stream is object) options.stream = source.Stream;
            if (!string.IsNullOrEmpty(source?.DeviceId)) options.deviceId = source.DeviceId;
            return new WordEnrollment(LauschwerkLib.Spot.enroll(text, options));
        }

        /// <summary>
        /// Runs the models over a recording, faster than real time, in a background worker - the same
        /// output as the live inputs give for the same 16 kHz audio. <paramref name="audio"/> is anything
        /// the browser can decode: a <c>File</c> or <c>Blob</c>, its bytes, or an <c>AudioBuffer</c>.
        /// </summary>
        public static async Task<ProcessedAudio> ProcessAsync(object audio, ProcessSettings settings = null, Action<double> progress = null)
        {
            await LoadAsync();

            var options = (settings ?? new ProcessSettings()).ToJs();
            if (progress is object) options.onProgress = p => progress(p.total > 0 ? p.done / p.total : 0);

            try
            {
                return await PromiseHelper.ToTask(LauschwerkLib.Lauschwerk.process(audio, options), r => new ProcessedAudio((IProcessResultJs)r));
            }
            catch (Exception e)
            {
                throw LauschwerkException.FromJs(e);
            }
        }

        /// <summary><see cref="ProcessAsync(object, ProcessSettings, Action{double})"/> over samples already decoded.</summary>
        public static Task<ProcessedAudio> ProcessAsync(AudioClip clip, ProcessSettings settings = null, Action<double> progress = null)
            => ProcessAsync(new PcmInput { pcm = clip.Samples, rate = clip.SampleRate }, settings, progress);

        /// <summary>Fetches a recording and runs <see cref="ProcessAsync(object, ProcessSettings, Action{double})"/> over it.</summary>
        public static async Task<ProcessedAudio> ProcessUrlAsync(string url, ProcessSettings settings = null, Action<double> progress = null)
        {
            var response = await window.fetch(url).ToTask();
            if (!response.ok) throw new Exception("HTTP " + response.status + ": " + url);
            var bytes = await response.arrayBuffer().ToTask();
            return await ProcessAsync(bytes, settings, progress);
        }

        /// <summary>Decodes a recording to 16 kHz mono, without running any model - for drawing or playing it.</summary>
        public static Task<AudioClip> DecodeAsync(object audio) => ProcessAsync(audio, new ProcessSettings()).ContinueWith(t => t.Result.Raw);
    }

    /// <summary>Which models <see cref="LauschwerkJs.ProcessAsync(object, ProcessSettings, Action{double})"/> runs.</summary>
    public sealed class ProcessSettings
    {
        /// <summary>Focus: keep the closest voice (or <see cref="Voiceprint"/>'s), drop noise, music and other voices. Runs Cue too.</summary>
        public bool Focus { get; set; }

        /// <summary>Keep only this voice.</summary>
        public Voiceprint Voiceprint { get; set; }

        /// <summary>How much Focus removes, 0 to 1 (default 1).</summary>
        public double? Strength { get; set; }

        /// <summary>Cue: where speech is (<see cref="ProcessedAudio.Speech"/>).</summary>
        public bool Cue { get; set; }

        /// <summary>Spot: these wake words (<see cref="ProcessedAudio.Wakes"/>).</summary>
        public WakeWord[] Words { get; set; }

        /// <summary>When the words count. A file's default is <see cref="WakeContext.Anywhere"/>.</summary>
        public WakeContext? Context { get; set; }

        /// <summary>Level the output to -23 dB (off by default for files).</summary>
        public bool? Normalize { get; set; }

        internal ProcessOptionsJs ToJs()
        {
            var o = new ProcessOptionsJs();

            if (Focus)
            {
                if (Voiceprint is object || Strength.HasValue)
                {
                    var f = new FocusProcessJs();
                    if (Voiceprint is object) f.voiceprint = Voiceprint.Js;
                    if (Strength.HasValue) f.strength = Strength.Value;
                    o.focus = f;
                }
                else o.focus = true;
            }

            if (Cue) o.cue = true;

            if (Words is object && Words.Length > 0)
            {
                var spot = new SpotProcessJs { words = WakeListenerSettings.WordList(Words) };
                if (Context.HasValue) spot.context = EnumNames.Context(Context.Value);
                o.spot = spot;
            }

            if (Normalize.HasValue) o.normalize = Normalize.Value;
            return o;
        }
    }

    /// <summary>What <see cref="LauschwerkJs.ProcessAsync(object, ProcessSettings, Action{double})"/> found, all at 16 kHz and in seconds.</summary>
    public sealed class ProcessedAudio
    {
        private readonly IProcessResultJs _js;

        internal ProcessedAudio(IProcessResultJs js)
        {
            _js   = js;
            Audio = new AudioClip(js.audio);
            Raw   = new AudioClip(js.raw);

            var speech = js.speech;
            Speech = new SpeechSpan[speech?.Length ?? 0];
            for (var i = 0; i < Speech.Length; i++) Speech[i] = new SpeechSpan(speech[i].start, speech[i].end);

            var wakes = js.wakes;
            Wakes = new WakeEvent[wakes?.Length ?? 0];
            for (var i = 0; i < Wakes.Length; i++) Wakes[i] = new WakeEvent(wakes[i]);
        }

        /// <summary>The output: Focus's when it ran, the input resampled otherwise.</summary>
        public AudioClip Audio { get; }

        /// <summary>The input, decoded and resampled to 16 kHz mono - the same length as <see cref="Audio"/>.</summary>
        public AudioClip Raw { get; }

        /// <summary>Where Cue heard speech.</summary>
        public SpeechSpan[] Speech { get; }

        /// <summary>Where Spot heard a word.</summary>
        public WakeEvent[] Wakes { get; }

        /// <summary>A 16-bit WAV of the output.</summary>
        public Blob ToWav() => Wav.ToBlob(_js.toWav("audio"));

        /// <summary>A stereo WAV: the input on the left, the output on the right.</summary>
        public Blob ToStereoWav() => Wav.ToBlob(_js.toWav("both"));
    }
}
