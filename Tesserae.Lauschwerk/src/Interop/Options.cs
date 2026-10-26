using System;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Lauschwerk
{
    // The option objects handed to lauschwerk. [ObjectLiteral] emits only the fields actually assigned, so
    // an option left alone keeps the SDK's own default instead of being overridden by a C# zero or
    // false - which is what lets one type carry every option. A field typed object is one of lauschwerk's
    // unions (a boolean or an object, a print or its JSON). These are internal: the public surface is
    // the typed settings classes in Types/, which write them.

    [ObjectLiteral]
    internal class LauschwerkConfig
    {
        public string modelBaseUrl;
        public string assetBaseUrl;
    }

    [ObjectLiteral]
    internal class UsageOptions
    {
        public bool voiceprint;
        public bool typedWords;
    }

    [ObjectLiteral]
    internal class PreloadOptions
    {
        public bool voiceprint;
        public bool typedWords;
        public Action<IProgressJs> onProgress;
    }

    [ObjectLiteral]
    internal class SpeechParamsJs
    {
        public double confidence;
        public double exitConfidence;
        public double startSecs;
        public double stopSecs;
    }

    [ObjectLiteral]
    internal class NearOptionsJs
    {
        public double gapDb;
    }

    /// <summary>What the three inputs share (<c>CommonOptions</c> and <c>FocusOptions</c>).</summary>
    [ObjectLiteral]
    internal class InputOptionsJs
    {
        public MediaStream stream;
        public MediaStreamTrack track;
        public string deviceId;
        public bool followDefault;
        public bool trace;
        public double keepRecent;

        public object voiceprint;
        public bool learn;
        public object farEnd;
        public double strength;
        public bool normalize;
        public bool stats;
        public SpeechParamsJs speech;
        public bool levels;
        public object near;
    }

    [ObjectLiteral]
    internal class CallInputOptions : InputOptionsJs
    {
        public bool keepAliveWhenHidden;
    }

    [ObjectLiteral]
    internal class AgentOutputJs
    {
        public string format;
        public double rate;
        public double chunkMs;
    }

    [ObjectLiteral]
    internal class AgentInputOptions : InputOptionsJs
    {
        public AgentOutputJs output;
        public bool withTrack;
    }

    [ObjectLiteral]
    internal class WakeListenerOptions : InputOptionsJs
    {
        public object[] words;
        public string context;
        public object onlyMyVoice;
    }

    [ObjectLiteral]
    internal class SpotOptions
    {
        public string context;
        public bool onlyMyVoice;
    }

    [ObjectLiteral]
    internal class CompileOptions
    {
        public string[] lang;
        public string chain;
    }

    [ObjectLiteral]
    internal class EnrollWordOptions
    {
        public string[] lang;
        public double takes;
        public MediaStream stream;
        public string deviceId;
    }

    [ObjectLiteral]
    internal class EmbedOptions
    {
        public string source;
    }

    [ObjectLiteral]
    internal class LevelOptions
    {
        public string level;
    }

    [ObjectLiteral]
    internal class LearnerOptions
    {
        public object voiceprint;
    }

    [ObjectLiteral]
    internal class FocusProcessJs
    {
        public object voiceprint;
        public double strength;
    }

    [ObjectLiteral]
    internal class SpotProcessJs
    {
        public object[] words;
        public string context;
    }

    [ObjectLiteral]
    internal class ProcessOptionsJs
    {
        public object focus;
        public object cue;
        public SpotProcessJs spot;
        public bool normalize;
        public Action<IProgressJs> onProgress;
    }
}
