using System;
using System.Linq;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Lauschwerk.Sample
{
    /// <summary>
    /// The bits every sample page shares: its title row, its "See also" footer, the small text helpers
    /// that keep the pages looking alike, and the recordings the gallery plays to the models.
    /// </summary>
    public static class SamplesHelper
    {
        private const string REPOSITORY = "https://github.com/theolivenbaum/lauschwerk";

        /// <summary>Where the recordings are served from, relative to the page (the sample's _CopySampleAudio target).</summary>
        public const string AUDIO = "assets/audio/";

        /// <summary>A pause, "Hey Nova.", then a question - one wake at about 1.7 s.</summary>
        public const string WAKE_WAV = AUDIO + "wake.wav";

        /// <summary>Two sentences a second apart - two phrases for Cue.</summary>
        public const string SPEECH_WAV = AUDIO + "speech.wav";

        /// <summary>The user close by, someone else 6 dB further away talking in the user's pause, and a noise floor.</summary>
        public const string MIX_WAV = AUDIO + "mix.wav";

        /// <summary>Ten seconds of the user reading - enough for a voiceprint.</summary>
        public const string ENROLL_WAV = AUDIO + "enroll.wav";

        /// <summary>Another sentence by the user.</summary>
        public const string VOICE_A_WAV = AUDIO + "voice-a.wav";

        /// <summary>A sentence by someone else.</summary>
        public const string VOICE_B_WAV = AUDIO + "voice-b.wav";

        /// <summary>"Hey Nova." three times, with pauses - what word enrollment listens for.</summary>
        public const string WAKE_TAKES_WAV = AUDIO + "wake-takes.wav";

        /// <summary>The wake word said by the other voice.</summary>
        public const string WAKE_OTHER_WAV = AUDIO + "wake-other.wav";

        /// <summary>Nothing but a noise floor.</summary>
        public const string NOISE_WAV = AUDIO + "noise.wav";

        /// <summary>Every recording, with a label, for the pickers.</summary>
        public static readonly (string Label, string Url)[] RECORDINGS =
        {
            ("Speech: two sentences", SPEECH_WAV),
            ("Wake word, then a question", WAKE_WAV),
            ("A voice over another voice and noise", MIX_WAV),
            ("The user reading (10 s)", ENROLL_WAV),
            ("Wake word, three times", WAKE_TAKES_WAV),
            ("Wake word, other voice", WAKE_OTHER_WAV),
            ("The user, another sentence", VOICE_A_WAV),
            ("Someone else", VOICE_B_WAV),
            ("Noise only", NOISE_WAV),
        };

        public static SectionStack SampleTitle(this SectionStack stack, Type sampleType, UIcons icon, string subtitle)
        {
            return stack.Title(icon, SamplePageName(sampleType), subtitle,
                Button("SDK docs").SetIcon(UIcons.Books).Tooltip("The web SDK's own documentation").OnClick(() => window.open($"{REPOSITORY}/blob/main/sdk/packages/lauschwerk/README.md", "_blank")),
                Button("View Code").SetIcon(UIcons.SquareTerminal).Tooltip("This page's source on GitHub").OnClick(() => window.open($"{REPOSITORY}/blob/main/Tesserae.Lauschwerk.Sample/src/Samples/{sampleType.Name}.cs", "_blank")));
        }

        /// <summary>Closes a page with one button per related sample, each navigating to that page.</summary>
        public static SectionStack SeeAlso(this SectionStack stack, params Type[] relatedSamples)
        {
            var links = HStack().WS().Wrap().Gap(8.px()).PT(8);

            foreach (var sampleType in relatedSamples)
            {
                links.Add(Button(SamplePageName(sampleType)).SetIcon(IconFor(sampleType)).OnClick(() => Router.Navigate(RouteFor(sampleType))));
            }

            return stack.FlatSection(VStack().WS().Children(
                Card(VStack().WS().Children(
                    TextBlock("Pages that usually come up together with this one."),
                    links)).SetTitle("See also")));
        }

        public static IComponent SampleSubTitle(string text) => TextBlock(text).SemiBold().PT(16).PB(8);

        /// <summary>A muted line under a demo, for the "now try this" instructions each page ends on.</summary>
        public static IComponent SampleHint(string text) => TextBlock(text).Small().Secondary().PT(4);

        /// <summary>A labelled read-out with a stable id, which is what the Playwright checks read.</summary>
        public static TextBlock Readout(string id, string initial = "-") => TextBlock(initial).Id(id).Class("lw-readout");

        private static string SamplePageName(Type sampleType) => SamplePage.FormatName(sampleType);

        private static string RouteFor(Type sampleType) => $"#/view/{SamplePage.FormatName(sampleType)}";

        private static UIcons IconFor(Type sampleType)
        {
            var details = sampleType.GetCustomAttributes(typeof(SampleDetailsAttribute), true).FirstOrDefault() as SampleDetailsAttribute;

            return details is object ? details.Icon : UIcons.Circle;
        }
    }
}
