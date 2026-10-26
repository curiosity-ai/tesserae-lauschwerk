using System;
using Tesserae;
using Tesserae.Lauschwerk;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Lauschwerk.LauschwerkUI;
using static Tesserae.Lauschwerk.Sample.SamplesHelper;

namespace Tesserae.Lauschwerk.Sample
{
    [SampleDetails(Group = "Live input", Order = 20, Icon = UIcons.Robot)]
    public class AgentInputSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public AgentInputSample()
        {
            var packets    = Readout("lw-agent-packets", "0 packets");
            var utterances = VStack().WS().Gap(8.px()).Id("lw-agent-utterances");
            var count      = 0;

            var panel = AgentInputPanel(() => new AgentInputSettings { Near = true })
               .OnStarted(input =>
                {
                    var agent = (AgentInput)input;
                    var n = 0;
                    agent.Audio += _ => { if (++n % 16 == 0) packets.Text = n + " packets"; };
                    agent.Utterance += u =>
                    {
                        count++;
                        utterances.Prepend(VStack().WS().Class("lw-utterance").Children(
                            TextBlock("Utterance " + count + ": " + u.Start.ToString("0.00") + " - " + u.End.ToString("0.00") + " s").Small().SemiBold(),
                            Waveform(u.ToClip()).SetHeight(56.px()).WS()));
                    };
                });

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(AgentInputSample), UIcons.Robot, "Continuous audio for speech-to-text, and one utterance per sentence")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("An agent input feeds a voice agent: Audio is continuous - 32 ms packets, never gated, for a streaming speech-to-text service - and Utterance hands over each sentence the user said, with 300 ms of pre-roll, for a batch one. SpeechStarted and SpeechEnded say where the user speaks; FarEndActive tells it when the agent itself talks."))).SetTitle("Overview"),
                    Card(VStack().WS().Children(
                        panel,
                        packets.PT(8),
                        SampleSubTitle("Utterances"),
                        utterances,
                        SampleHint("Say two sentences with a pause between them: each becomes an utterance you can play back."))).SetTitle("Demo"),
                    Card(CallInputSample.Code(@"var input = await LauschwerkJs.CreateAgentInputAsync(new AgentInputSettings { FarEndManual = true, Format = PcmFormat.Int16, SampleRate = 24000 });
input.Audio += pcm => socket.send(pcm);                  // continuous
input.Utterance += u => TranscribeAsync(u.Pcm);         // one per sentence
input.FarEndActive(true);                               // the agent starts speaking")).SetTitle("Usage")))
               .SeeAlso(typeof(CallInputSample), typeof(ProcessFileSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
