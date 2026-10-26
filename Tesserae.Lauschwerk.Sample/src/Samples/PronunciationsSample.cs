using System;
using System.Linq;
using System.Threading.Tasks;
using Tesserae;
using Tesserae.Lauschwerk;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Lauschwerk.Sample.SamplesHelper;

namespace Tesserae.Lauschwerk.Sample
{
    [SampleDetails(Group = "Wake words", Order = 40, Icon = UIcons.Language)]
    public class PronunciationsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public PronunciationsSample()
        {
            var text     = TextBox("Hey Nova").W(220.px());
            var language = Dropdown().Items(Enum.GetValues(typeof(SpeechLanguage)).Cast<SpeechLanguage>().Select(l => DropdownItem(l.ToString()).SelectedIf(l == SpeechLanguage.English)).ToArray()).W(140.px());
            var output   = VStack().WS().Gap(4.px()).Id("lw-lex-result");

            async Task PhonemizeAsync()
            {
                var lang = (SpeechLanguage)Enum.Parse(typeof(SpeechLanguage), language.SelectedItems[0].Text);
                var lex  = await Lex.CreateAsync();
                var pronunciations = await lex.PhonemizeAsync(text.Text, lang);
                var word = await LauschwerkJs.CompileWordAsync(text.Text, lang);

                output.Clear();
                foreach (var p in pronunciations) output.Add(TextBlock(p.ToString()).Class("lw-lex-ipa"));
                output.Add(TextBlock("As a wake word: thresholds " + word.Thresholds.pause.ToString("0.00") + " after a pause, " + word.Thresholds.anywhere.ToString("0.00") + " anywhere.").Small().Secondary().PT(8));
                foreach (var w in word.Warnings) output.Add(TextBlock(w).Small().Foreground(Theme.Warning.Background));
            }

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(PronunciationsSample), UIcons.Language, "Written words to their likely pronunciations")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("Lex is what makes typed wake words work: it reads a word in a language and gives up to three likely pronunciations, in IPA. Spot then listens for any of them. Short words and digits come with warnings - a wake word wants six phonemes or more."))).SetTitle("Overview"),
                    Card(VStack().WS().Children(
                        HStack().Wrap().AlignItemsCenter().Gap(8.px()).Children(text, language, Button("Phonemize").Primary().OnClickSpinWhile(PhonemizeAsync)),
                        output.PT(8))).SetTitle("Demo"),
                    Card(CallInputSample.Code(@"var lex = await Lex.CreateAsync();
var options = await lex.PhonemizeAsync(""Camille"", SpeechLanguage.French);   // /kamij/ ...")).SetTitle("Usage")))
               .SeeAlso(typeof(WakeWordSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
