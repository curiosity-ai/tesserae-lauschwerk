using System;
using Tesserae;
using Tesserae.Alkove;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Alkove.AlkoveUI;
using static Tesserae.Alkove.Sample.SamplesHelper;

namespace Tesserae.Alkove.Sample
{
    [SampleDetails(Group = "Files", Order = 10, Icon = UIcons.FileAudio)]
    public class ProcessFileSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ProcessFileSample()
        {
            var processor = AudioFileProcessor();
            processor.Render().id = "alk-file-processor";

            // Without a print Focus keeps the closest voice - and a voice talking alone is the closest, so
            // mix.wav's other speaker, who talks in the user's pause, passes. With the user's print (made
            // here from enroll.wav) only the user is kept: that stretch drops by some 40 dB.
            var onlyUser = Toggle().Id("alk-only-user");
            onlyUser.OnChange(async (t, e) =>
            {
                if (onlyUser.IsChecked)
                {
                    var timbre = await Timbre.CreateAsync();
                    var enroll = await AlkoveJs.DecodeAsync(await VerifyVoicesSample.FetchAsync(ENROLL_WAV));
                    processor.WithVoiceprint(await timbre.EmbedAsync(enroll));
                }
                else processor.WithVoiceprint(null);

                await processor.RunAsync();
            });

            var recordings = HStack().WS().Wrap().Gap(8.px());
            foreach (var (label, url) in RECORDINGS)
            {
                var u = url;
                recordings.Add(Button(label).SetIcon(UIcons.Waveform).OnClick(() =>
                {
                    processor.LookFor(u == WAKE_WAV || u == WAKE_TAKES_WAV || u == WAKE_OTHER_WAV ? "Hey Nova" : "");
                    processor.LoadUrlAsync(u).FireAndForget();
                }));
            }

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ProcessFileSample), UIcons.FileAudio, "Run the models over a recording, faster than real time")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("AlkoveJs.ProcessAsync runs Focus, Cue and Spot over a whole recording in alkove's background worker and gives the same output the live inputs give for the same audio: the cleaned audio, where speech is, and where a word was heard. Anything the browser decodes works; the file never leaves the page."))).SetTitle("Overview"),
                    Card(VStack().WS().Children(
                        TextBlock("Try one of the gallery's recordings, or pick a file of your own:").Small().Secondary(),
                        recordings.PB(8),
                        HStack().AlignItemsCenter().Gap(8.px()).PB(8).Children(onlyUser, TextBlock("Keep only the user's voice (a voiceprint made from \"The user reading\")").Small()),
                        processor,
                        SampleHint("Before is the recording as it was; after is what Focus kept. The shading is where Cue heard speech, a line where Spot heard the word. Click a waveform to play it. In \"A voice over another voice\", the other speaker talks in the user's pause: without a print Focus keeps them (they are the closest voice while they talk alone), with the user's print it drops them."))).SetTitle("Demo"),
                    Card(CallInputSample.Code(@"var r = await AlkoveJs.ProcessAsync(file, new ProcessSettings { Focus = true, Cue = true, Words = new[] { word } });
r.Audio;   // AudioClip, 16 kHz, processed
r.Speech;  // SpeechSpan[]
r.Wakes;   // WakeEvent[]
r.ToWav(); // Blob")).SetTitle("Usage")))
               .SeeAlso(typeof(CallInputSample), typeof(VerifyVoicesSample), typeof(WakeWordSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
