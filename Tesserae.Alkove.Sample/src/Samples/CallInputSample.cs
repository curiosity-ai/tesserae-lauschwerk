using System;
using Tesserae;
using Tesserae.Alkove;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Alkove.AlkoveUI;
using static Tesserae.Alkove.Sample.SamplesHelper;

namespace Tesserae.Alkove.Sample
{
    [SampleDetails(Group = "Live input", Order = 10, Icon = UIcons.Microphone)]
    public class CallInputSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public CallInputSample()
        {
            var learn = Toggle();
            var near  = Toggle();
            var log   = VStack().WS().Id("alk-call-log");

            var panel = CallInputPanel(() => new CallInputSettings { Learn = learn.IsChecked, Near = near.IsChecked, Levels = true })
               .OnStarted(input =>
                {
                    input.SpeechEnded += e => log.Add(TextBlock("phrase " + e.start.ToString("0.00") + " - " + e.end.ToString("0.00") + " s").Small().Class("alk-call-phrase"));
                    input.ModeChanged += (mode, reason) => log.Add(TextBlock("mode: " + mode + (reason != RawReason.None ? " (" + reason + ")" : "")).Small().Secondary());
                });

            panel.Render().id = "alk-call-panel";

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(CallInputSample), UIcons.Microphone, "Only the user's voice goes out: a track for WebRTC")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("A call input opens the microphone and gives back a MediaStreamTrack carrying the user's voice only - other voices, music and noise removed by Focus, gated between sentences by Cue, at a steady level. Put that track on the RTCPeerConnection instead of the microphone's."),
                        TextBlock("The models load in the background: until they run, the raw microphone goes out at the same delay, and the mode says why. Nothing ever fails because of a model - only a missing or refused microphone fails the input.").PT(8))).SetTitle("Overview"),
                    Card(VStack().WS().Children(
                        HStack().Wrap().AlignItemsCenter().Gap(16.px()).Children(
                            HStack().AlignItemsCenter().Gap(6.px()).Children(learn, TextBlock("Learn my voiceprint").Small()),
                            HStack().AlignItemsCenter().Gap(6.px()).Children(near, TextBlock("Ignore far voices (near)").Small())),
                        panel.PT(8),
                        SampleSubTitle("Events"),
                        log,
                        SampleHint("Start it and talk; then let someone else talk from across the room, or play a video. The second meter is what a call would carry."))).SetTitle("Demo"),
                    Card(VStack().Children(
                        Code(@"var input = await AlkoveJs.CreateCallInputAsync(new CallInputSettings { Learn = true, FarEnd = remoteTrack });
pc.addTrack(input.Track);
input.Bind(pc.getSenders()[0]);            // swaps in the raw mic if the audio clock stalls
input.VoiceprintLearned += print => localStorage.setItem(""print"", print.ToJson());
input.SpeechStarted += e => console.log(""speaking"");"))).SetTitle("Usage")))
               .SeeAlso(typeof(AgentInputSample), typeof(VoiceprintEnrollmentSample), typeof(CallWakeWordsSample));
        }

        internal static IComponent Code(string code) => TextBlock(code).Class("alk-code").Small().Wrap().Style(s => { s.fontFamily = "monospace"; s.whiteSpace = "pre-wrap"; });

        public HTMLElement Render() => _content.Render();
    }
}
