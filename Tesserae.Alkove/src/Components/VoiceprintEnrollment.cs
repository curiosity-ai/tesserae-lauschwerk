using System;
using System.Threading.Tasks;
using Transpose;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Alkove
{
    /// <summary>
    /// An enrollment screen for a voiceprint: a sentence to read aloud, a start button, and a progress
    /// bar while alkove learns the voice (about 6 s of it). When the print is ready it stops the
    /// microphone and raises <see cref="Enrolled"/>; give the print to an input
    /// (<see cref="VoiceInputSettings.Voiceprint"/>) to keep only that voice.
    ///
    /// With <see cref="RememberIn"/> the print is stored in <c>localStorage</c> and offered back on the
    /// next visit. A voiceprint is biometric data: tell the user, and let them <see cref="Forget"/> it.
    /// </summary>
    public sealed class VoiceprintEnrollment : IComponent
    {
        public const string DEFAULT_TEXT = "The quick brown fox jumps over the lazy dog. I am reading these sentences out loud, so that the system can learn the sound of my voice.";

        private readonly TextBlock         _prompt;
        private readonly Button            _start;
        private readonly Button            _forget;
        private readonly ProgressIndicator _progress = ProgressIndicator();
        private readonly TextBlock         _status   = TextBlock("").Small().Secondary().Class("tss-alkove-enroll-status");
        private readonly LevelMeter        _meter    = new LevelMeter("Microphone");
        private readonly IComponent        _content;

        private CallInput   _input;
        private MediaStream _stream;
        private string      _storageKey;
        private Voiceprint  _voiceprint;

        public VoiceprintEnrollment(string text = DEFAULT_TEXT)
        {
            _prompt = TextBlock(text).MediumPlus().Class("tss-alkove-enroll-text");
            _start  = Button("Start reading").Primary().SetIcon(UIcons.Microphone).OnClick(() =>
            {
                if (_input is object) Stop();
                else StartAsync().FireAndForget();
            });
            _forget = Button("Forget my voice").SetIcon(UIcons.Trash).OnClick(Forget);
            _forget.Collapse();

            _content = VStack().WS().Gap(8.px()).Class("tss-alkove-enroll").Children(
                TextBlock("Read this aloud, at your normal speaking voice:").Small().Secondary(),
                _prompt,
                HStack().WS().Wrap().AlignItemsCenter().Gap(8.px()).Children(_start, _forget),
                _meter.WS(),
                _progress.WS(),
                _status);

            ShowProgress(0, "Not started");
            DomObserver.WhenMounted(_content.Render(), () => DomObserver.WhenRemoved(_content.Render(), Stop));
        }

        /// <summary>The learned (or remembered) print, or null.</summary>
        public Voiceprint Voiceprint => _voiceprint;

        /// <summary>The print is ready.</summary>
        public event Action<Voiceprint> Enrolled;

        public VoiceprintEnrollment OnEnrolled(Action<Voiceprint> handler)
        {
            Enrolled += handler;
            return this;
        }

        /// <summary>
        /// Keeps the print in <c>localStorage[key]</c>, and loads one already there (when it was made by
        /// the bundled Timbre). Loading needs the SDK, so it happens in the background.
        /// </summary>
        public VoiceprintEnrollment RememberIn(string key)
        {
            _storageKey = key;
            RestoreAsync().FireAndForget();
            return this;
        }

        private async Task RestoreAsync()
        {
            var json = localStorage.getItem(_storageKey);
            if (string.IsNullOrEmpty(json)) return;

            var print = await Voiceprint.FromJsonAsync(json);
            if (print is null)
            {
                localStorage.removeItem(_storageKey);
                ShowProgress(0, "The stored voiceprint was made by another model: read the text again.");
                return;
            }

            Done(print, remembered: true);
        }

        public async Task StartAsync()
        {
            if (_input is object) return;

            _start.Disabled();
            ShowProgress(0, "Listening...");

            try
            {
                _stream = await Microphone.OpenAsync();
                _meter.Track(_stream.getAudioTracks()[0]);
                _input = await AlkoveJs.CreateCallInputAsync(new CallInputSettings { Stream = _stream, Learn = true, KeepAliveWhenHidden = false });

                _input.LearnProgress     += p => ShowProgress(p, "Learning your voice " + Math.Round(p * 100) + "%");
                _input.VoiceprintLearned += print => Done(print, remembered: false);
                _input.Diagnostic        += d => { if (d.code == "print.restart") ShowProgress(0, "That did not sound like the same voice - starting over."); };
                _input.ModeChanged       += (mode, reason) => { if (reason == RawReason.Loading) ShowProgress(0, "Loading the models..."); };

                _start.SetText("Stop").Disabled(false);
            }
            catch (Exception e)
            {
                Stop();
                ShowProgress(0, VoiceInputPanel.Explain(AlkoveException.FromJs(e)));
            }
        }

        private void Done(Voiceprint print, bool remembered)
        {
            _voiceprint = print;
            if (_storageKey is object && !remembered) localStorage.setItem(_storageKey, print.ToJson());

            Stop();
            ShowProgress(1, (remembered ? "Remembered voiceprint" : "Voiceprint ready") + " - " + Math.Round(print.Seconds, 1) + " s of voice.");
            _forget.Show();
            Enrolled?.Invoke(print);
        }

        /// <summary>Drops the print, here and in storage.</summary>
        public void Forget()
        {
            _voiceprint = null;
            if (_storageKey is object) localStorage.removeItem(_storageKey);
            _forget.Collapse();
            ShowProgress(0, "Forgotten.");
        }

        public void Stop()
        {
            _input?.Dispose();
            _input = null;
            Microphone.Stop(_stream);
            _stream = null;
            _meter.Track(null);
            _start.SetText("Start reading").Disabled(false);
        }

        private void ShowProgress(double fraction, string text)
        {
            _progress.Progress((float)(fraction * 100));
            _status.Text = text;
            _content?.Render().setAttribute("data-progress", Math.Round(fraction * 100).ToString());
        }

        public HTMLElement Render() => _content.Render();
    }
}
