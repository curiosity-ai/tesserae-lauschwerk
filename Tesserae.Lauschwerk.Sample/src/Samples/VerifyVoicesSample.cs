using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tesserae;
using Tesserae.Lauschwerk;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Lauschwerk.LauschwerkUI;
using static Tesserae.Lauschwerk.Sample.SamplesHelper;

namespace Tesserae.Lauschwerk.Sample
{
    [SampleDetails(Group = "Voiceprints", Order = 20, Icon = UIcons.UserCheck)]
    public class VerifyVoicesSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public VerifyVoicesSample()
        {
            var result = VStack().WS().Gap(4.px()).Id("lw-verify-result");
            var level  = Dropdown().Items(DropdownItem("Lenient"), DropdownItem("Balanced").Selected(), DropdownItem("Strict")).W(140.px());

            async Task CompareAsync()
            {
                result.Clear();
                result.Add(TextBlock("Making the prints...").Small().Secondary());

                var timbre = await Timbre.CreateAsync();
                var enroll = await LauschwerkJs.DecodeAsync(await FetchAsync(ENROLL_WAV));
                var a      = await LauschwerkJs.DecodeAsync(await FetchAsync(VOICE_A_WAV));
                var b      = await LauschwerkJs.DecodeAsync(await FetchAsync(VOICE_B_WAV));

                var print = await timbre.EmbedAsync(enroll);
                var printA = await timbre.EmbedAsync(a);
                var printB = await timbre.EmbedAsync(b);
                var chosen = (VerifyLevel)Enum.Parse(typeof(VerifyLevel), level.SelectedItems[0].Text);

                var sameA = timbre.Verify(print, printA, chosen);
                var sameB = timbre.Verify(print, printB, chosen);
                var who   = await timbre.IdentifyAsync(printB, new Dictionary<string, Voiceprint> { ["the user"] = print, ["someone else"] = printA }, chosen);

                result.Clear();
                result.Children(
                    TextBlock("Enrolled from enroll.wav: " + print.Seconds.ToString("0.0") + " s of voice.").Small(),
                    TextBlock("voice-a.wav (the user): " + (sameA.Same ? "same voice" : "another voice") + ", score " + sameA.Score.ToString("0.00")).Small().Id("lw-verify-a"),
                    TextBlock("voice-b.wav (someone else): " + (sameB.Same ? "same voice" : "another voice") + ", score " + sameB.Score.ToString("0.00")).Small().Id("lw-verify-b"),
                    TextBlock("Identify voice-b among the user and voice-a: " + (who is null ? "nobody known" : who.Who + " (" + who.Score.ToString("0.00") + ")")).Small().Id("lw-identify-b"));
            }

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(VerifyVoicesSample), UIcons.UserCheck, "Compare voiceprints: is this the same person?")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("Timbre turns a recording into a voiceprint and compares two: Verify answers same or not at a level (lenient, balanced, strict), Identify picks the known print a voice belongs to, or nobody. Short prints are judged more strictly."))).SetTitle("Overview"),
                    Card(VStack().WS().Children(
                        HStack().AlignItemsCenter().Gap(8.px()).Children(level, Button("Compare the recordings").Primary().SetIcon(UIcons.UserCheck).OnClickSpinWhile(CompareAsync)),
                        result.PT(8),
                        SampleHint("The recordings are two synthetic speakers (tools/Lauschwerk.TestAudio): the user reads, says another sentence, and someone else speaks."))).SetTitle("Demo"),
                    Card(CallInputSample.Code(@"var timbre = await Timbre.CreateAsync();
var print = await timbre.EmbedAsync(clip);              // 16 kHz, 1.5 s or more
var r = timbre.Verify(print, other, VerifyLevel.Balanced);   // r.Same, r.Score")).SetTitle("Usage")))
               .SeeAlso(typeof(VoiceprintEnrollmentSample), typeof(ProcessFileSample));
        }

        internal static async Task<Transpose.Core.es5.ArrayBuffer> FetchAsync(string url)
        {
            var response = await window.fetch(url).ToTask();
            return await response.arrayBuffer().ToTask();
        }

        public HTMLElement Render() => _content.Render();
    }
}
