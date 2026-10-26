using static Transpose.Core.dom;

namespace Tesserae.Alkove
{
    /// <summary>
    /// Where an input's sound comes from, and the options every input shares. Anything left unset keeps
    /// alkove's own default - nothing here is sent to the SDK unless it was set.
    /// </summary>
    public abstract class InputSourceSettings
    {
        /// <summary>A stream the host already opened. Open it with echoCancellation on and noiseSuppression,
        /// autoGainControl and voiceIsolation off, or the input raises a <c>mic.processed</c> diagnostic.
        /// The SDK never stops a stream it did not open.</summary>
        public MediaStream Stream { get; set; }

        /// <summary>A track the host already opened (see <see cref="Stream"/>).</summary>
        public MediaStreamTrack Track { get; set; }

        /// <summary>A microphone by id (<c>navigator.mediaDevices.enumerateDevices()</c>). Unknown: <c>mic.not-found</c>.</summary>
        public string DeviceId { get; set; }

        /// <summary>Follow the system's default microphone when it changes. alkove's default: on, when it opened the mic itself.</summary>
        public bool? FollowDefault { get; set; }

        /// <summary>Keeps the last N seconds (up to 600) for <see cref="VoiceInput.RecentAsync"/>.</summary>
        public double? KeepRecentSeconds { get; set; }

        internal void WriteSource(InputOptionsJs o)
        {
            if (Stream is object) o.stream = Stream;
            if (Track is object) o.track = Track;
            if (!string.IsNullOrEmpty(DeviceId)) o.deviceId = DeviceId;
            if (FollowDefault.HasValue) o.followDefault = FollowDefault.Value;
            if (KeepRecentSeconds.HasValue) o.keepRecent = KeepRecentSeconds.Value;
        }
    }

    /// <summary>Focus and Cue's options: what calls and agents share.</summary>
    public abstract class VoiceInputSettings : InputSourceSettings
    {
        /// <summary>Keep only this person's voice (<see cref="VoiceMode.FocusPrint"/>) from the start.</summary>
        public Voiceprint Voiceprint { get; set; }

        /// <summary>
        /// Learn the user's print while they talk (about 6 s of their voice), then keep only their voice.
        /// Raises <see cref="VoiceInput.LearnProgress"/> and <see cref="VoiceInput.VoiceprintLearned"/>.
        /// Set a <see cref="FarEnd"/> too, or the other side's voice may get into the print (an agent
        /// refuses to learn without one).
        /// </summary>
        public bool? Learn { get; set; }

        /// <summary>The other side's audio (a remote track), so their voice never counts as the user's.</summary>
        public MediaStreamTrack FarEnd { get; set; }

        /// <summary>Report the far end by hand, with <see cref="VoiceInput.FarEndActive"/> (an agent speaking).</summary>
        public bool FarEndManual { get; set; }

        /// <summary>How much of what Focus removes is removed, 0 to 1. alkove's default: 1.</summary>
        public double? Strength { get; set; }

        /// <summary>Level the voice to -23 dB. alkove's default for inputs: on.</summary>
        public bool? Normalize { get; set; }

        /// <summary>
        /// A voice 10 dB under the user's - a colleague across the room, a video on another device - opens
        /// no phrase. No voiceprint: it goes by loudness. <see cref="NearGapDb"/> tunes the 10.
        /// </summary>
        public bool? Near { get; set; }

        /// <summary>The gap for <see cref="Near"/>: 12 lets more through, 8 keeps more out.</summary>
        public double? NearGapDb { get; set; }

        /// <summary>Report each phrase's levels on <see cref="VoiceInput.SpeechEnded"/>.</summary>
        public bool? Levels { get; set; }

        /// <summary>Count speech and noise removed, for <see cref="VoiceInput.StatsAsync"/>.</summary>
        public bool? Stats { get; set; }

        /// <summary>Cue's probability to open a phrase (default 0.5).</summary>
        public double? SpeechConfidence { get; set; }

        /// <summary>Cue's probability under which a phrase may close (default 0.1).</summary>
        public double? SpeechExitConfidence { get; set; }

        /// <summary>Seconds of speech before a phrase opens (default 0.2).</summary>
        public double? SpeechStartSeconds { get; set; }

        /// <summary>Seconds of silence before a phrase closes (default 0.6).</summary>
        public double? SpeechStopSeconds { get; set; }

        internal void WriteVoice(InputOptionsJs o)
        {
            WriteSource(o);
            if (Voiceprint is object) o.voiceprint = Voiceprint.Js;
            if (Learn.HasValue) o.learn = Learn.Value;
            if (FarEnd is object) o.farEnd = FarEnd;
            else if (FarEndManual) o.farEnd = "manual";
            if (Strength.HasValue) o.strength = Strength.Value;
            if (Normalize.HasValue) o.normalize = Normalize.Value;
            if (NearGapDb.HasValue) o.near = new NearOptionsJs { gapDb = NearGapDb.Value };
            else if (Near.HasValue) o.near = Near.Value;
            if (Levels.HasValue) o.levels = Levels.Value;
            if (Stats.HasValue) o.stats = Stats.Value;

            if (SpeechConfidence.HasValue || SpeechExitConfidence.HasValue || SpeechStartSeconds.HasValue || SpeechStopSeconds.HasValue)
            {
                var s = new SpeechParamsJs();
                if (SpeechConfidence.HasValue) s.confidence = SpeechConfidence.Value;
                if (SpeechExitConfidence.HasValue) s.exitConfidence = SpeechExitConfidence.Value;
                if (SpeechStartSeconds.HasValue) s.startSecs = SpeechStartSeconds.Value;
                if (SpeechStopSeconds.HasValue) s.stopSecs = SpeechStopSeconds.Value;
                o.speech = s;
            }
        }
    }

    /// <summary>A call between two people: out goes the user's voice only, gated between sentences, as a track.</summary>
    public sealed class CallInputSettings : VoiceInputSettings
    {
        /// <summary>Play a 30 Hz tone at -54 dBFS while the page is hidden, so Chrome does not freeze it. Default on.</summary>
        public bool? KeepAliveWhenHidden { get; set; }

        internal CallInputOptions ToJs()
        {
            var o = new CallInputOptions();
            WriteVoice(o);
            if (KeepAliveWhenHidden.HasValue) o.keepAliveWhenHidden = KeepAliveWhenHidden.Value;
            return o;
        }
    }

    /// <summary>A voice agent (speech to text, LLM): continuous audio packets, and one utterance per sentence.</summary>
    public sealed class AgentInputSettings : VoiceInputSettings
    {
        /// <summary>The sample format of <see cref="AgentInput.Audio"/> and utterances. Default float.</summary>
        public PcmFormat? Format { get; set; }

        /// <summary>The sample rate of the packets (resampled from 16 kHz). Default 16000.</summary>
        public int? SampleRate { get; set; }

        /// <summary>Packet length in ms, a multiple of 16. Default 32.</summary>
        public int? ChunkMs { get; set; }

        /// <summary>Also produce <see cref="VoiceInput.Track"/> (off by default for an agent).</summary>
        public bool? WithTrack { get; set; }

        internal AgentInputOptions ToJs()
        {
            var o = new AgentInputOptions();
            WriteVoice(o);
            if (Format.HasValue || SampleRate.HasValue || ChunkMs.HasValue)
            {
                var output = new AgentOutputJs();
                if (Format.HasValue) output.format = Format.Value == PcmFormat.Int16 ? "s16" : "f32";
                if (SampleRate.HasValue) output.rate = SampleRate.Value;
                if (ChunkMs.HasValue) output.chunkMs = ChunkMs.Value;
                o.output = output;
            }
            if (WithTrack.HasValue) o.withTrack = WithTrack.Value;
            return o;
        }
    }

    /// <summary>A wake word listener: Spot alone, on the raw microphone.</summary>
    public sealed class WakeListenerSettings : InputSourceSettings
    {
        public WakeListenerSettings(params WakeWord[] words)
        {
            Words = words;
        }

        /// <summary>The words to hear - at least one.</summary>
        public WakeWord[] Words { get; set; }

        /// <summary>When a word counts. Default <see cref="WakeContext.Pause"/>.</summary>
        public WakeContext? Context { get; set; }

        /// <summary>Only this voice wakes the listener (the wake then comes 0.5 s later, once verified).</summary>
        public Voiceprint OnlyMyVoice { get; set; }

        internal WakeListenerOptions ToJs()
        {
            var o = new WakeListenerOptions();
            WriteSource(o);
            o.words = WordList(Words);
            if (Context.HasValue) o.context = EnumNames.Context(Context.Value);
            if (OnlyMyVoice is object) o.onlyMyVoice = OnlyMyVoice.Js;
            return o;
        }

        internal static object[] WordList(WakeWord[] words)
        {
            var list = new object[words?.Length ?? 0];
            for (var i = 0; i < list.Length; i++) list[i] = words[i].ToJs();
            return list;
        }
    }
}
