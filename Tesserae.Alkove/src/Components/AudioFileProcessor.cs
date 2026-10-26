using System;
using System.Linq;
using System.Threading.Tasks;
using Transpose;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Alkove
{
    /// <summary>
    /// Runs the models over a recording and shows what they did: the recording as it was and as Focus
    /// left it, each with where Cue heard speech and where Spot heard a word, playable and downloadable.
    /// Takes a file the user picks or drops, or a URL (<see cref="LoadUrlAsync"/>). Everything runs in
    /// alkove's background worker, faster than real time; nothing leaves the browser.
    /// </summary>
    public sealed class AudioFileProcessor : IComponent
    {
        private readonly FileSelector      _file;
        private readonly Toggle            _focus = Toggle().Checked();
        private readonly Toggle            _cue   = Toggle().Checked();
        private readonly TextBox           _words = TextBox("").SetPlaceholder("Wake word to look for (optional)").W(240.px());
        private readonly ProgressIndicator _progress = ProgressIndicator();
        private readonly TextBlock         _status  = TextBlock("").Small().Secondary().Class("tss-alkove-file-status");
        private readonly Waveform          _before  = new Waveform();
        private readonly Waveform          _after   = new Waveform();
        private readonly Button            _download;
        private readonly IComponent        _content;

        private object         _source;
        private string         _sourceName;
        private ProcessedAudio _result;
        private Voiceprint     _voiceprint;

        public AudioFileProcessor()
        {
            _file = FileSelector().SetPlaceholder("Pick a recording").SetAccepts("audio/*,.wav,.mp3,.ogg,.webm,.m4a,.flac")
               .OnFileSelected((fs, e) =>
                {
                    _source     = fs.SelectedFile;
                    _sourceName = fs.SelectedFile.name;
                    RunAsync().FireAndForget();
                });

            _download = Button("Download WAV").SetIcon(UIcons.Download).OnClick(Download);
            _download.Disabled();

            _content = VStack().WS().Gap(8.px()).Class("tss-alkove-file").Children(
                HStack().WS().Wrap().AlignItemsCenter().Gap(16.px()).Children(
                    _file.W(320.px()),
                    Label("Focus").Inline().AutoWidth().SetContent(_focus),
                    Label("Cue").Inline().AutoWidth().SetContent(_cue),
                    _words,
                    Button("Run again").SetIcon(UIcons.Refresh).OnClick(() => RunAsync().FireAndForget())),
                _progress.WS(),
                _status,
                TextBlock("Before").SemiBold().Small(),
                _before.WS(),
                TextBlock("After").SemiBold().Small(),
                _after.WS(),
                HStack().Gap(8.px()).Children(
                    Button("Play before").SetIcon(UIcons.Play).OnClick(() => { _after.Stop(); _before.Play(); }),
                    Button("Play after").SetIcon(UIcons.Play).OnClick(() => { _before.Stop(); _after.Play(); }),
                    _download));

            _status.Text = "Pick a recording - nothing is uploaded.";
        }

        /// <summary>The last result, or null.</summary>
        public ProcessedAudio Result => _result;

        public event Action<ProcessedAudio> Processed;

        public AudioFileProcessor OnProcessed(Action<ProcessedAudio> handler)
        {
            Processed += handler;
            return this;
        }

        /// <summary>Keep only this voice (Focus with a print).</summary>
        public AudioFileProcessor WithVoiceprint(Voiceprint print)
        {
            _voiceprint = print;
            return this;
        }

        /// <summary>Sets the wake word looked for (typed, English).</summary>
        public AudioFileProcessor LookFor(string word)
        {
            _words.Text = word ?? "";
            return this;
        }

        /// <summary>Fetches a recording and processes it.</summary>
        public async Task LoadUrlAsync(string url)
        {
            _status.Text = "Downloading " + url + "...";
            var response = await window.fetch(url).ToTask();
            _source      = await response.arrayBuffer().ToTask();
            _sourceName  = url.Split('/').Last();
            await RunAsync();
        }

        public async Task RunAsync()
        {
            if (_source is null) return;

            _download.Disabled();
            _progress.Progress(0);
            _status.Text = "Processing " + _sourceName + "...";
            _content.Render().setAttribute("data-state", "running");

            try
            {
                var settings = new ProcessSettings { Focus = _focus.IsChecked, Cue = _cue.IsChecked, Voiceprint = _voiceprint, Context = WakeContext.Pause };

                if (!string.IsNullOrWhiteSpace(_words.Text)) settings.Words = new[] { await AlkoveJs.CompileWordAsync(_words.Text.Trim()) };

                // Each run hands the worker a copy: an ArrayBuffer is transferred, and a second run would
                // otherwise find it detached.
                var source = _source is Transpose.Core.es5.ArrayBuffer buffer ? buffer.slice(0) : _source;

                _result = await AlkoveJs.ProcessAsync(source, settings, p => _progress.Progress((float)(p * 100)));

                _before.SetClip(_result.Raw).SetSpeech(_result.Speech).SetWakes(_result.Wakes);
                _after.SetClip(_result.Audio).SetSpeech(_result.Speech).SetWakes(_result.Wakes);

                _progress.Progress(100);
                _status.Text = _sourceName + ": " + _result.Audio.Duration.ToString("0.0") + " s"
                             + (settings.Cue || settings.Focus ? ", " + _result.Speech.Length + (_result.Speech.Length == 1 ? " phrase" : " phrases") : "")
                             + (settings.Words is object ? ", " + _result.Wakes.Length + (_result.Wakes.Length == 1 ? " wake" : " wakes") : "")
                             + ".";

                var root = _content.Render();
                root.setAttribute("data-state", "done");
                root.setAttribute("data-phrases", _result.Speech.Length.ToString());
                root.setAttribute("data-wakes", _result.Wakes.Length.ToString());

                _download.Disabled(false);
                Processed?.Invoke(_result);
            }
            catch (Exception e)
            {
                _content.Render().setAttribute("data-state", "failed");
                _status.Text = "Failed: " + AlkoveException.FromJs(e).Message;
            }
        }

        private void Download()
        {
            if (_result is null) return;
            var url = URL.createObjectURL(_result.ToStereoWav());
            var a = (HTMLAnchorElement)document.createElement("a");
            a.href = url;
            var name = _sourceName ?? "recording";
            var dot  = name.LastIndexOf('.');
            a.download = (dot > 0 ? name.Substring(0, dot) : name) + ".alkove.wav";
            a.click();
            window.setTimeout(_ => URL.revokeObjectURL(url), 1000);
        }

        public HTMLElement Render() => _content.Render();
    }
}
