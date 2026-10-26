namespace Tesserae.Alkove
{
    /// <summary>What an input sends out right now.</summary>
    public enum VoiceMode
    {
        /// <summary>The raw microphone, at the same delay - see <see cref="VoiceInput.RawReason"/> for why.</summary>
        Raw,

        /// <summary>Focus keeps the closest voice and drops noise, music and other voices.</summary>
        Focus,

        /// <summary>Focus keeps the voice of a print: only that person.</summary>
        FocusPrint,

        /// <summary>A wake listener running Spot on the raw microphone.</summary>
        Spot,

        /// <summary>A wake listener that only wakes for the voice of a print.</summary>
        SpotPrint
    }

    /// <summary>Why an input is <see cref="VoiceMode.Raw"/>.</summary>
    public enum RawReason
    {
        /// <summary>It is not raw.</summary>
        None,

        /// <summary><see cref="VoiceInput.SetEnabledAsync"/> turned the models off.</summary>
        Off,

        /// <summary>The models are downloading or compiling; the mode changes by itself when they run.</summary>
        Loading,

        /// <summary>The models failed to load or the engine crashed; the SDK retries (5 s, doubling to 60 s).</summary>
        Failed,

        /// <summary>The device is too slow for the models; the SDK tries again later (10 s, doubling to 160 s).</summary>
        Overload,

        /// <summary>No WebAssembly SIMD in this browser: raw for the whole session.</summary>
        Unsupported
    }

    /// <summary>The input's audio graph.</summary>
    public enum InputStatus
    {
        Starting,
        Running,

        /// <summary>The audio context waits for a user gesture: call <see cref="VoiceInput.ResumeAsync"/> inside a click.</summary>
        Blocked,

        /// <summary>The system took the audio (a phone call, Siri); it comes back by itself.</summary>
        Interrupted,

        Disposed
    }

    /// <summary>When a wake word counts.</summary>
    public enum WakeContext
    {
        /// <summary>Only after a pause (300 ms without speech): far fewer false alarms. The wake listener's default.</summary>
        Pause,

        /// <summary>Anywhere, mid-sentence included, at a stricter threshold.</summary>
        Anywhere,

        /// <summary>The pause threshold after a pause, the stricter one elsewhere.</summary>
        Both
    }

    /// <summary>How sure <see cref="Timbre.Verify"/> has to be that two prints are the same voice.</summary>
    public enum VerifyLevel
    {
        Lenient,
        Balanced,
        Strict
    }

    /// <summary>The seven languages Spot hears typed words in.</summary>
    public enum SpeechLanguage
    {
        English,
        French,
        Spanish,
        Italian,
        Portuguese,
        Catalan,
        Romanian
    }

    /// <summary>The sample format of an agent input's continuous audio.</summary>
    public enum PcmFormat
    {
        /// <summary>32-bit float, -1 to 1.</summary>
        Float32,

        /// <summary>16-bit signed, little-endian.</summary>
        Int16
    }

    internal static class EnumNames
    {
        internal static VoiceMode Mode(string mode)
        {
            switch (mode)
            {
                case "focus":      return VoiceMode.Focus;
                case "focus+print": return VoiceMode.FocusPrint;
                case "spot":       return VoiceMode.Spot;
                case "spot+print": return VoiceMode.SpotPrint;
                default:           return VoiceMode.Raw;
            }
        }

        internal static RawReason Reason(string reason)
        {
            switch (reason)
            {
                case "off":         return RawReason.Off;
                case "loading":     return RawReason.Loading;
                case "failed":      return RawReason.Failed;
                case "overload":    return RawReason.Overload;
                case "unsupported": return RawReason.Unsupported;
                default:            return RawReason.None;
            }
        }

        internal static InputStatus Status(string status)
        {
            switch (status)
            {
                case "running":     return InputStatus.Running;
                case "blocked":     return InputStatus.Blocked;
                case "interrupted": return InputStatus.Interrupted;
                case "disposed":    return InputStatus.Disposed;
                default:            return InputStatus.Starting;
            }
        }

        internal static string Context(WakeContext context) => context == WakeContext.Anywhere ? "anywhere" : context == WakeContext.Both ? "both" : "pause";

        internal static string Level(VerifyLevel level) => level == VerifyLevel.Lenient ? "lenient" : level == VerifyLevel.Strict ? "strict" : "balanced";

        internal static string Code(SpeechLanguage language)
        {
            switch (language)
            {
                case SpeechLanguage.French:     return "fr";
                case SpeechLanguage.Spanish:    return "es";
                case SpeechLanguage.Italian:    return "it";
                case SpeechLanguage.Portuguese: return "pt";
                case SpeechLanguage.Catalan:    return "ca";
                case SpeechLanguage.Romanian:   return "ro";
                default:                        return "en";
            }
        }

        internal static string[] Codes(SpeechLanguage[] languages)
        {
            if (languages is null || languages.Length == 0) return new[] { "en" };
            var codes = new string[languages.Length];
            for (var i = 0; i < languages.Length; i++) codes[i] = Code(languages[i]);
            return codes;
        }

        /// <summary>A display name for a mode, as the gallery and the components print it.</summary>
        internal static string Describe(VoiceMode mode)
        {
            switch (mode)
            {
                case VoiceMode.Focus:      return "focus";
                case VoiceMode.FocusPrint: return "focus + voiceprint";
                case VoiceMode.Spot:       return "spot";
                case VoiceMode.SpotPrint:  return "spot + voiceprint";
                default:                   return "raw";
            }
        }
    }
}
