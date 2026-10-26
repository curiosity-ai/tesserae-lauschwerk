using System;
using Tesserae;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// What listening looks like: a panel that opens over the interface, at the bottom of the page, with the sound of the room as bars -
    /// and, if asked, the words as they are heard. For the moment an assistant starts hearing the user (after the wake word, or when the
    /// microphone button was pressed), and until it has what it came for.
    ///
    /// The bars are the "Bars" study of the Listening Studies: twenty-nine square bars mirrored around the middle, the lowest frequencies
    /// at the centre and the highest at the edges, fed by sixteen bands of a short FFT of the latest audio. They rise quickly and fall
    /// slowly, breathe a little when nothing is heard (not under reduced motion), and the loudest one takes the primary colour.
    ///
    /// Give it audio the way <see cref="LiveWaveform"/> takes it: <see cref="Bind"/> an <see cref="AgentInput"/>, point it at a track
    /// (<see cref="Track"/>), or <see cref="Feed(es5.Float32Array)"/> samples. <see cref="Open"/> shows it, <see cref="Close"/> takes it
    /// away. With <see cref="ShowTranscript"/> it has a line of text under the bars that <see cref="SetTranscript"/> fills - speech-to-text is
    /// not part of this package, so the words come from wherever the host gets them.
    ///
    /// Drawn on a canvas in the theme's colour tokens, only while it is on screen.
    /// </summary>
    public sealed class ListeningOverlay : IComponent
    {
        private const int BARS  = 29;
        private const int BANDS = 16;

        /// <summary>The loudest band is never taken to be softer than this (in the units of <see cref="Frame"/>), so room noise does not fill the bars.</summary>
        private const double REFERENCE_FLOOR = 2.0;

        /// <summary>What lies below this is the noise floor and is not drawn.</summary>
        private const double NOISE_FLOOR = 0.18;

        private static AudioContext _sharedContext;

        private readonly HTMLElement       _root;
        private readonly HTMLElement       _state;
        private readonly HTMLElement       _text;
        private readonly HTMLCanvasElement _canvas;
        private readonly HTMLElement       _cancelHost;
        private readonly MountedLoop       _loop;

        // The latest audio: a ring of samples, the bands of its spectrum and what the bars are showing
        private double[] _window;
        private double[] _re;
        private double[] _im;
        private double[] _hann;
        private int[]    _edges;
        private int      _write;
        private int      _sampleRate;
        private readonly double[] _bands = new double[BANDS];
        private readonly double[] _bars  = new double[BARS];
        private double            _reference = REFERENCE_FLOOR;

        private AgentInput                 _input;
        private Action<es5.ArrayBuffer>    _onAudio;
        private MediaStreamAudioSourceNode _source;
        private AnalyserNode               _analyser;
        private es5.Float32Array           _buffer;

        private double _lastFrame;
        private double _closeTimer;
        private int    _drawnTheme = -1;
        private string _ink;
        private string _signal;
        private bool   _showTranscript;
        private bool   _open;

        public ListeningOverlay()
        {
            LauschwerkStyles.Ensure();

            _state  = Div(Att("tss-lw-overlay-state"));
            _canvas = (HTMLCanvasElement)document.createElement("canvas");
            _text   = Div(Att("tss-lw-overlay-text"));
            _cancelHost = Div(Att("tss-lw-overlay-cancel"));

            var head = Div(Att("tss-lw-overlay-head"), _state, _cancelHost);
            var body = Div(Att("tss-lw-overlay-body"), _canvas);

            _root = Div(Att("tss-lw-overlay"), head, body, _text);
            _root.setAttribute("role", "status");
            _root.setAttribute("aria-live", "polite");
            _canvas.setAttribute("aria-hidden", "true");

            _text.style.display = "none";
            _cancelHost.style.display = "none";

            SetSampleRate(AudioClip.LAUSCHWERK_RATE);
            SetStatus("Listening");

            _loop = new MountedLoop(_root, Frame);
        }

        /// <summary>Whether the overlay is on screen (between <see cref="Open"/> and <see cref="Close"/>).</summary>
        public bool IsOpen => _open;

        /// <summary>Raised when the user presses the cancel button (see <see cref="ShowCancel"/>).</summary>
        public event Action Cancelled;

        /// <summary>The line above the bars: "Listening", "Transcribing", "Heard"... Shown in capitals.</summary>
        public ListeningOverlay SetStatus(string status)
        {
            _state.textContent = status ?? "";
            _root.setAttribute("aria-label", status ?? "");
            return this;
        }

        /// <summary>Shows a line of text under the bars (<see cref="SetTranscript"/>): what the user is saying, as it is understood. Off by default.</summary>
        public ListeningOverlay ShowTranscript(bool show = true)
        {
            _showTranscript = show;
            _text.style.display = show ? "" : "none";
            return this;
        }

        /// <summary>
        /// The words heard so far, replacing the last call's. While <paramref name="isFinal"/> is false they are drawn softer, as what may still
        /// change; final text takes the full ink. Does nothing unless <see cref="ShowTranscript"/> is on.
        /// </summary>
        public ListeningOverlay SetTranscript(string text, bool isFinal = false)
        {
            if (!_showTranscript) return this;

            _text.textContent = text ?? "";
            _text.classList.toggle("tss-lw-overlay-final", isFinal);
            return this;
        }

        /// <summary>A cancel button in the corner that raises <see cref="Cancelled"/>.</summary>
        public ListeningOverlay ShowCancel(bool show = true)
        {
            if (show && _cancelHost.childElementCount == 0)
            {
                var button = Button().SetIcon(UIcons.Cross).NoBorder().NoBackground().Tooltip("Cancel").AriaLabel("Cancel").OnClick(() => Cancelled?.Invoke());
                _cancelHost.appendChild(button.Render());
            }

            _cancelHost.style.display = show ? "" : "none";
            return this;
        }

        /// <summary>The sample rate of what is fed (default 16000; <see cref="Bind"/> takes it from the input). Decides the size of the FFT: about 32 ms of audio.</summary>
        public ListeningOverlay SetSampleRate(int sampleRate)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (_window is object && sampleRate == _sampleRate) return this;

            _sampleRate = sampleRate;

            var n = 64;
            while (n < sampleRate * 0.032) n <<= 1;

            _window = new double[n];
            _re     = new double[n];
            _im     = new double[n];
            _hann   = new double[n];
            _write  = 0;

            for (var i = 0; i < n; i++) _hann[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1));

            // Bands spaced evenly on a log axis over the bins, at least one bin wide
            _edges = new int[BANDS + 1];
            var prev = 1;
            for (var i = 0; i <= BANDS; i++)
            {
                var e = Math.Max(prev + (i > 0 ? 1 : 0), (int)Math.Round(Math.Pow(n / 2 - 1, i / (double)BANDS)));
                _edges[i] = e;
                prev = e;
            }

            return this;
        }

        /// <summary>Feeds float samples, -1 to 1.</summary>
        public ListeningOverlay Feed(es5.Float32Array samples)
        {
            var n = (int)samples.length;
            for (var i = 0; i < n; i++) Push(samples[(uint)i]);
            return this;
        }

        /// <summary>Feeds a PCM packet, as an <see cref="AgentInput"/> raises it.</summary>
        public ListeningOverlay FeedPcm(es5.ArrayBuffer buffer, PcmFormat format)
        {
            if (format == PcmFormat.Float32) return Feed(new es5.Float32Array(buffer));

            var ints = new es5.Int16Array(buffer);
            var n = (int)ints.length;
            for (var i = 0; i < n; i++) Push(ints[(uint)i] / 32768.0);
            return this;
        }

        /// <summary>Shows an agent input: its audio packets are the bars. A new input replaces the old one; null unbinds. The input itself is not touched.</summary>
        public ListeningOverlay Bind(AgentInput input)
        {
            Unbind();
            if (input is null) return this;

            _input = input;
            SetSampleRate(input.SampleRate);

            _onAudio = pcm => FeedPcm(pcm, input.Format);
            input.Audio += _onAudio;
            return this;
        }

        private void Unbind()
        {
            if (_input is null) return;
            _input.Audio -= _onAudio;
            _input   = null;
            _onAudio = null;
        }

        /// <summary>Shows a track instead (the raw microphone, or a call input's processed one). The track itself is not touched. Null detaches.</summary>
        public ListeningOverlay Track(MediaStreamTrack track)
        {
            DetachTrack();
            if (track is null) return this;

            if (_sharedContext is null) _sharedContext = new AudioContext();
            if (_sharedContext.state == "suspended") _sharedContext.resume();

            SetSampleRate((int)_sharedContext.sampleRate);

            _source   = _sharedContext.createMediaStreamSource(new MediaStream(new[] { track }));
            _analyser = _sharedContext.createAnalyser();
            _analyser.fftSize = _window.Length;
            _buffer   = new es5.Float32Array((uint)_analyser.fftSize);
            _source.connect(_analyser);
            return this;
        }

        private void DetachTrack()
        {
            if (_source is object)
            {
                _source.disconnect();
                _source = null;
            }
            _analyser = null;
        }

        /// <summary>Opens the overlay over the page: it slides up from the bottom edge. Safe to call while it is open.</summary>
        public ListeningOverlay Open()
        {
            window.clearTimeout(_closeTimer);

            if (_root.parentElement is null)
            {
                for (var i = 0; i < _bars.Length; i++) _bars[i] = 0;
                document.body.appendChild(_root);
            }

            _open = true;

            // Next frame, so the entrance transitions from where it was put
            window.requestAnimationFrame(_ =>
            {
                if (_open) _root.classList.add("tss-lw-overlay-open");
            });

            return this;
        }

        /// <summary>Takes the overlay away, after <paramref name="delayMs"/> (a moment to read the last words), and lets go of the audio.</summary>
        public void Close(int delayMs = 0)
        {
            window.clearTimeout(_closeTimer);
            _open = false;

            _closeTimer = window.setTimeout(_ =>
            {
                _root.classList.remove("tss-lw-overlay-open");
                Unbind();
                DetachTrack();

                // After the exit transition
                _closeTimer = window.setTimeout(__ =>
                {
                    if (!_open) _root.remove();
                }, 240);
            }, delayMs);
        }

        // One sample into the ring, oldest first when read
        private void Push(double v)
        {
            _window[_write] = v;
            _write = (_write + 1) % _window.Length;
        }

        private void Frame()
        {
            var now = window.performance.now() / 1000;
            var dt  = _lastFrame == 0 ? 0.016 : Math.Min(0.05, now - _lastFrame);
            _lastFrame = now;

            if (_analyser is object)
            {
                _analyser.getFloatTimeDomainData(_buffer);
                for (var i = 0; i < (int)_buffer.length; i++) _window[i] = _buffer[(uint)i];
                _write = 0;
            }

            Analyze();

            var ratio  = window.devicePixelRatio > 0 ? window.devicePixelRatio : 1;
            var width  = _canvas.clientWidth;
            var height = _canvas.clientHeight;
            if (width <= 0 || height <= 0) return;

            var pw = (uint)Math.Round(width * ratio);
            var ph = (uint)Math.Round(height * ratio);
            if (_canvas.width != pw || _canvas.height != ph)
            {
                _canvas.width  = pw;
                _canvas.height = ph;
            }

            if (_drawnTheme != LauschwerkStyles.ThemeGeneration || _ink is null)
            {
                _drawnTheme = LauschwerkStyles.ThemeGeneration;
                _ink    = Color.EvalVar(Theme.Default.Foreground);
                _signal = Color.EvalVar(Theme.Primary.Background);
            }

            var ctx = (CanvasRenderingContext2D)_canvas.getContext("2d");
            ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
            ctx.clearRect(0, 0, width, height);

            DrawBars(ctx, width, height, now, dt);
        }

        // The sixteen bands of the latest ~32 ms: a Hann-windowed FFT, the power of each band's bins, taken to the fourth root as the study does
        // (a spectrum is far too steep to draw as it is) and scaled by the loudest band lately.
        private void Analyze()
        {
            var n = _window.Length;
            for (var i = 0; i < n; i++)
            {
                _re[i] = _window[(_write + i) % n] * _hann[i];
                _im[i] = 0;
            }

            Fft(_re, _im);

            var loudest = 0.0;
            for (var b = 0; b < BANDS; b++)
            {
                double power = 0;
                var from = _edges[b];
                var to   = Math.Min(n / 2, Math.Max(_edges[b + 1], from + 1));
                for (var k = from; k < to; k++) power += _re[k] * _re[k] + _im[k] * _im[k];

                var v = Math.Sqrt(Math.Sqrt(power / Math.Max(1, to - from)));
                _bands[b] = v;
                if (v > loudest) loudest = v;
            }

            // The reference falls slowly so a quiet voice fills the bars after a loud one, and never below the floor so noise does not
            _reference = Math.Max(REFERENCE_FLOOR, Math.Max(loudest, _reference * 0.996));

            for (var b = 0; b < BANDS; b++)
            {
                var scaled = Math.Max(0, _bands[b] - NOISE_FLOOR) / (_reference - NOISE_FLOOR);
                _bands[b] = Math.Pow(Math.Min(1, scaled), 0.9);
            }
        }

        private void DrawBars(CanvasRenderingContext2D ctx, double w, double h, double time, double dt)
        {
            var reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

            var pad  = 20.0;
            var gap  = Math.Max(3, w * 0.012);
            var bw   = (w - pad * 2 - (BARS - 1) * gap) / BARS;
            var mid  = (BARS - 1) / 2.0;
            var best = 0.0;
            var mi   = (int)mid;

            for (var i = 0; i < BARS; i++)
            {
                // The middle bar is the lowest band; the edges, the highest
                var q = Math.Abs(i - mid) / mid * (BANDS - 1);
                var a = (int)Math.Floor(q);
                var b = Math.Min(BANDS - 1, a + 1);
                var f = q - a;

                var target = _bands[a] * (1 - f) + _bands[b] * f;
                if (!reduce) target = Math.Max(target, 0.02 + 0.015 * (1 + Math.Sin(time * 1.6 + i * 0.55)));

                // Quick to rise, slow to fall
                var rate = target > _bars[i] ? 0.5 : 0.12;
                _bars[i] += (target - _bars[i]) * (1 - Math.Pow(1 - rate, dt * 60));

                if (_bars[i] > best)
                {
                    best = _bars[i];
                    mi   = i;
                }
            }

            for (var i = 0; i < BARS; i++)
            {
                var half = Math.Max(bw / 2, _bars[i] * (h - 24) / 2);
                ctx.fillStyle = i == mi ? _signal : _ink;
                ctx.fillRect(pad + i * (bw + gap), h / 2 - half, bw, half * 2);
            }
        }

        // In-place radix-2 FFT
        private static void Fft(double[] re, double[] im)
        {
            var n = re.Length;

            for (int i = 1, j = 0; i < n; i++)
            {
                var bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;

                if (i < j)
                {
                    var tr = re[i]; re[i] = re[j]; re[j] = tr;
                    var ti = im[i]; im[i] = im[j]; im[j] = ti;
                }
            }

            for (var len = 2; len <= n; len <<= 1)
            {
                var ang = -2 * Math.PI / len;
                var wr  = Math.Cos(ang);
                var wi  = Math.Sin(ang);

                for (var i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;

                    for (var j = 0; j < len / 2; j++)
                    {
                        var a = i + j;
                        var b = a + len / 2;
                        var tr = re[b] * cr - im[b] * ci;
                        var ti = re[b] * ci + im[b] * cr;

                        re[b] = re[a] - tr;
                        im[b] = im[a] - ti;
                        re[a] += tr;
                        im[a] += ti;

                        var ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }

        /// <summary>Stops for good: closes, unbinds and ends the drawing loop.</summary>
        public void Dispose()
        {
            Close();
            _loop.Stop();
            Unbind();
            DetachTrack();
        }

        public HTMLElement Render() => _root;
    }
}
