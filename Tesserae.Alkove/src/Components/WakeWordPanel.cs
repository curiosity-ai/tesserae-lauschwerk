using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Transpose;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Alkove
{
    /// <summary>
    /// A ready-made wake word listener: type any word, pick its language, start listening. A meter shows
    /// how close what it hears comes to the word, live (Spot's best score against the word's threshold),
    /// and every wake is listed with its time and score. <see cref="Woke"/> hands the host the wake,
    /// with the word's audio - start the voice input there.
    ///
    /// Typed words need no training. A word enrolled with <see cref="WordEnrollmentPanel"/> can be given
    /// with <see cref="UseWord"/> instead: its thresholds are set on the user's voice.
    /// </summary>
    public sealed class WakeWordPanel : IComponent
    {
        private readonly TextBox     _word;
        private readonly Dropdown    _language;
        private readonly Button      _toggle;
        private readonly LevelMeter  _closeness = new LevelMeter("Closeness");
        private readonly Stack       _log       = VStack().WS().Class("tss-alkove-wake-log");
        private readonly TextBlock   _status    = TextBlock("").Small().Secondary().Class("tss-alkove-wake-status");
        private readonly IComponent  _content;

        private WakeListener _listener;
        private WakeWord     _word0;
        private WakeWord     _enrolled;
        private Voiceprint   _onlyMyVoice;
        private WakeContext  _context = WakeContext.Pause;
        private MediaStream  _stream;
        private bool         _busy;
        private int          _wakes;

        public WakeWordPanel(string word = "Hey Nova", SpeechLanguage language = SpeechLanguage.English)
        {
            _word     = TextBox(word).SetPlaceholder("Any word or short phrase").W(220.px());
            _language = LanguagePicker.Create(language);
            _toggle   = Button("Start listening").Primary().SetIcon(UIcons.Ear).OnClick(() => ToggleAsync().FireAndForget());

            _content = VStack().WS().Gap(8.px()).Class("tss-alkove-wake").Children(
                HStack().WS().Wrap().AlignItemsCenter().Gap(8.px()).Children(_word, _language, _toggle),
                _closeness.WS(),
                _status,
                _log);

            SetStatus("Not listening.");
            DomObserver.WhenMounted(_content.Render(), () => DomObserver.WhenRemoved(_content.Render(), Stop));
        }

        /// <summary>The word heard.</summary>
        public event Action<WakeEvent> Woke;

        public WakeWordPanel OnWake(Action<WakeEvent> handler)
        {
            Woke += handler;
            return this;
        }

        /// <summary>How many wakes since the panel started listening.</summary>
        public int Wakes => _wakes;

        public bool IsListening => _listener is object;

        /// <summary>The compiled word in use, once listening.</summary>
        public WakeWord Word => _enrolled ?? _word0;

        /// <summary>Listen for an enrolled (or stored) word instead of the typed one.</summary>
        public WakeWordPanel UseWord(WakeWord word)
        {
            _enrolled  = word;
            _word.Text = word?.Text ?? _word.Text;
            return this;
        }

        /// <summary>Only this voice wakes the listener.</summary>
        public WakeWordPanel OnlyMyVoice(Voiceprint print)
        {
            _onlyMyVoice = print;
            return this;
        }

        public WakeWordPanel Context(WakeContext context)
        {
            _context = context;
            return this;
        }

        private async Task ToggleAsync()
        {
            if (_busy) return;
            if (_listener is object) Stop();
            else await StartAsync();
        }

        public async Task StartAsync()
        {
            if (_busy || _listener is object) return;
            _busy = true;
            _toggle.Disabled();

            try
            {
                SetStatus("Compiling \"" + _word.Text + "\"...");
                _word0 = _enrolled is object && _enrolled.Text == _word.Text ? _enrolled : await AlkoveJs.CompileWordAsync(_word.Text, LanguagePicker.Read(_language));

                _stream = await Microphone.OpenAsync();

                _listener = await AlkoveJs.CreateWakeListenerAsync(new WakeListenerSettings(_word0) { Stream = _stream, Context = _context, OnlyMyVoice = _onlyMyVoice });
                _listener.Wake += Wake;
                _listener.Heard += Heard;
                _listener.ModeChanged += (mode, reason) => SetStatus(mode == VoiceMode.Raw ? "Loading Spot..." : "Listening for \"" + _word0.Text + "\" (/" + string.Join("/, /", _word0.Pronunciations) + "/).");

                _wakes = 0;
                _toggle.SetText("Stop").SetIcon(UIcons.EarDeaf);
                _word.Disabled();
                _language.Disabled();
                SetStatus("Loading Spot...");
            }
            catch (Exception e)
            {
                var error = AlkoveException.FromJs(e);
                Stop();
                SetStatus(error.Code == AlkoveException.LANG_UNSUPPORTED || error.Code is null ? error.Message : VoiceInputPanel.Explain(error));
            }
            finally
            {
                _busy = false;
                _toggle.Disabled(false);
            }
        }

        private void Heard(double speech, Dictionary<string, double> scores)
        {
            if (_word0 is null || !scores.TryGetValue(_word0.Text, out var score)) return;

            // A score is a log-probability: -6 is nothing like the word, the threshold around -2, 0 is
            // certain. The meter puts -6 at empty and the threshold at 80%, so a wake is a bar past 80.
            var threshold = _context == WakeContext.Anywhere ? _word0.Thresholds.anywhere : _word0.Thresholds.pause;
            var closeness = Math.Max(0, (score + 6) / (threshold + 6) * 0.8);
            _closeness.SetFraction(closeness, score.ToString("0.00"));
        }

        private void Wake(WakeEvent e)
        {
            _wakes++;
            _content.Render().setAttribute("data-wakes", _wakes.ToString());

            var audio = e.Audio is object ? Button().SetIcon(UIcons.Play).Tooltip("Play the word").OnClick(() => new Waveform(e.Audio).Play()) : null;
            var line = HStack().AlignItemsCenter().Gap(8.px()).Class("tss-alkove-wake-entry").Children(
                Badge(e.Word).Primary(),
                TextBlock("at " + e.Start.ToString("0.00") + " s, score " + e.Score.ToString("0.00") + " (threshold " + e.Threshold.ToString("0.00") + ")" + (e.Voice.HasValue ? ", voice " + e.Voice.Value.ToString("0.00") : "")).Small());

            if (audio is object) line.Add(audio);
            _log.Prepend(line);

            Woke?.Invoke(e);
        }

        public void Stop()
        {
            _listener?.Dispose();
            _listener = null;
            Microphone.Stop(_stream);
            _stream = null;
            _toggle.SetText("Start listening").SetIcon(UIcons.Ear);
            _word.Disabled(false);
            _language.Disabled(false);
            SetStatus("Not listening.");
        }

        private void SetStatus(string text) => _status.Text = text;

        public HTMLElement Render() => _content.Render();
    }
}
