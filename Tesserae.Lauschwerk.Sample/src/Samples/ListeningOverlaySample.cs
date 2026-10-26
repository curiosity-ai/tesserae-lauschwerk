using System;
using System.Linq;
using System.Threading.Tasks;
using Tesserae;
using Tesserae.Lauschwerk;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Lauschwerk.LauschwerkUI;
using static Tesserae.Lauschwerk.Sample.SamplesHelper;

namespace Tesserae.Lauschwerk.Sample
{
    [SampleDetails(Group = "Live input", Order = 25, Icon = UIcons.EarSound)]
    public class ListeningOverlaySample : IComponent, ISample
    {
        private const string PHRASE = "Show me open supplier notices for the A320 line";

        private readonly IComponent _content;

        private AgentInput       _input;
        private ListeningOverlay _overlay;
        private double           _timer;

        public ListeningOverlaySample()
        {
            var status     = Readout("lw-overlay-status", "closed");
            var transcript = Toggle("Show the words as they are heard").Checked(true).Id("lw-overlay-transcript");
            var listen     = Button("Listen").SetIcon(UIcons.Microphone).Id("lw-overlay-listen");

            listen.OnClick(() => StartAsync(transcript.IsChecked, status).FireAndForget());

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ListeningOverlaySample), UIcons.EarSound, "A panel over the page that shows the sound being heard, and the words")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("The listening overlay is what an app shows the moment it starts hearing the user - after the wake word, or when the microphone button was pressed - and until it has what it came for. The sound is drawn as bars, mirrored around the middle with the lowest frequencies at the centre; they rise quickly, fall slowly and the loudest one takes the primary colour. It can carry a line of text under the bars: speech-to-text is not part of this package, so the words come from wherever the host gets them (the demo types a phrase)."))).SetTitle("Overview"),
                    Card(VStack().WS().Children(
                        HStack().Gap(12.px()).AlignItemsCenter().Children(listen, transcript),
                        status.PT(8),
                        SampleHint("The overlay opens at the bottom of the page and listens to your microphone until you cancel it."))).SetTitle("Demo"),
                    Card(CallInputSample.Code(@"var overlay = ListeningOverlay().ShowTranscript().ShowCancel();
overlay.Cancelled += () => overlay.Close();

var input = await LauschwerkJs.CreateAgentInputAsync(new AgentInputSettings { Near = true });
overlay.Bind(input).SetStatus(""Listening"").Open();

speechToText.Partial += text => overlay.SetTranscript(text);                  // the words so far
speechToText.Final   += text => { overlay.SetTranscript(text, true); overlay.SetStatus(""Heard"").Close(1200); };")).SetTitle("Usage")))
               .SeeAlso(typeof(AgentInputSample), typeof(CallInputSample));
        }

        private async Task StartAsync(bool showTranscript, TextBlock status)
        {
            Stop();

            _overlay = ListeningOverlay().ShowTranscript(showTranscript).ShowCancel();
            _overlay.Cancelled += () =>
            {
                Stop();
                status.Text = "closed";
            };

            try
            {
                _input = await LauschwerkJs.CreateAgentInputAsync(new AgentInputSettings { Near = true });
            }
            catch (LauschwerkException e)
            {
                status.Text = e.Message;
                return;
            }

            _overlay.Bind(_input).SetStatus("Listening").Open();
            status.Text = "open";

            if (!showTranscript) return;

            // A stand-in for a speech-to-text service: the phrase, a word at a time
            var words = PHRASE.Split(' ');
            var heard = 0;

            Action step = null;
            step = () =>
            {
                heard++;
                _overlay?.SetTranscript(string.Join(" ", words.Take(heard)), heard == words.Length);

                if (heard < words.Length) _timer = window.setTimeout(_ => step(), 420);
                else _overlay?.SetStatus("Heard");
            };

            _timer = window.setTimeout(_ => step(), 900);
        }

        private void Stop()
        {
            window.clearTimeout(_timer);
            _overlay?.Close();
            _input?.Dispose();
            _overlay = null;
            _input   = null;
        }

        public HTMLElement Render() => _content.Render();
    }
}
