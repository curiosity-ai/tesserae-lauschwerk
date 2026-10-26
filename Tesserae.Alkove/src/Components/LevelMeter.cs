using System;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Alkove
{
    /// <summary>
    /// A live level meter: the loudness of a <c>MediaStreamTrack</c> (the raw microphone, or a call's
    /// processed track), or of audio the host feeds it (<see cref="Feed"/>, an agent's packets). Shows
    /// the level in dBFS with a falling peak mark, from -60 dB to 0.
    ///
    /// Reads the track through an <c>AnalyserNode</c> on one audio context shared by every meter, and only
    /// while it is on screen. It never changes the track.
    /// </summary>
    public sealed class LevelMeter : IComponent
    {
        private const double FLOOR_DB = -60;

        private static AudioContext _sharedContext;

        private readonly HTMLElement _root;
        private readonly HTMLElement _fill;
        private readonly HTMLElement _peakMark;
        private readonly HTMLElement _value;
        private readonly HTMLElement _label;
        private readonly MountedLoop _loop;

        private MediaStreamAudioSourceNode _source;
        private AnalyserNode               _analyser;
        private es5.Float32Array           _buffer;
        private MediaStreamTrack           _track;

        private double _fed = double.NegativeInfinity;
        private double _fraction = -1;
        private string _fractionText;
        private double _peak = FLOOR_DB;
        private double _level = FLOOR_DB;

        public LevelMeter(string label = null)
        {
            AlkoveStyles.Ensure();

            _root     = Div(Att("tss-alkove-meter"));
            _label    = Div(Att("tss-alkove-meter-label"));
            var track = Div(Att("tss-alkove-meter-track"));
            _fill     = Div(Att("tss-alkove-meter-fill"));
            _peakMark = Div(Att("tss-alkove-meter-peak"));
            _value    = Div(Att("tss-alkove-meter-value"));

            track.appendChild(_fill);
            track.appendChild(_peakMark);
            _root.appendChild(_label);
            _root.appendChild(track);
            _root.appendChild(_value);

            _root.setAttribute("role", "meter");
            _root.setAttribute("aria-valuemin", FLOOR_DB.ToString());
            _root.setAttribute("aria-valuemax", "0");

            SetLabel(label);
            _loop = new MountedLoop(_root, Frame);
        }

        /// <summary>The current level in dBFS (-60 when silent).</summary>
        public double LevelDb => _level;

        /// <summary>The text on the left, or none.</summary>
        public LevelMeter SetLabel(string label)
        {
            _label.textContent   = label ?? "";
            _label.style.display = string.IsNullOrEmpty(label) ? "none" : "";
            _root.setAttribute("aria-label", string.IsNullOrEmpty(label) ? "Level" : label);
            return this;
        }

        /// <summary>Measures a track; null detaches. The track itself is not touched or stopped.</summary>
        public LevelMeter Track(MediaStreamTrack track)
        {
            if (_track == track) return this;

            Detach();
            _track = track;
            if (track is null) return this;

            if (_sharedContext is null) _sharedContext = new AudioContext();
            if (_sharedContext.state == "suspended") _sharedContext.resume();

            _source   = _sharedContext.createMediaStreamSource(new MediaStream(new[] { track }));
            _analyser = _sharedContext.createAnalyser();
            _analyser.fftSize = 1024;
            _buffer   = new es5.Float32Array((uint)_analyser.fftSize);
            _source.connect(_analyser);
            return this;
        }

        /// <summary>Feeds samples (float, -1 to 1) instead of a track: the meter shows their RMS.</summary>
        public LevelMeter Feed(es5.Float32Array samples)
        {
            _fed = Rms(samples, (int)samples.length);
            return this;
        }

        /// <summary>
        /// Shows a value that is not a level - 0 to 1, with its own text - instead of a track or fed
        /// samples: how close a wake word came, how far a download got. A negative fraction goes back to
        /// measuring.
        /// </summary>
        public LevelMeter SetFraction(double fraction, string text)
        {
            _fraction     = fraction;
            _fractionText = text;
            return this;
        }

        private void Detach()
        {
            if (_source is object)
            {
                _source.disconnect();
                _source = null;
            }
            _analyser = null;
            _track    = null;
        }

        private void Frame()
        {
            if (_fraction >= 0)
            {
                var f = Math.Max(0, Math.Min(1, _fraction));
                _peak = Math.Max(FLOOR_DB + f * -FLOOR_DB, _peak - 0.5);
                _fill.style.width    = Math.Round(f * 100, 1) + "%";
                _peakMark.style.left = Math.Round((_peak - FLOOR_DB) / -FLOOR_DB * 100, 1) + "%";
                _value.textContent   = _fractionText ?? Math.Round(f * 100) + "%";
                _root.setAttribute("aria-valuenow", Math.Round(f * 100).ToString());
                _root.classList.remove("tss-alkove-meter-hot");
                return;
            }

            double db;

            if (_analyser is object)
            {
                _analyser.getFloatTimeDomainData(_buffer);
                db = Rms(_buffer, (int)_buffer.length);
            }
            else
            {
                db = _fed;
            }

            if (double.IsNaN(db) || db < FLOOR_DB) db = FLOOR_DB;

            _level = db;
            _peak  = Math.Max(db, _peak - 0.5);

            var fraction = (db - FLOOR_DB) / -FLOOR_DB;
            _fill.style.width      = Math.Round(fraction * 100, 1) + "%";
            _peakMark.style.left   = Math.Round((_peak - FLOOR_DB) / -FLOOR_DB * 100, 1) + "%";
            _value.textContent     = db <= FLOOR_DB ? "-∞ dB" : Math.Round(db).ToString() + " dB";
            _root.setAttribute("aria-valuenow", Math.Round(db).ToString());
            _root.classList.toggle("tss-alkove-meter-hot", db > -3);
        }

        private static double Rms(es5.Float32Array samples, int n)
        {
            if (n == 0) return double.NegativeInfinity;
            double sum = 0;
            for (var i = 0; i < n; i++)
            {
                var v = samples[(uint)i];
                sum += v * v;
            }
            return 10 * Math.Log10(sum / n);
        }

        /// <summary>Stops measuring for good.</summary>
        public void Dispose()
        {
            _loop.Stop();
            Detach();
        }

        public HTMLElement Render() => _root;
    }
}
