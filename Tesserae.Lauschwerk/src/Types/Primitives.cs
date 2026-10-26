using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// Timbre, the voiceprint model: makes prints from recordings and compares them. Runs in the SDK's
    /// background worker; <see cref="CreateAsync"/> loads the model (0.7 MB) the first time.
    /// </summary>
    public sealed class Timbre
    {
        private readonly ITimbreJs _js;

        private Timbre(ITimbreJs js)
        {
            _js = js;
        }

        public static async Task<Timbre> CreateAsync()
        {
            await LauschwerkJs.LoadAsync();

            try
            {
                return await PromiseHelper.ToTask(LauschwerkLib.Timbre.create(), t => new Timbre((ITimbreJs)t));
            }
            catch (Exception e)
            {
                throw LauschwerkException.FromJs(e);
            }
        }

        /// <summary>A print from 16 kHz audio - 1.5 s at least, more is better.</summary>
        public Task<Voiceprint> EmbedAsync(AudioClip clip, bool behindFocus = false)
            => PromiseHelper.ToTask(_js.embed(clip.Samples, new EmbedOptions { source = behindFocus ? "focus" : "raw" }), p => new Voiceprint((IVoiceprintJs)p));

        /// <summary>Whether two prints are the same voice, and how alike they are.</summary>
        public VerifyResult Verify(Voiceprint a, Voiceprint b, VerifyLevel level = VerifyLevel.Balanced)
        {
            var r = _js.verify(a.Js, b.Js, new LevelOptions { level = EnumNames.Level(level) });
            return new VerifyResult(r.same, r.score, null);
        }

        /// <summary>Which of <paramref name="known"/> this voice is, or null when none passes the level.</summary>
        public async Task<VerifyResult> IdentifyAsync(Voiceprint voice, Dictionary<string, Voiceprint> known, VerifyLevel level = VerifyLevel.Balanced)
        {
            var record = new object();
            foreach (var kv in known) Script.Set(record, kv.Key, kv.Value.Js);

            var r = await PromiseHelper.ToTask<IVerifyResultJs>(_js.identify(voice.Js, record, new LevelOptions { level = EnumNames.Level(level) }));
            return r is null ? null : new VerifyResult(true, r.score, r.who);
        }
    }

    /// <summary>The answer of <see cref="Timbre.Verify"/> or <see cref="Timbre.IdentifyAsync"/>.</summary>
    public sealed class VerifyResult
    {
        internal VerifyResult(bool same, double score, string who)
        {
            Same  = same;
            Score = score;
            Who   = who;
        }

        public bool Same { get; }

        /// <summary>The similarity, -1 to 1.</summary>
        public double Score { get; }

        /// <summary>The key of the matching print, from <see cref="Timbre.IdentifyAsync"/>.</summary>
        public string Who { get; }
    }

    /// <summary>
    /// Learns a print from audio the host feeds it - an enrollment screen reading back a recording, or
    /// audio from somewhere other than a live input. The same rules as an input's <c>Learn</c>.
    /// </summary>
    public sealed class VoiceprintLearner
    {
        private readonly IVoiceprintLearnerJs _js;

        private VoiceprintLearner(IVoiceprintLearnerJs js)
        {
            _js = js;
        }

        /// <summary>A learner, starting from <paramref name="start"/> when given.</summary>
        public static async Task<VoiceprintLearner> CreateAsync(Voiceprint start = null)
        {
            await LauschwerkJs.LoadAsync();
            var options = new LearnerOptions();
            if (start is object) options.voiceprint = start.Js;

            try
            {
                return await PromiseHelper.ToTask(LauschwerkLib.VoiceprintLearner.create(options), l => new VoiceprintLearner((IVoiceprintLearnerJs)l));
            }
            catch (Exception e)
            {
                throw LauschwerkException.FromJs(e);
            }
        }

        /// <summary>0 to 1; ready at 1 (about 6 s of voice).</summary>
        public double Progress => _js.progress;

        public bool IsReady => _js.ready;

        public Voiceprint Voiceprint => Voiceprint.Wrap(_js.voiceprint);

        /// <summary>Offers 16 kHz audio; windows unlike the print so far are rejected.</summary>
        public async Task<LearnerOffer> OfferAsync(AudioClip clip)
        {
            var r = await PromiseHelper.ToTask<IOfferResultJs>(_js.offer(clip.Samples));
            return new LearnerOffer(r.accepted, r.rejected, r.progress, r.ready);
        }

        public void Forget() => _js.forget();
    }

    public sealed class LearnerOffer
    {
        internal LearnerOffer(int accepted, int rejected, double progress, bool ready)
        {
            Accepted = accepted;
            Rejected = rejected;
            Progress = progress;
            IsReady  = ready;
        }

        public int Accepted { get; }
        public int Rejected { get; }
        public double Progress { get; }
        public bool IsReady { get; }
    }

    /// <summary>Lex: written words to their likely pronunciations, in seven languages.</summary>
    public sealed class Lex
    {
        private readonly ILexJs _js;

        private Lex(ILexJs js)
        {
            _js = js;
        }

        public static async Task<Lex> CreateAsync()
        {
            await LauschwerkJs.LoadAsync();

            try
            {
                return await PromiseHelper.ToTask(LauschwerkLib.Lex.create(), l => new Lex((ILexJs)l));
            }
            catch (Exception e)
            {
                throw LauschwerkException.FromJs(e);
            }
        }

        /// <summary>Up to three pronunciations of <paramref name="text"/>, likeliest first.</summary>
        public async Task<Pronunciation[]> PhonemizeAsync(string text, SpeechLanguage language = SpeechLanguage.English)
        {
            var result = await PromiseHelper.ToTask<object>(_js.phonemize(text, EnumNames.Code(language)));
            var list = (IPhonemeJs[])result;
            var output = new Pronunciation[list.Length];
            for (var i = 0; i < list.Length; i++) output[i] = new Pronunciation(list[i].ipa, list[i].p);
            return output;
        }

        /// <summary>How different two IPA strings sound, 0 (same) to 1.</summary>
        public static double Distance(string ipaA, string ipaB) => LauschwerkLib.Lex.distance("/" + ipaA.Trim('/') + "/", "/" + ipaB.Trim('/') + "/");
    }

    public sealed class Pronunciation
    {
        internal Pronunciation(string ipa, double probability)
        {
            Ipa         = ipa;
            Probability = probability;
        }

        public string Ipa { get; }
        public double Probability { get; }
        public override string ToString() => "/" + Ipa + "/ " + Math.Round(Probability * 100) + "%";
    }

    /// <summary>
    /// A wake word being enrolled: the user says it a few times. Each attempt raises <see cref="Take"/>;
    /// <see cref="DoneAsync"/> gives the calibrated word and a voiceprint.
    /// </summary>
    public sealed class WordEnrollment
    {
        private readonly IEnrollmentJs _js;
        private readonly Action        _offTake;
        private readonly Action        _offWarning;

        internal WordEnrollment(IEnrollmentJs js)
        {
            _js         = js;
            _offTake    = js.on("take", e =>
            {
                var t = (ITakeJs)e;
                Take?.Invoke(new EnrollTake(t.score, t.ok, t.start, t.end, t.pronunciation));
            });
            _offWarning = js.on("warning", e =>
            {
                var w = (IWarningJs)e;
                Warning?.Invoke(w.code, w.message);
            });
        }

        /// <summary>One segment of speech: whether it was the word, and how clearly.</summary>
        public event Action<EnrollTake> Take;

        /// <summary>A warning about the word or the takes (<c>word.takes-differ</c>, <c>word.no-margin</c>, ...).</summary>
        public event Action<string, string> Warning;

        /// <summary>The calibrated word and the voiceprint from the takes (null when they were too short).</summary>
        public async Task<EnrolledWord> DoneAsync()
        {
            try
            {
                var result = await PromiseHelper.ToTask<IEnrollmentResultJs>(_js.done);
                return new EnrolledWord(new WakeWord(result.word), Voiceprint.Wrap(result.voiceprint));
            }
            catch (Exception e)
            {
                throw LauschwerkException.FromJs(e);
            }
            finally
            {
                _offTake();
                _offWarning();
            }
        }

        /// <summary>Stops the microphone; <see cref="DoneAsync"/> fails with an abort.</summary>
        public void Cancel() => _js.cancel();
    }

    public sealed class EnrollTake
    {
        internal EnrollTake(double score, bool ok, double start, double end, string pronunciation)
        {
            Score         = score;
            Ok            = ok;
            Start         = start;
            End           = end;
            Pronunciation = pronunciation;
        }

        public double Score { get; }

        /// <summary>True when it counts as the word.</summary>
        public bool Ok { get; }

        public double Start { get; }
        public double End { get; }
        public string Pronunciation { get; }
    }

    public sealed class EnrolledWord
    {
        internal EnrolledWord(WakeWord word, Voiceprint voiceprint)
        {
            Word       = word;
            Voiceprint = voiceprint;
        }

        public WakeWord Word { get; }
        public Voiceprint Voiceprint { get; }
    }
}
