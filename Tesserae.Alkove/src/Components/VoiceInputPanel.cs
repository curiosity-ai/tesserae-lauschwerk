using System;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Alkove
{
    /// <summary>
    /// A ready-made control for a call or agent input: a start/stop button, the mode the models are in,
    /// whether someone speaks, the latency, and two meters - the microphone as it arrives and what
    /// alkove lets out - so the difference Focus makes is visible as well as audible. While the input
    /// learns a voiceprint, a progress bar shows how far it has got.
    ///
    /// It opens the microphone itself (<see cref="Microphone.OpenAsync"/>) so it can meter it, and hands
    /// the stream to the input; it stops both when stopped, when <see cref="Dispose"/>d, or when it
    /// leaves the page. <see cref="Started"/> gives the host the input to wire up - <c>pc.addTrack</c>,
    /// a speech-to-text socket.
    /// </summary>
    public sealed class VoiceInputPanel : IComponent
    {
        private readonly Func<MediaStream, Task<VoiceInput>> _create;
        private readonly Button            _toggle;
        private readonly ModeBadge         _mode    = new ModeBadge();
        private readonly SpeechIndicator   _speech  = new SpeechIndicator();
        private readonly TextBlock         _latency = TextBlock("").Small().Secondary();
        private readonly LevelMeter        _rawMeter = new LevelMeter("Microphone");
        private readonly LevelMeter        _outMeter = new LevelMeter("alkove");
        private readonly ProgressIndicator _learn   = ProgressIndicator();
        private readonly TextBlock         _learnText = TextBlock("").Small().Secondary();
        private readonly Stack             _learnRow;
        private readonly TextBlock         _error   = TextBlock("").Small().Foreground(Theme.Danger.Background);
        private readonly IComponent        _content;

        private VoiceInput  _input;
        private MediaStream _stream;
        private bool        _busy;
        private double      _timer;

        private VoiceInputPanel(Func<MediaStream, Task<VoiceInput>> create)
        {
            _create = create;
            _toggle = Button("Start").Primary().SetIcon(UIcons.Microphone).OnClick(() => ToggleAsync().FireAndForget());

            _learnRow = HStack().WS().AlignItemsCenter().Gap(8.px()).Children(_learn.W(1).Grow(), _learnText).Collapse();

            _content = VStack().WS().Gap(8.px()).Class("tss-alkove-panel").Children(
                HStack().WS().Wrap().AlignItemsCenter().Gap(12.px()).Children(_toggle, _mode, _speech, _latency),
                _rawMeter.WS(),
                _outMeter.WS(),
                _learnRow,
                _error.Collapse());

            _mode.Bind(null);

            // Leaving the page stops the microphone: a panel nobody can see must not keep listening.
            DomObserver.WhenMounted(_content.Render(), () => DomObserver.WhenRemoved(_content.Render(), Stop));
        }

        /// <summary>A panel driving a <see cref="CallInput"/>; <paramref name="settings"/> is read at each start.</summary>
        public static VoiceInputPanel ForCall(Func<CallInputSettings> settings = null) => new VoiceInputPanel(async stream =>
        {
            var s = settings?.Invoke() ?? new CallInputSettings();
            s.Stream = stream;
            return await AlkoveJs.CreateCallInputAsync(s);
        });

        /// <summary>A panel driving an <see cref="AgentInput"/>; the output meter follows its packets.</summary>
        public static VoiceInputPanel ForAgent(Func<AgentInputSettings> settings = null) => new VoiceInputPanel(async stream =>
        {
            var s = settings?.Invoke() ?? new AgentInputSettings();
            s.Stream = stream;
            s.Format = PcmFormat.Float32;
            s.SampleRate = AudioClip.ALKOVE_RATE;
            return await AlkoveJs.CreateAgentInputAsync(s);
        });

        /// <summary>The running input, or null.</summary>
        public VoiceInput Input => _input;

        public bool IsRunning => _input is object;

        /// <summary>The input started: wire it up here.</summary>
        public event Action<VoiceInput> Started;

        /// <summary>The input stopped (it is already disposed).</summary>
        public event Action Stopped;

        /// <summary>The input failed to open (<see cref="AlkoveException.Code"/> says why).</summary>
        public event Action<AlkoveException> Failed;

        public VoiceInputPanel OnStarted(Action<VoiceInput> handler)
        {
            Started += handler;
            return this;
        }

        public VoiceInputPanel OnStopped(Action handler)
        {
            Stopped += handler;
            return this;
        }

        private async Task ToggleAsync()
        {
            if (_busy) return;

            if (_input is object)
            {
                Stop();
                return;
            }

            await StartAsync();
        }

        /// <summary>Starts the input (asks for the microphone). Usually from the panel's own button.</summary>
        public async Task StartAsync()
        {
            if (_busy || _input is object) return;
            _busy = true;
            _toggle.Disabled();
            _error.Collapse();

            try
            {
                _stream = await Microphone.OpenAsync();
                _rawMeter.Track(_stream.getAudioTracks()[0]);

                _input = await _create(_stream);

                _mode.Bind(_input);
                _speech.Bind(_input);

                if (_input.Track is object) _outMeter.Track(_input.Track);
                if (_input is AgentInput agent) agent.Audio += buffer => _outMeter.Feed(new es5.Float32Array(buffer));

                _input.LearnProgress += ShowLearn;
                _input.VoiceprintLearned += _ => ShowLearn(1);

                _timer = window.setInterval(_ => _latency.Text = _input is object && _input.LatencyMs > 0 ? Math.Round(_input.LatencyMs) + " ms" : "", 500);

                _toggle.SetText("Stop").SetIcon(UIcons.MicrophoneSlash);
                Started?.Invoke(_input);
            }
            catch (Exception e)
            {
                var error = AlkoveException.FromJs(e);
                Microphone.Stop(_stream);
                _stream = null;
                _rawMeter.Track(null);
                _error.Text = Explain(error);
                _error.Show();
                Failed?.Invoke(error);
            }
            finally
            {
                _busy = false;
                _toggle.Disabled(false);
            }
        }

        private void ShowLearn(double progress)
        {
            _learnRow.Show();
            _learn.Progress((float)(progress * 100));
            _learnText.Text = progress >= 1 ? "Voiceprint ready" : "Learning the voice " + Math.Round(progress * 100) + "%";
        }

        /// <summary>Stops the input and the microphone.</summary>
        public void Stop()
        {
            if (_timer != 0)
            {
                window.clearInterval(_timer);
                _timer = 0;
            }

            var hadInput = _input is object;

            _input?.Dispose();
            _input = null;
            Microphone.Stop(_stream);
            _stream = null;

            _rawMeter.Track(null);
            _outMeter.Track(null);
            _mode.Bind(null);
            _speech.Bind(null);
            _latency.Text = "";
            _learnRow.Collapse();
            _toggle.SetText("Start").SetIcon(UIcons.Microphone);

            if (hadInput) Stopped?.Invoke();
        }

        public void Dispose()
        {
            Stop();
            _rawMeter.Dispose();
            _outMeter.Dispose();
        }

        internal static string Explain(AlkoveException error)
        {
            switch (error.Code)
            {
                case AlkoveException.MIC_DENIED:          return "The microphone was refused. Allow it in the browser's address bar and try again.";
                case AlkoveException.MIC_NOT_FOUND:       return "No microphone was found.";
                case AlkoveException.MIC_BUSY:            return "The microphone is in use by another application.";
                case AlkoveException.PAGE_INSECURE:       return "The microphone needs HTTPS (or localhost).";
                case AlkoveException.BROWSER_UNSUPPORTED: return "This browser has no AudioWorklet.";
                default:                                  return error.Message;
            }
        }

        public HTMLElement Render() => _content.Render();
    }
}
