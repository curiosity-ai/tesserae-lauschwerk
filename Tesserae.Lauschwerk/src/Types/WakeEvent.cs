namespace Tesserae.Lauschwerk
{
    /// <summary>A wake word was heard.</summary>
    public sealed class WakeEvent
    {
        internal WakeEvent(IWakeEventJs js)
        {
            Word      = js.word;
            Score     = js.score;
            Threshold = js.threshold;
            Context   = js.context == "anywhere" ? WakeContext.Anywhere : WakeContext.Pause;
            Start     = js.start;
            End       = js.end;
            Audio     = js.audio is object ? new AudioClip(js.audio) : null;
            Voice     = js.voice;
        }

        /// <summary>The word's text.</summary>
        public string Word { get; }

        /// <summary>A log-probability: near 0 is clear, the threshold is around -1 to -2.</summary>
        public double Score { get; }

        /// <summary>The threshold it beat.</summary>
        public double Threshold { get; }

        /// <summary>Which threshold matched: after a pause, or anywhere.</summary>
        public WakeContext Context { get; }

        /// <summary>Seconds on the input's clock (or from the start of a file).</summary>
        public double Start { get; }

        public double End { get; }

        /// <summary>The word's raw audio, 16 kHz - on a wake listener only.</summary>
        public AudioClip Audio { get; }

        /// <summary>The likeness to the print, with only-my-voice. Null otherwise.</summary>
        public double? Voice { get; }
    }

    /// <summary>A span of speech, in seconds.</summary>
    public sealed class SpeechSpan
    {
        public SpeechSpan(double start, double end)
        {
            Start = start;
            End   = end;
        }

        public double Start { get; }
        public double End { get; }
        public double Duration => End - Start;
    }
}
