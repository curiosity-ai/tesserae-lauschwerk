using System;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// Factories for the package's components, in Tesserae's style (<c>using static Tesserae.Lauschwerk.LauschwerkUI;</c>).
    /// </summary>
    public static class LauschwerkUI
    {
        /// <summary>A start/stop control for a call input, with the mode, speech and two level meters.</summary>
        public static VoiceInputPanel CallInputPanel(Func<CallInputSettings> settings = null) => VoiceInputPanel.ForCall(settings);

        /// <summary>A start/stop control for an agent input.</summary>
        public static VoiceInputPanel AgentInputPanel(Func<AgentInputSettings> settings = null) => VoiceInputPanel.ForAgent(settings);

        /// <summary>A wake word listener for any typed word.</summary>
        public static WakeWordPanel WakeWordPanel(string word = "Hey Nova", SpeechLanguage language = SpeechLanguage.English) => new WakeWordPanel(word, language);

        /// <summary>Enrolls a wake word on the user's voice.</summary>
        public static WordEnrollmentPanel WordEnrollmentPanel(string word = "Hey Nova", SpeechLanguage language = SpeechLanguage.English, int takes = 3) => new WordEnrollmentPanel(word, language, takes);

        /// <summary>Learns a voiceprint while the user reads a text aloud.</summary>
        public static VoiceprintEnrollment VoiceprintEnrollment(string text = Lauschwerk.VoiceprintEnrollment.DEFAULT_TEXT) => new VoiceprintEnrollment(text);

        /// <summary>Runs the models over a recording and shows the before and after.</summary>
        public static AudioFileProcessor AudioFileProcessor() => new AudioFileProcessor();

        /// <summary>Downloads a usage's models ahead of time.</summary>
        public static ModelPreloader ModelPreloader(LauschwerkUsage usage, bool voiceprint = false, bool typedWords = false) => new ModelPreloader(usage, voiceprint, typedWords);

        /// <summary>A live level meter for a track, or for samples fed to it.</summary>
        public static LevelMeter LevelMeter(string label = null) => new LevelMeter(label);

        /// <summary>A recording drawn with its speech and wakes, playable.</summary>
        public static Waveform Waveform(AudioClip clip = null) => new Waveform(clip);

        /// <summary>A scrolling waveform of live audio: an agent input's packets, a track, or samples you feed it.</summary>
        public static LiveWaveform LiveWaveform() => new LiveWaveform();

        /// <summary>
        /// The listening overlay: a panel over the interface with the sound as bars, and optionally the words as they are heard.
        /// <see cref="ListeningOverlay.Open"/> shows it.
        /// </summary>
        public static ListeningOverlay ListeningOverlay() => new ListeningOverlay();

        /// <summary>A dot that lights while someone speaks.</summary>
        public static SpeechIndicator SpeechIndicator() => new SpeechIndicator();

        /// <summary>An input's mode as a badge.</summary>
        public static ModeBadge ModeBadge() => new ModeBadge();
    }
}
