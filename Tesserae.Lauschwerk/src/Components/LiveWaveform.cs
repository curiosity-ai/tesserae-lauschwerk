using System;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// A scrolling waveform of live audio: the newest sound enters at the right edge and moves left, so the last
    /// few seconds are always on screen. Where <see cref="Waveform"/> draws a finished recording, this draws what is
    /// being said now - for a voice overlay, or beside a <see cref="VoiceInput"/>.
    ///
    /// Feed it samples (<see cref="Feed(es5.Float32Array)"/>, <see cref="FeedPcm"/>), bind it to an agent input
    /// (<see cref="Bind"/>: its audio packets, and its Cue speech shaded), or point it at a track
    /// (<see cref="Track"/>). Audio is reduced to one peak column per 20 ms, so a long history costs a few hundred
    /// numbers. It scrolls with the audio, not the clock: when nothing is fed it stands still.
    ///
    /// Drawn on a canvas at the device's pixel ratio in the theme's colours (read from the <c>--tss-*</c> tokens),
    /// only while on screen and only when something changed.
    /// </summary>
    public sealed class LiveWaveform : IComponent
    {
        /// <summary>Peak columns per second of audio.</summary>
        public const int COLUMNS_PER_SECOND = 50;

        private const double DEFAULT_SECONDS = 5;

        // Bits of a column's flag: Cue heard speech, something marked it
        private const byte SPEECH = 1;
        private const byte MARKED = 2;

        private static AudioContext _sharedContext;

        private readonly HTMLElement       _root;
        private readonly HTMLCanvasElement _canvas;
        private readonly MountedLoop       _loop;

        // Ring of peak columns, newest at _head - 1.
        private es5.Float32Array _peaks;
        private es5.Uint8Array   _speech;
        private int              _columns;
        private int              _head;
        private int              _filled;

        private int    _sampleRate = AudioClip.LAUSCHWERK_RATE;
        private int    _samplesPerColumn;
        private int    _pending;
        private double _pendingPeak;
        private bool   _speaking;
        private double _seconds = DEFAULT_SECONDS;

        private bool _dirty = true;
        private int  _drawnWidth;
        private int  _drawnHeight;
        private int  _drawnTheme = -1;

        private AgentInput          _input;
        private Action<es5.ArrayBuffer> _onAudio;
        private MediaStreamAudioSourceNode _source;
        private AnalyserNode               _analyser;
        private es5.Float32Array           _buffer;
        private double                     _lastTrackFrame;

        public LiveWaveform()
        {
            LauschwerkStyles.Ensure();

            _root   = Div(Att("tss-lw-live"));
            _canvas = (HTMLCanvasElement)document.createElement("canvas");
            _root.appendChild(_canvas);
            _root.setAttribute("role", "img");
            _root.setAttribute("aria-label", "Live audio");

            Allocate();
            _loop = new MountedLoop(_root, Frame);
        }

        /// <summary>The height of the drawing (default 96px).</summary>
        public LiveWaveform SetHeight(UnitSize height)
        {
            _root.style.height = height.ToString();
            _dirty = true;
            return this;
        }

        /// <summary>How many seconds of history fit across the width (default 5). Keeps what has been fed.</summary>
        public LiveWaveform SetSeconds(double seconds)
        {
            if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (seconds == _seconds) return this;

            var oldPeaks  = _peaks;
            var oldSpeech = _speech;
            var oldColumns = _columns;
            var oldHead   = _head;
            var oldFilled = _filled;

            _seconds = seconds;
            Allocate();

            // Carry the newest columns over, oldest first.
            var keep = Math.Min(oldFilled, _columns);
            for (var i = keep; i > 0; i--)
            {
                var src = ((oldHead - i) % oldColumns + oldColumns) % oldColumns;
                Push(oldPeaks[(uint)src], oldSpeech[(uint)src] != 0);
            }

            _dirty = true;
            return this;
        }

        /// <summary>The sample rate of what is fed (default 16000; <see cref="Bind"/> takes it from the input).</summary>
        public LiveWaveform SetSampleRate(int sampleRate)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            _sampleRate       = sampleRate;
            _samplesPerColumn = Math.Max(1, (int)Math.Round(sampleRate / (double)COLUMNS_PER_SECOND));
            return this;
        }

        /// <summary>
        /// Removes the border, so the waveform can sit on another surface (an overlay). The background stays
        /// transparent in both cases.
        /// </summary>
        public LiveWaveform Frameless(bool frameless = true)
        {
            _root.classList.toggle("tss-lw-live-frameless", frameless);
            return this;
        }

        /// <summary>Shades the columns that arrive while this is true - set it from an input's speech events.</summary>
        public LiveWaveform SetSpeaking(bool speaking)
        {
            _speaking = speaking;
            return this;
        }

        public bool IsSpeaking => _speaking;

        /// <summary>How many times <see cref="Mark"/> has been called since the last <see cref="Clear"/>.</summary>
        public int Marks { get; private set; }

        /// <summary>
        /// Highlights a stretch of what is already on screen, given as seconds before now (<paramref name="secondsAgoStart"/> is the
        /// older end). For marking the moment something was recognized: the event arrives after the audio it is about.
        /// </summary>
        public LiveWaveform Mark(double secondsAgoStart, double secondsAgoEnd)
        {
            var from = Math.Max(0, (int)Math.Floor(Math.Min(secondsAgoStart, secondsAgoEnd) * COLUMNS_PER_SECOND));
            var to   = Math.Min(_filled - 1, (int)Math.Ceiling(Math.Max(secondsAgoStart, secondsAgoEnd) * COLUMNS_PER_SECOND));

            for (var i = from; i <= to; i++)
            {
                var src = ((_head - 1 - i) % _columns + _columns) % _columns;
                _speech[(uint)src] = (byte)(_speech[(uint)src] | MARKED);
            }

            Marks++;
            _root.setAttribute("data-marks", Marks.ToString());
            _dirty = true;
            return this;
        }

        /// <summary>
        /// Highlights where a wake word was heard, from its event on the bound input's clock (see <see cref="Bind"/>). The word as the
        /// engine placed it, with a little around it so it reads on a short word.
        /// </summary>
        public LiveWaveform MarkWake(WakeEvent wake)
        {
            if (_input is null || wake is null) return this;

            var now = _input.Now();

            return Mark(now - wake.End - 0.05, now - wake.Start + 0.05);
        }

        /// <summary>The peak of the newest column, 0 to 1.</summary>
        public double Level => _filled == 0 ? 0 : _peaks[(uint)((_head - 1 + _columns) % _columns)];

        /// <summary>Forgets what was fed; the history is empty again.</summary>
        public LiveWaveform Clear()
        {
            for (var i = 0; i < _columns; i++)
            {
                _peaks[(uint)i]  = 0;
                _speech[(uint)i] = 0;
            }
            _head = 0;
            _filled = 0;
            _pending = 0;
            _pendingPeak = 0;
            Marks = 0;
            _root.setAttribute("data-marks", "0");
            _dirty = true;
            return this;
        }

        /// <summary>Feeds float samples, -1 to 1.</summary>
        public LiveWaveform Feed(es5.Float32Array samples)
        {
            var n = (int)samples.length;
            for (var i = 0; i < n; i++) Sample(samples[(uint)i]);
            _dirty = true;
            return this;
        }

        /// <summary>Feeds a PCM packet, as an <see cref="AgentInput"/> raises it.</summary>
        public LiveWaveform FeedPcm(es5.ArrayBuffer buffer, PcmFormat format)
        {
            if (format == PcmFormat.Float32) return Feed(new es5.Float32Array(buffer));

            var ints = new es5.Int16Array(buffer);
            var n = (int)ints.length;
            for (var i = 0; i < n; i++) Sample(ints[(uint)i] / 32768.0);
            _dirty = true;
            return this;
        }

        /// <summary>
        /// Shows an agent input: its audio packets scroll in, and the columns that arrive while Cue hears speech
        /// are shaded. A new input replaces the old one; null unbinds. The input itself is not touched.
        /// </summary>
        public LiveWaveform Bind(AgentInput input)
        {
            Unbind();
            if (input is null) return this;

            _input = input;
            SetSampleRate(input.SampleRate);
            SetSpeaking(input.IsSpeaking);

            _onAudio = pcm => FeedPcm(pcm, input.Format);
            input.Audio         += _onAudio;
            input.SpeechStarted += Started;
            input.SpeechEnded   += Ended;
            return this;
        }

        private void Started(ISpeechEvent e) => _speaking = true;
        private void Ended(ISpeechEvent e)   => _speaking = false;

        private void Unbind()
        {
            if (_input is null) return;
            _input.Audio         -= _onAudio;
            _input.SpeechStarted -= Started;
            _input.SpeechEnded   -= Ended;
            _input = null;
            _onAudio = null;
        }

        /// <summary>
        /// Shows a track instead (the raw microphone, or a call input's processed one). It is read through an
        /// analyser once per frame, so it is shown at the screen's pace; the track itself is not touched.
        /// Null detaches.
        /// </summary>
        public LiveWaveform Track(MediaStreamTrack track)
        {
            DetachTrack();
            if (track is null) return this;

            if (_sharedContext is null) _sharedContext = new AudioContext();
            if (_sharedContext.state == "suspended") _sharedContext.resume();

            SetSampleRate((int)_sharedContext.sampleRate);
            _source   = _sharedContext.createMediaStreamSource(new MediaStream(new[] { track }));
            _analyser = _sharedContext.createAnalyser();
            _analyser.fftSize = 2048;
            _buffer   = new es5.Float32Array((uint)_analyser.fftSize);
            _source.connect(_analyser);
            _lastTrackFrame = window.performance.now();
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

        private void Allocate()
        {
            _columns = Math.Max(2, (int)Math.Round(_seconds * COLUMNS_PER_SECOND));
            _peaks   = new es5.Float32Array((uint)_columns);
            _speech  = new es5.Uint8Array((uint)_columns);
            _head    = 0;
            _filled  = 0;
            _pending = 0;
            _pendingPeak = 0;
            _samplesPerColumn = Math.Max(1, (int)Math.Round(_sampleRate / (double)COLUMNS_PER_SECOND));
        }

        private void Sample(double v)
        {
            var a = Math.Abs(v);
            if (a > _pendingPeak) _pendingPeak = a;

            if (++_pending < _samplesPerColumn) return;

            Push(_pendingPeak, _speaking);
            _pending = 0;
            _pendingPeak = 0;
        }

        private void Push(double peak, bool speech)
        {
            _peaks[(uint)_head]  = (float)Math.Min(1, peak);
            _speech[(uint)_head] = speech ? SPEECH : (byte)0;
            _head = (_head + 1) % _columns;
            if (_filled < _columns) _filled++;
        }

        private void Frame()
        {
            if (_analyser is object)
            {
                // The samples that went by since the last frame, from the end of the analyser's window.
                var now = window.performance.now();
                var want = (int)Math.Min(_buffer.length, Math.Max(1, Math.Round((now - _lastTrackFrame) / 1000 * _sampleRate)));
                _lastTrackFrame = now;
                _analyser.getFloatTimeDomainData(_buffer);
                for (var i = (int)_buffer.length - want; i < (int)_buffer.length; i++) Sample(_buffer[(uint)i]);
                _dirty = true;
            }

            var ratio  = window.devicePixelRatio > 0 ? window.devicePixelRatio : 1;
            var width  = (int)Math.Round(_root.clientWidth * ratio);
            var height = (int)Math.Round(_root.clientHeight * ratio);

            if (_drawnTheme != LauschwerkStyles.ThemeGeneration) _dirty = true;
            if (!_dirty && width == _drawnWidth && height == _drawnHeight) return;
            if (width <= 0 || height <= 0) return;

            _dirty       = false;
            _drawnTheme  = LauschwerkStyles.ThemeGeneration;
            _drawnWidth  = width;
            _drawnHeight = height;

            _canvas.width  = (uint)width;
            _canvas.height = (uint)height;

            Draw(width, height, ratio);
        }

        private void Draw(int width, int height, double ratio)
        {
            var ctx = (CanvasRenderingContext2D)_canvas.getContext("2d");
            ctx.clearRect(0, 0, width, height);

            var muted   = Color.EvalVar(Theme.Secondary.Foreground);
            var primary = Color.EvalVar(Theme.Primary.Background);

            var colWidth = width / (double)_columns;
            var bar      = Math.Max(1, Math.Floor(colWidth) - (colWidth >= 3 * ratio ? Math.Max(1, Math.Round(ratio)) : 0));
            var mid      = height / 2.0;
            var minHalf  = Math.Max(ratio / 2, 0.5);

            // Newest column at the right edge.
            for (var i = 0; i < _filled; i++)
            {
                var src  = ((_head - 1 - i) % _columns + _columns) % _columns;
                var x    = width - (i + 1) * colWidth;
                var half = Math.Max(minHalf, _peaks[(uint)src] * mid);
                var flag = _speech[(uint)src];

                // A marked stretch is a band behind the bars, so it reads whatever the bars do
                if ((flag & MARKED) != 0)
                {
                    ctx.globalAlpha = 0.22;
                    ctx.fillStyle   = primary;
                    ctx.fillRect(Math.Round(x), 0, Math.Ceiling(colWidth), height);
                    ctx.globalAlpha = 1;
                }

                ctx.fillStyle = flag != 0 ? primary : muted;
                ctx.fillRect(Math.Round(x), mid - half, bar, half * 2);
            }
        }

        /// <summary>Stops for good: unbinds, detaches the track and ends the drawing loop.</summary>
        public void Dispose()
        {
            _loop.Stop();
            Unbind();
            DetachTrack();
        }

        public HTMLElement Render() => _root;
    }
}
