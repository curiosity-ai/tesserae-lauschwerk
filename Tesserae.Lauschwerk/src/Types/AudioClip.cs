using System;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// Mono audio as lauschwerk hands it over: float samples at 16 kHz unless said otherwise. Wraps the
    /// <c>Float32Array</c> without copying it.
    /// </summary>
    public sealed class AudioClip
    {
        /// <summary>The rate everything lauschwerk returns is at.</summary>
        public const int LAUSCHWERK_RATE = 16000;

        public AudioClip(es5.Float32Array samples, int sampleRate = LAUSCHWERK_RATE)
        {
            Samples    = samples;
            SampleRate = sampleRate;
        }

        public es5.Float32Array Samples { get; }
        public int SampleRate { get; }
        public int Length => (int)Samples.length;
        public double Duration => Samples.length / (double)SampleRate;

        /// <summary>The loudest moment, in dBFS (−∞ for silence).</summary>
        public double PeakDb
        {
            get
            {
                double peak = 0;
                var n = (int)Samples.length;
                for (var i = 0; i < n; i++)
                {
                    var v = Math.Abs(Samples[(uint)i]);
                    if (v > peak) peak = v;
                }
                return 20 * Math.Log10(peak);
            }
        }

        /// <summary>Samples <paramref name="from"/> to <paramref name="to"/> seconds, sharing the buffer.</summary>
        public AudioClip Slice(double from, double to)
        {
            var a = (uint)Math.Max(0, Math.Min(Length, (int)Math.Round(from * SampleRate)));
            var b = (uint)Math.Max(a, Math.Min(Length, (int)Math.Round(to * SampleRate)));
            return new AudioClip(Samples.subarray(a, (int)b), SampleRate);
        }

        /// <summary>A 16-bit PCM WAV of the clip.</summary>
        public es5.Uint8Array ToWavBytes() => Wav.Encode(Samples, SampleRate);

        /// <summary>A 16-bit PCM WAV of the clip, as a Blob - for an &lt;audio&gt; element or a download.</summary>
        public Blob ToWav() => Wav.ToBlob(ToWavBytes());

        /// <summary>From an agent input's <c>Float32</c> audio packet or utterance.</summary>
        public static AudioClip FromFloat32(es5.ArrayBuffer buffer, int sampleRate = LAUSCHWERK_RATE) => new AudioClip(new es5.Float32Array(buffer), sampleRate);
    }

    internal static class Wav
    {
        internal static es5.Uint8Array Encode(es5.Float32Array samples, int rate)
        {
            var n = (int)samples.length;
            var bytes = new es5.Uint8Array((uint)(44 + n * 2));
            var view = new es5.DataView(bytes.buffer);

            void Ascii(uint at, string text)
            {
                for (var i = 0; i < text.Length; i++) view.setUint8(at + (uint)i, (byte)text[i]);
            }

            Ascii(0, "RIFF");
            view.setUint32(4, (uint)(36 + n * 2), true);
            Ascii(8, "WAVE");
            Ascii(12, "fmt ");
            view.setUint32(16, 16, true);
            view.setUint16(20, 1, true);
            view.setUint16(22, 1, true);
            view.setUint32(24, (uint)rate, true);
            view.setUint32(28, (uint)(rate * 2), true);
            view.setUint16(32, 2, true);
            view.setUint16(34, 16, true);
            Ascii(36, "data");
            view.setUint32(40, (uint)(n * 2), true);

            for (var i = 0; i < n; i++)
            {
                var s = Math.Max(-1, Math.Min(1, samples[(uint)i]));
                view.setInt16((uint)(44 + i * 2), (short)Math.Round(s * 32767), true);
            }

            return bytes;
        }

        internal static Blob ToBlob(es5.Uint8Array bytes) => new Blob(new object[] { bytes }, new BlobPropertyBag { type = "audio/wav" });
    }
}
