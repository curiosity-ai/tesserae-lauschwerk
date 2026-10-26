using System;
using Tesserae;
using Tesserae.Alkove;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Alkove.AlkoveUI;
using static Tesserae.Alkove.Sample.SamplesHelper;

namespace Tesserae.Alkove.Sample
{
    [SampleDetails(Group = "Wake words", Order = 30, Icon = UIcons.PhoneCall)]
    public class CallWakeWordsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public CallWakeWordsSample()
        {
            var wakes  = VStack().WS().Id("alk-call-wakes");
            var status = Readout("alk-call-wake-status", "Start the call to listen for the word.");
            WakeWordHandle handle = null;

            var panel = CallInputPanel()
               .OnStarted(async input =>
                {
                    var word = await AlkoveJs.CompileWordAsync("Hey Nova");
                    handle = ((CallInput)input).Spot(new[] { word }, WakeContext.Pause);
                    handle.Wake += e => wakes.Add(TextBlock("woke at " + e.Start.ToString("0.00") + " s, score " + e.Score.ToString("0.00")).Small().Class("alk-call-wake"));
                    status.Text = "Listening for \"Hey Nova\" behind Focus.";
                })
               .OnStopped(() =>
                {
                    handle = null;
                    status.Text = "Start the call to listen for the word.";
                });

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(CallWakeWordsSample), UIcons.PhoneCall, "Wake words on an input that is already open")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("One microphone, one pipeline: a second input on the same microphone would run every model twice (alkove warns with mic.twice). Spot(...) adds words to a call or agent input instead - heard behind Focus, so the other voices do not wake it."))).SetTitle("Overview"),
                    Card(VStack().WS().Children(panel, status.PT(8), wakes)).SetTitle("Demo"),
                    Card(CallInputSample.Code(@"var handle = call.Spot(new[] { word }, WakeContext.Pause);
handle.Wake += e => ...;
handle.Dispose();                                       // stop hearing it")).SetTitle("Usage")))
               .SeeAlso(typeof(CallInputSample), typeof(WakeWordSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
