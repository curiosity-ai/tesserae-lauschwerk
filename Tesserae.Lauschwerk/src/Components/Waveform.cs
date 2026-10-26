using System;
using System.Collections.Generic;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// A recording drawn as a waveform, with where Cue heard speech shaded behind it and where Spot heard
    /// a word marked on it - what <see cref="LauschwerkJs.ProcessAsync(object, ProcessSettings, Action{double})"/>
    /// returns, made visible. Click it (or call <see cref="Play"/>) to hear the clip with a cursor
    /// following along.
    ///
    /// Drawn on a canvas at the device's pixel ratio, in the theme's colours, read from the
    /// <c>--tss-*</c> tokens when it draws: it redraws when it is resized and when the theme changes.
    /// </summary>
    public sealed class Waveform : IComponent
    {
        private readonly HTMLElement       _root;
        private readonly HTMLCanvasElement _canvas;
        private readonly MountedLoop       _loop;
        private readonly List<SpeechSpan>  _spans = new List<SpeechSpan>();
        private readonly List<WakeEvent>   _wakes = new List<WakeEvent>();

        private AudioClip        _clip;
        private HTMLAudioElement _audio;
        private string           _audioUrl;
        private double           _drawnCursor = -1;
        private int              _drawnWidth;
        private int              _drawnHeight;
        private bool             _dirty = true;
        private int              _drawnTheme = -1;

        public Waveform(AudioClip clip = null)
        {
            LauschwerkStyles.Ensure();

            _root   = Div(Att("tss-lw-waveform"));
            _canvas = (HTMLCanvasElement)document.createElement("canvas");
            _root.appendChild(_canvas);

            _root.setAttribute("role", "img");
            _root.tabIndex = 0;
            _root.addEventListener("click", _ => Toggle());
            _root.addEventListener("keydown", e =>
            {
                var key = ((KeyboardEvent)e).key;
                if (key == "Enter" || key == " ")
                {
                    e.preventDefault();
                    Toggle();
                }
            });

            // The loop only draws while on screen, and only when something (size, data, theme, cursor)
            // changed; a theme change is seen through LauschwerkStyles.ThemeGeneration.
            _loop = new MountedLoop(_root, Frame);

            SetClip(clip);
        }

        public AudioClip Clip => _clip;

        /// <summary>The height of the drawing (default 96px).</summary>
        public Waveform SetHeight(UnitSize height)
        {
            _root.style.height = height.ToString();
            _dirty = true;
            return this;
        }

        public Waveform SetClip(AudioClip clip)
        {
            Stop();
            _clip = clip;
            _dirty = true;

            if (_audioUrl is object)
            {
                URL.revokeObjectURL(_audioUrl);
                _audioUrl = null;
            }

            _root.setAttribute("aria-label", clip is null ? "No recording" : "Recording, " + clip.Duration.ToString("0.0") + " s - press Enter to play");
            return this;
        }

        /// <summary>Shades these spans (seconds) - typically <see cref="ProcessedAudio.Speech"/>.</summary>
        public Waveform SetSpeech(IEnumerable<SpeechSpan> spans)
        {
            _spans.Clear();
            if (spans is object) _spans.AddRange(spans);
            _dirty = true;
            return this;
        }

        /// <summary>Marks these wakes - typically <see cref="ProcessedAudio.Wakes"/>.</summary>
        public Waveform SetWakes(IEnumerable<WakeEvent> wakes)
        {
            _wakes.Clear();
            if (wakes is object) _wakes.AddRange(wakes);
            _dirty = true;
            return this;
        }

        public bool IsPlaying => _audio is object && !_audio.paused;

        /// <summary>Plays the clip from the start, or resumes it.</summary>
        public void Play()
        {
            if (_clip is null) return;

            if (_audio is null)
            {
                _audioUrl = URL.createObjectURL(_clip.ToWav());
                _audio = (HTMLAudioElement)document.createElement("audio");
                _audio.src = _audioUrl;
                _audio.onended = _ => _dirty = true;
            }

            _audio.play();
        }

        public void Stop()
        {
            if (_audio is null) return;
            _audio.pause();
            _audio.currentTime = 0;
            _audio = null;
            _dirty = true;
        }

        private void Toggle()
        {
            if (IsPlaying) _audio.pause();
            else Play();
        }

        private void Frame()
        {
            var ratio  = window.devicePixelRatio > 0 ? window.devicePixelRatio : 1;
            var width  = (int)Math.Round(_root.clientWidth * ratio);
            var height = (int)Math.Round(_root.clientHeight * ratio);
            var cursor = _audio is object ? _audio.currentTime : -1;

            if (_drawnTheme != LauschwerkStyles.ThemeGeneration) _dirty = true;
            if (!_dirty && width == _drawnWidth && height == _drawnHeight && cursor == _drawnCursor) return;
            if (width <= 0 || height <= 0) return;

            _dirty       = false;
            _drawnTheme  = LauschwerkStyles.ThemeGeneration;
            _drawnWidth  = width;
            _drawnHeight = height;
            _drawnCursor = cursor;

            _canvas.width  = (uint)width;
            _canvas.height = (uint)height;

            Draw(width, height, ratio, cursor);
        }

        private void Draw(int width, int height, double ratio, double cursor)
        {
            var ctx = (CanvasRenderingContext2D)_canvas.getContext("2d");
            ctx.clearRect(0, 0, width, height);

            if (_clip is null || _clip.Length == 0)
            {
                ctx.fillStyle = Color.EvalVar(Theme.Secondary.Foreground);
                ctx.font = (12 * ratio) + "px sans-serif";
                ctx.fillText("No recording", 8 * ratio, height / 2.0 + 4 * ratio);
                return;
            }

            var duration = _clip.Duration;
            var primary  = Color.EvalVar(Theme.Primary.Background);
            var ink      = Color.EvalVar(Theme.Default.Foreground);
            var muted    = Color.EvalVar(Theme.Secondary.Foreground);

            // Speech, behind everything.
            ctx.globalAlpha = 0.16;
            ctx.fillStyle = primary;
            foreach (var s in _spans)
            {
                var x0 = s.Start / duration * width;
                var x1 = s.End / duration * width;
                ctx.fillRect(x0, 0, Math.Max(1, x1 - x0), height);
            }
            ctx.globalAlpha = 1;

            // One min/max column per device pixel.
            var samples = _clip.Samples;
            var n = _clip.Length;
            var mid = height / 2.0;
            ctx.fillStyle = muted;

            for (var x = 0; x < width; x++)
            {
                var from = (int)((long)x * n / width);
                var to = Math.Max(from + 1, (int)((long)(x + 1) * n / width));
                float min = 0, max = 0;

                for (var i = from; i < to && i < n; i++)
                {
                    var v = samples[(uint)i];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

                var top = mid - max * mid;
                var bottom = mid - min * mid;
                ctx.fillRect(x, top, 1, Math.Max(ratio, bottom - top));
            }

            // Wakes on top, labelled.
            ctx.strokeStyle = ink;
            ctx.fillStyle = ink;
            ctx.lineWidth = Math.Max(1, ratio);
            ctx.font = (11 * ratio) + "px sans-serif";

            foreach (var w in _wakes)
            {
                var x = w.Start / duration * width;
                ctx.beginPath();
                ctx.moveTo(x, 0);
                ctx.lineTo(x, height);
                ctx.stroke();
                ctx.fillText(w.Word, x + 4 * ratio, 12 * ratio);
            }

            if (cursor >= 0)
            {
                ctx.fillStyle = primary;
                ctx.fillRect(cursor / duration * width, 0, Math.Max(2, 2 * ratio), height);
            }
        }

        public HTMLElement Render() => _root;
    }
}
