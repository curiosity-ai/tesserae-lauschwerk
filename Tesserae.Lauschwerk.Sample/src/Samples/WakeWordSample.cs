using System;
using Tesserae;
using Tesserae.Lauschwerk;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Lauschwerk.LauschwerkUI;
using static Tesserae.Lauschwerk.Sample.SamplesHelper;

namespace Tesserae.Lauschwerk.Sample
{
    [SampleDetails(Group = "Wake words", Order = 10, Icon = UIcons.Ear)]
    public class WakeWordSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public WakeWordSample()
        {
            var panel = WakeWordPanel("Hey Nova");
            panel.Render().id = "lw-wake-panel";

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(WakeWordSample), UIcons.Ear, "Any typed word is a wake word: no training, no recording")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("Type a word in one of seven languages (English, French, Spanish, Italian, Portuguese, Catalan, Romanian): Lex turns it into its likely pronunciations and Spot listens for them on the raw microphone - cheap enough to leave on."),
                        TextBlock("By default a word only counts after a pause, which keeps false alarms far down: \"so hey nova was great\" does not wake it. WakeContext.Anywhere hears it mid-sentence, at a stricter threshold.").PT(8))).SetTitle("Overview"),
                    Card(VStack().WS().Children(
                        panel,
                        SampleHint("Start listening, pause, and say the word. Each wake carries the word's audio - play it back from the list."))).SetTitle("Demo"),
                    Card(CallInputSample.Code(@"var word = await LauschwerkJs.CompileWordAsync(""Hey Nova"", SpeechLanguage.English);
var wake = await LauschwerkJs.CreateWakeListenerAsync(new WakeListenerSettings(word));
wake.Wake += e => StartListening(e.Audio);
localStorage.setItem(""word"", word.ToJson());           // compile once, store it")).SetTitle("Usage")))
               .SeeAlso(typeof(WordEnrollmentSample), typeof(CallWakeWordsSample), typeof(PronunciationsSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
