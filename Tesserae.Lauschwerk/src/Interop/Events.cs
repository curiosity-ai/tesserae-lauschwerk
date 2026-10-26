using Transpose;
using Transpose.Core;

namespace Tesserae.Lauschwerk
{
    // The payloads lauschwerk's inputs raise. Read-only JavaScript objects the SDK builds, so each is an
    // [External] interface: a direct cast to one emits nothing, and a member the payload lacks reads as
    // undefined. Never `as`/`is` on these - a type test against an [External] interface has no runtime
    // metadata to test and throws instead of answering false.

    /// <summary><c>speechstart</c>, <c>speechend</c> and <c>speechcancel</c>: seconds on the input's clock.</summary>
    [External]
    [Convention(Notation.None)]
    public interface ISpeechEvent
    {
        double start { get; }

        /// <summary>On <c>speechend</c> only.</summary>
        double end { get; }

        /// <summary>On <c>speechend</c>, with the <c>levels</c> (or <c>near</c>) option only.</summary>
        IPhraseLevel level { get; }
    }

    /// <summary>A phrase's levels in dBFS: Focus's output before leveling, the noise floor, the session's main voice.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IPhraseLevel
    {
        double? voiceDb { get; }
        double noiseDb { get; }
        double? refDb { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IModeEventJs
    {
        string mode { get; }
        string reason { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface ILearnEventJs
    {
        double progress { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IMicEventJs
    {
        string state { get; }
    }

    /// <summary>
    /// A diagnostic. The code is an open set - new ones arrive in patch releases - so match on
    /// <see cref="code"/>, never on <see cref="message"/>.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface ILauschwerkDiag
    {
        string code { get; }

        /// <summary><c>"info"</c> or <c>"warn"</c>.</summary>
        string level { get; }

        string message { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IWakeEventJs
    {
        string word { get; }
        double score { get; }
        double threshold { get; }
        string context { get; }
        double start { get; }
        double end { get; }
        es5.Float32Array audio { get; }
        double? voice { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IHeardEventJs
    {
        double speech { get; }
        IWordScoreJs[] scores { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IWordScoreJs
    {
        string word { get; }
        double score { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IUtteranceEventJs
    {
        es5.ArrayBuffer pcm { get; }
        double start { get; }
        double end { get; }
        IPhraseLevel level { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface ITakeJs
    {
        double score { get; }
        bool ok { get; }
        double start { get; }
        double end { get; }
        string pronunciation { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IStatsJs
    {
        double speechSeconds { get; }
        double seconds { get; }
        double? noiseRemovedDb { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IProgressJs
    {
        double loaded { get; }
        double total { get; }
        double done { get; }
    }
}
