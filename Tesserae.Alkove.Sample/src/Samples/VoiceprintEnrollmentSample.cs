using System;
using Tesserae;
using Tesserae.Alkove;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Alkove.AlkoveUI;
using static Tesserae.Alkove.Sample.SamplesHelper;

namespace Tesserae.Alkove.Sample
{
    [SampleDetails(Group = "Voiceprints", Order = 10, Icon = UIcons.Fingerprint)]
    public class VoiceprintEnrollmentSample : IComponent, ISample
    {
        private const string STORAGE_KEY = "tss-alkove-sample-voiceprint";

        private readonly IComponent _content;

        public VoiceprintEnrollmentSample()
        {
            var print  = Readout("alk-print-status", "No voiceprint yet.");
            var enroll = VoiceprintEnrollment().RememberIn(STORAGE_KEY).OnEnrolled(p => print.Text = "Voiceprint: " + p.Seconds.ToString("0.0") + " s of voice, model " + p.Model);
            enroll.Render().id = "alk-print-enroll";

            var call = CallInputPanel(() => new CallInputSettings { Voiceprint = enroll.Voiceprint });

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(VoiceprintEnrollmentSample), UIcons.Fingerprint, "Learn one person's voice, then keep only that voice")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("Timbre learns a voiceprint from about six seconds of a person's voice. Given to an input, Focus then keeps that voice only - even when someone else is closer to the microphone - and the mode reads 'focus + voiceprint'."),
                        TextBlock("A voiceprint is biometric data (GDPR art. 9). This page keeps it in localStorage so it survives a reload; a real application says so, and offers to forget it.").PT(8))).SetTitle("Overview"),
                    Card(VStack().WS().Children(
                        SampleSubTitle("1. Read the text aloud"),
                        enroll,
                        print.PT(8),
                        SampleSubTitle("2. A call that keeps only that voice"),
                        call)).SetTitle("Demo"),
                    Card(CallInputSample.Code(@"var enroll = AlkoveUI.VoiceprintEnrollment().RememberIn(""my-print"");
enroll.Enrolled += print => ...;
var call = await AlkoveJs.CreateCallInputAsync(new CallInputSettings { Voiceprint = enroll.Voiceprint });
// later: Voiceprint.FromJson(stored) - null when another Timbre made it")).SetTitle("Usage")))
               .SeeAlso(typeof(VerifyVoicesSample), typeof(CallInputSample), typeof(WordEnrollmentSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
