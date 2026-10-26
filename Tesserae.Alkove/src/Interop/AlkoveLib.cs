using System;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Alkove
{
    /// <summary>
    /// The global <c>alkove</c> object - the SDK's module namespace, published on <c>window</c> by the
    /// entry build/bundle-alkove.mjs writes - declared to the compiler instead of reached through
    /// <c>Script.Write</c>. Nothing is emitted for an <c>[External]</c> declaration: a call site compiles
    /// straight to the JavaScript it names, and a typo is a build error rather than a runtime one.
    ///
    /// <c>[Convention(Notation.None)]</c> keeps the C# names identical to the JavaScript ones; without it
    /// the compiler camel-cases members and <c>alkove.Alkove</c> would be emitted as <c>alkove.alkove</c>.
    ///
    /// Only reachable once <see cref="AlkoveJs.LoadAsync"/> has finished: every wrapper awaits it first.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("alkove")]
    internal static class AlkoveLib
    {
        /// <summary>The bundled alkove version, written into the entry by the bundle script.</summary>
        public static extern string version { get; }

        /// <summary><c>sdk</c> when built from this repository's sdk/, <c>npm</c> when staged from the release.</summary>
        public static extern string source { get; }

        /// <summary>The sha256 of the bundled alkove-engine.wasm.</summary>
        public static extern string engineSha256 { get; }

        /// <summary>The folder the SDK loads its worker, worklet and engine from.</summary>
        public static extern string assetBaseUrl { get; }

        public static extern IAlkoveApi Alkove { get; }
        public static extern ISpotApi Spot { get; }
        public static extern ITimbreApi Timbre { get; }
        public static extern ILexApi Lex { get; }
        public static extern IVoiceprintApi Voiceprint { get; }
        public static extern IVoiceprintLearnerApi VoiceprintLearner { get; }

        /// <summary>Resolves to an <see cref="IAlkoveInputJs"/>.</summary>
        public static extern IPromise createCallInput(CallInputOptions options);

        /// <summary>Resolves to an <see cref="IAlkoveInputJs"/>.</summary>
        public static extern IPromise createAgentInput(AgentInputOptions options);

        /// <summary>Resolves to an <see cref="IAlkoveInputJs"/>.</summary>
        public static extern IPromise createWakeListener(WakeListenerOptions options);
    }

    /// <summary><c>window.alkove</c>, for asking whether the SDK has loaded yet without a ReferenceError.</summary>
    [External]
    [Convention(Notation.None)]
    [Name("window")]
    internal static class AlkoveWindow
    {
        public static extern object alkove { get; }
    }

    /// <summary>The <c>Alkove</c> object of static functions.</summary>
    [External]
    [Convention(Notation.None)]
    internal interface IAlkoveApi
    {
        void configure(AlkoveConfig config);
        IAlkoveSupport supported();

        /// <summary>Resolves to <c>'granted' | 'denied' | 'prompt'</c>.</summary>
        IPromise micPermission();

        /// <summary>Returns its own unsubscribe function.</summary>
        Action onDiag(Action<IAlkoveDiag> handler);

        IPromise preload(string usage, PreloadOptions options);

        /// <summary>Resolves to a boolean.</summary>
        IPromise cached(string usage, UsageOptions options);

        /// <summary>
        /// Resolves to an <see cref="IProcessResultJs"/>. <paramref name="input"/> is a <c>Blob</c>, an
        /// <c>es5.ArrayBuffer</c>, a <c>es5.Uint8Array</c>, an <c>AudioBuffer</c> or <c>{ pcm, rate }</c>.
        /// </summary>
        IPromise process(object input, ProcessOptionsJs options);
    }

    [External]
    [Convention(Notation.None)]
    internal interface IAlkoveSupport
    {
        bool worklet { get; }
        bool simd { get; }
        bool secure { get; }
    }

    /// <summary>
    /// One of the three inputs (call, agent, wake listener) - one class in the SDK, so one declaration
    /// here; what each kind does not have is simply null or a no-op there.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    internal interface IAlkoveInputJs
    {
        string mode { get; }
        string status { get; }
        double latency { get; }
        IVoiceprintJs voiceprint { get; }

        /// <summary>A getter that answers a new promise each time.</summary>
        IPromise ready { get; }

        MediaStreamTrack track { get; }

        /// <summary>Subscribes to one event; returns the unsubscribe function.</summary>
        Action on(string type, Action<object> handler);

        double now();
        IPromise setEnabled(bool on);
        void listen(bool on);
        void hold();
        void release();
        void setStrength(double strength);
        void setVoiceprint(object print);
        void forgetVoiceprint();
        void farEndActive(bool on);
        IPromise stats();
        IPromise record(double seconds);
        IPromise recent();
        IPromise resume();
        void bind(RTCRtpSender sender);
        IPromise setDevice(string deviceId);
        ISpotHandleJs spot(object[] words, SpotOptions options);
        IEnrollmentJs enrollWord(string text, EnrollWordOptions options);
        void dispose();
    }

    [External]
    [Convention(Notation.None)]
    internal interface ISpotHandleJs
    {
        Action on(string type, Action<object> handler);
        void dispose();
    }

    [External]
    [Convention(Notation.None)]
    internal interface IEnrollmentJs
    {
        Action on(string type, Action<object> handler);

        /// <summary>Resolves to <c>{ word, voiceprint }</c>.</summary>
        IPromise done { get; }

        void cancel();
    }

    [External]
    [Convention(Notation.None)]
    internal interface IEnrollmentResultJs
    {
        IWordJs word { get; }
        IVoiceprintJs voiceprint { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface ISpotApi
    {
        /// <summary>Resolves to an <see cref="IWordJs"/>.</summary>
        IPromise compile(string text, CompileOptions options);

        IEnrollmentJs enroll(string text, EnrollWordOptions options);
    }

    [External]
    [Convention(Notation.None)]
    internal interface IWordJs
    {
        string text { get; }
        string[] lang { get; }
        IPhonemeJs[] phonemes { get; }
        IThresholds thresholds { get; }
        bool calibrated { get; }
        string chain { get; }
        IWarningJs[] warnings { get; }

        /// <summary>The plain <c>WordJSON</c> object.</summary>
        object toJSON();
    }

    [External]
    [Convention(Notation.None)]
    internal interface IPhonemeJs
    {
        string ipa { get; }
        string lang { get; }
        double p { get; }
    }

    [External]
    [Convention(Notation.None)]
    public interface IThresholds
    {
        double anywhere { get; }
        double pause { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IWarningJs
    {
        string code { get; }
        string message { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IVoiceprintApi
    {
        /// <summary>Null when the print was made by another Timbre model. Throws on something that is not a print.</summary>
        IVoiceprintJs fromJSON(object json);

        IVoiceprintJs merge(IVoiceprintJs[] prints);
    }

    [External]
    [Convention(Notation.None)]
    internal interface IVoiceprintJs
    {
        string source { get; }
        string model { get; }
        IVoiceprintCenterJs[] centers { get; }
        double similarity(IVoiceprintJs other);

        /// <summary>The plain <c>VoiceprintJSON</c> object.</summary>
        object toJSON();
    }

    [External]
    [Convention(Notation.None)]
    internal interface IVoiceprintCenterJs
    {
        double seconds { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface ITimbreApi
    {
        /// <summary>Resolves to an <see cref="ITimbreJs"/>.</summary>
        IPromise create();
    }

    [External]
    [Convention(Notation.None)]
    internal interface ITimbreJs
    {
        /// <summary>Resolves to an <see cref="IVoiceprintJs"/>.</summary>
        IPromise embed(es5.Float32Array pcm, EmbedOptions options);

        IVerifyResultJs verify(IVoiceprintJs a, IVoiceprintJs b, LevelOptions options);

        /// <summary>Resolves to <c>{ who, score }</c> or null.</summary>
        IPromise identify(object voice, object known, LevelOptions options);
    }

    [External]
    [Convention(Notation.None)]
    internal interface IVerifyResultJs
    {
        bool same { get; }
        double score { get; }
        string who { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface ILexApi
    {
        string[] langs { get; }

        /// <summary>Resolves to an <see cref="ILexJs"/>.</summary>
        IPromise create();

        double distance(string a, string b);
    }

    [External]
    [Convention(Notation.None)]
    internal interface ILexJs
    {
        /// <summary>Resolves to an array of <see cref="IPhonemeJs"/> (<c>{ ipa, p }</c>).</summary>
        IPromise phonemize(string text, string lang);
    }

    [External]
    [Convention(Notation.None)]
    internal interface IVoiceprintLearnerApi
    {
        /// <summary>Resolves to an <see cref="IVoiceprintLearnerJs"/>.</summary>
        IPromise create(LearnerOptions options);
    }

    [External]
    [Convention(Notation.None)]
    internal interface IVoiceprintLearnerJs
    {
        double progress { get; }
        bool ready { get; }
        IVoiceprintJs voiceprint { get; }

        /// <summary>Resolves to <c>{ accepted, rejected, progress, ready }</c>.</summary>
        IPromise offer(es5.Float32Array pcm);

        void forget();
    }

    [External]
    [Convention(Notation.None)]
    internal interface IOfferResultJs
    {
        int accepted { get; }
        int rejected { get; }
        double progress { get; }
        bool ready { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IProcessResultJs
    {
        es5.Float32Array audio { get; }
        es5.Float32Array raw { get; }
        ISpanJs[] speech { get; }
        IWakeEventJs[] wakes { get; }

        /// <summary>A 16 kHz 16-bit WAV: <c>'audio'</c> (default), <c>'raw'</c>, or <c>'both'</c> (stereo, input left).</summary>
        es5.Uint8Array toWav(string which);
    }

    [External]
    [Convention(Notation.None)]
    internal interface ISpanJs
    {
        double start { get; }
        double end { get; }
    }

    /// <summary>The <c>{ pcm, rate }</c> shape <c>Alkove.process</c> takes for samples already decoded.</summary>
    [ObjectLiteral]
    internal class PcmInput
    {
        public es5.Float32Array pcm;
        public double rate;
    }
}
