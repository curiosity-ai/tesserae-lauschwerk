using System;
using Tesserae;
using Tesserae.Alkove;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Alkove.AlkoveUI;
using static Tesserae.Alkove.Sample.SamplesHelper;

namespace Tesserae.Alkove.Sample
{
    [SampleDetails(Group = "Wake words", Order = 20, Icon = UIcons.ListCheck)]
    public class WordEnrollmentSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public WordEnrollmentSample()
        {
            var listen = WakeWordPanel("Hey Nova");
            var enroll = WordEnrollmentPanel("Hey Nova").OnEnrolled(result =>
            {
                listen.UseWord(result.Word);
                if (result.Voiceprint is object) listen.OnlyMyVoice(result.Voiceprint);
            });

            enroll.Render().id = "alk-enroll-word-panel";
            listen.Render().id = "alk-enrolled-wake-panel";

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(WordEnrollmentSample), UIcons.ListCheck, "Set a word's thresholds on the user's own voice")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("A typed word works for anyone. Enrolling it - the user says it three times - sets its thresholds on their voice: fewer misses for them, and the takes also give a voiceprint, so only they wake it."),
                        TextBlock("Each segment of speech is a take. One that does not sound like the word counts as other speech and raises the floor instead.").PT(8))).SetTitle("Overview"),
                    Card(VStack().WS().Children(
                        SampleSubTitle("1. Enroll"),
                        enroll,
                        SampleSubTitle("2. Listen for the enrolled word, your voice only"),
                        listen)).SetTitle("Demo"),
                    Card(CallInputSample.Code(@"var enrollment = await AlkoveJs.EnrollWordAsync(""Hey Nova"", SpeechLanguage.English, takes: 3);
enrollment.Take += t => console.log(t.Ok ? ""counted"" : ""not the word"");
var result = await enrollment.DoneAsync();
var wake = await AlkoveJs.CreateWakeListenerAsync(new WakeListenerSettings(result.Word) { OnlyMyVoice = result.Voiceprint });")).SetTitle("Usage")))
               .SeeAlso(typeof(WakeWordSample), typeof(VoiceprintEnrollmentSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
