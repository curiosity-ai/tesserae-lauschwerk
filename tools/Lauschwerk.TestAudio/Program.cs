using Voxwerk;
using Voxwerk.Audio;
using Voxwerk.Talker;

// The fixtures the sample gallery and the Playwright checks play to lauschwerk's models. Every clip is
// synthesized with two of Qwen3-TTS's built-in speakers - "A" (serena) is the user, "B" (ryan) is
// someone else in the room - with a fixed seed, then laid out on a 16 kHz timeline with silences and
// noise from a seeded generator, so a re-run writes the same files and a test can name the times.
//
//     dotnet run -c Release --project tools/Lauschwerk.TestAudio -- [output folder]
//
// The default output is Tesserae.Lauschwerk.Sample/assets/audio. 16-bit PCM mono: Chromium's
// --use-file-for-fake-audio-capture reads that, and lauschwerk's own decodeWav does too.

const int Rate = 16000;
const string Model = "Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice";
const string UserVoice = "serena";
const string OtherVoice = "ryan";

var output = args.Length > 0 ? args[0] : Path.Combine(FindRepoRoot(), "Tesserae.Lauschwerk.Sample", "assets", "audio");
Directory.CreateDirectory(output);

Console.WriteLine($"Loading {Model} (downloaded once to {Voxwerk.IO.ModelDownloader.DefaultCacheDir})...");
using var tts = await Qwen3Tts.LoadAsync(Model);

var cache = new Dictionary<string, float[]>();

float[] Say(string speaker, string text, int seed)
{
    var key = $"{speaker}|{seed}|{text}";
    if (cache.TryGetValue(key, out var hit)) return hit;
    var result = tts.SynthesizeCustomVoice(text, speaker, "english", options: new GenerationOptions { Seed = seed });
    var pcm = Trim(AudioFile.Resample(result.Audio, result.SampleRate, Rate));
    Console.WriteLine($"  {speaker,-7} {pcm.Length / (double)Rate,5:F2}s  \"{text}\"  (RTF {result.RealTimeFactor:F2})");
    return cache[key] = pcm;
}

var heyNova = new[] { Say(UserVoice, "Hey Nova.", 11), Say(UserVoice, "Hey Nova.", 12), Say(UserVoice, "Hey Nova.", 13) };
var weather = Say(UserVoice, "What is the weather like in Berlin tomorrow?", 21);
var remind = Say(UserVoice, "Please remind me to call my sister when I get home.", 22);
var reading = Say(UserVoice, "The quick brown fox jumps over the lazy dog. I am reading these sentences out loud, so that the system can learn the sound of my voice.", 23);
var verifyA = Say(UserVoice, "This is my voice again, a few days later, asking for the news.", 24);
var meeting = Say(OtherVoice, "I think the meeting moved to three o'clock, let me check the calendar.", 31);
var otherNova = Say(OtherVoice, "Hey Nova.", 32);
var verifyB = Say(OtherVoice, "Good morning everyone, the train was late again today.", 33);

var noise = new Random(7);
float[] Silence(double seconds) => new float[(int)(seconds * Rate)];
float[] Noise(double seconds, double dbfs)
{
    var gain = (float)Math.Pow(10, dbfs / 20) * 1.7f; // uniform white noise: RMS is amplitude / sqrt(3)
    return Enumerable.Range(0, (int)(seconds * Rate)).Select(_ => (float)(noise.NextDouble() * 2 - 1) * gain).ToArray();
}

// wake.wav - a pause, the wake word, then a request: what a wake listener is for. Spot's default
// context ('pause') wants silence before the word, which the 1.5 s lead gives it.
Write("wake.wav", Concat(Silence(1.5), heyNova[0], Silence(0.8), weather, Silence(1.5)));

// speech.wav - two sentences apart, for Cue: two phrases, so two speechstart/speechend pairs.
Write("speech.wav", Concat(Silence(1.0), weather, Silence(1.2), remind, Silence(1.0)));

// mix.wav - the user close by and someone else further away (6 dB down) talking in the user's pause,
// over a noise floor: the before of Focus's before/after. Focus keeps the user's two sentences and
// blanks the other voice, so the difference is visible on a waveform as well as audible.
var userLine = Concat(Silence(0.5), remind, Silence(4.6), weather, Silence(0.5));
var otherAt = (int)((0.5 + remind.Length / (double)Rate + 0.2) * Rate);
Write("mix.wav", Mix(userLine, (Gain(meeting, -6), otherAt), (Noise(userLine.Length / (double)Rate, -38), 0)));

// enroll.wav - several seconds of the user reading, enough for a voiceprint (lauschwerk wants about 6 s).
Write("enroll.wav", Concat(Silence(0.5), reading, Silence(0.5)));

// voice-a.wav / voice-b.wav - one more sentence from each speaker, to verify against the print.
Write("voice-a.wav", Concat(Silence(0.3), verifyA, Silence(0.3)));
Write("voice-b.wav", Concat(Silence(0.3), verifyB, Silence(0.3)));

// wake-takes.wav - the wake word three times with pauses between: what word enrollment listens to.
Write("wake-takes.wav", Concat(Silence(1.5), heyNova[0], Silence(1.8), heyNova[1], Silence(1.8), heyNova[2], Silence(2.0)));

// wake-other.wav - the wake word in the other voice: wakes a plain listener, not an onlyMyVoice one.
Write("wake-other.wav", Concat(Silence(1.5), otherNova, Silence(0.8), meeting, Silence(1.5)));

// noise.wav - nothing but the noise floor: no phrase, no wake.
Write("noise.wav", Noise(4, -38));

Console.WriteLine($"Wrote the fixtures to {output}");

void Write(string name, float[] pcm)
{
    var path = Path.Combine(output, name);
    using var w = new BinaryWriter(File.Create(path));
    w.Write("RIFF"u8); w.Write(36 + pcm.Length * 2); w.Write("WAVE"u8);
    w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
    w.Write("data"u8); w.Write(pcm.Length * 2);
    foreach (var s in pcm) w.Write((short)Math.Round(Math.Clamp(s, -1f, 1f) * 32767));
    Console.WriteLine($"{name,-16} {pcm.Length / (double)Rate,6:F2}s");
}

static float[] Concat(params float[][] parts) => parts.SelectMany(p => p).ToArray();

static float[] Gain(float[] pcm, double db)
{
    var g = (float)Math.Pow(10, db / 20);
    return pcm.Select(s => s * g).ToArray();
}

static float[] Mix(float[] bed, params (float[] Pcm, int At)[] layers)
{
    var length = Math.Max(bed.Length, layers.Max(l => l.At + l.Pcm.Length));
    var outp = new float[length];
    bed.CopyTo(outp, 0);
    foreach (var (pcm, at) in layers)
        for (var i = 0; i < pcm.Length; i++) outp[at + i] += pcm[i];
    return outp;
}

// The synthesizer leaves a little room either side; the layouts above set the silences themselves.
static float[] Trim(float[] pcm)
{
    const float floor = 0.01f;
    var start = Array.FindIndex(pcm, s => Math.Abs(s) > floor);
    var end = Array.FindLastIndex(pcm, s => Math.Abs(s) > floor);
    if (start < 0) return pcm;
    start = Math.Max(0, start - Rate / 20);
    end = Math.Min(pcm.Length - 1, end + Rate / 20);
    return pcm[start..(end + 1)];
}

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (Directory.Exists(Path.Combine(dir.FullName, ".reference"))) return dir.FullName;
    return Directory.GetCurrentDirectory();
}
