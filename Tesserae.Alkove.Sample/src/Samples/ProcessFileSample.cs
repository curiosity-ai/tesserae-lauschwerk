using System;
using System.Linq;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Alkove.Sample.SamplesHelper;

namespace Tesserae.Alkove.Sample
{
    [SampleDetails(Group = "Files", Order = 10, Icon = UIcons.FileAudio)]
    public class ProcessFileSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ProcessFileSample()
        {
            var result = Readout("alk-process-result");
            var run = Button("Process speech.wav").Primary().OnClick(async () =>
            {
                result.Text = "processing...";
                try
                {
                    var r = await AlkoveJs.ProcessUrlAsync(SPEECH_WAV, new ProcessSettings { Focus = true, Cue = true });
                    result.Text = "speech: " + string.Join(", ", r.Speech.Select(s => s.Start.ToString("0.00") + "-" + s.End.ToString("0.00"))) + " | duration " + r.Audio.Duration.ToString("0.00");
                }
                catch (Exception e)
                {
                    result.Text = "error: " + e.Message;
                }
            });

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ProcessFileSample), UIcons.FileAudio, "Run the models over a recording")
               .FlatSection(Card(VStack().Children(run, result)).SetTitle("Demo"));
        }

        public HTMLElement Render() => _content.Render();
    }
}
