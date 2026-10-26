using System;
using Transpose;
using Transpose.Core;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// A rejected JavaScript promise does not arrive as the value it rejected with: the runtime wraps it
    /// in a PromiseException whose <c>arguments</c> carry the real rejection. Declared by shape, because
    /// the cast to it is erased - reading <c>arguments</c> off something that is not a wrapper gives
    /// undefined.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    internal interface IPromiseRejection
    {
        object[] arguments { get; }
    }

    [External]
    [Convention(Notation.None)]
    internal interface IJsError
    {
        string name { get; }

        /// <summary>A string on an LauschwerkError - but a number on a DOMException (a DataCloneError is 25), so read it through <see cref="LauschwerkException.FromJs"/>.</summary>
        object code { get; }

        string message { get; }
    }

    /// <summary>
    /// An lauschwerk failure with its <see cref="Code"/>. Creating an input only fails when there can be no
    /// sound at all (<c>mic.denied</c>, <c>mic.not-found</c>, <c>mic.busy</c>, <c>page.insecure</c>,
    /// <c>browser.unsupported</c>); models, network and slow devices never fail an input, they make it
    /// raw (see <see cref="VoiceInput.Mode"/>). Files, words, voiceprints and preloads can also fail with
    /// <c>models.fetch</c>, <c>models.hash</c>, <c>engine.failed</c>, <c>lang.unsupported</c> and
    /// <c>word.outdated</c>. An aborted call carries <see cref="Name"/> <c>AbortError</c> and no code.
    /// </summary>
    public sealed class LauschwerkException : Exception
    {
        public const string MIC_DENIED          = "mic.denied";
        public const string MIC_NOT_FOUND       = "mic.not-found";
        public const string MIC_BUSY            = "mic.busy";
        public const string PAGE_INSECURE       = "page.insecure";
        public const string BROWSER_UNSUPPORTED = "browser.unsupported";
        public const string MODELS_FETCH        = "models.fetch";
        public const string MODELS_HASH         = "models.hash";
        public const string ENGINE_FAILED       = "engine.failed";
        public const string LANG_UNSUPPORTED    = "lang.unsupported";
        public const string WORD_OUTDATED       = "word.outdated";

        internal LauschwerkException(string code, string name, string message) : base(string.IsNullOrEmpty(message) ? (code ?? name ?? "lauschwerk error") : message)
        {
            Code = code;
            Name = name;
        }

        /// <summary>lauschwerk's error code, or null when the failure was not an <c>LauschwerkError</c>.</summary>
        public string Code { get; }

        /// <summary>The JavaScript error's name: <c>LauschwerkError</c>, <c>AbortError</c>, <c>TypeError</c>...</summary>
        public string Name { get; }

        /// <summary>True when the user refused the microphone, or a policy did.</summary>
        public bool IsMicDenied => Code == MIC_DENIED;

        /// <summary>True for a cancelled enrollment or an aborted call.</summary>
        public bool IsAbort => Name == "AbortError";

        internal static LauschwerkException FromJs(object error)
        {
            if (error is LauschwerkException already) return already;

            var rejection = ((IPromiseRejection)error)?.arguments;
            if (rejection is object && rejection.Length > 0) return FromJs(rejection[0]);

            var js = (IJsError)error;
            if (js is null) return new LauschwerkException(null, null, "lauschwerk error");

            string message = js.message;
            if (string.IsNullOrEmpty(message) && error is Exception e) message = e.Message;

            // Only lauschwerk's own codes are strings; a DOMException's legacy numeric code means nothing here.
            var code = Script.TypeOf(js.code) == "string" ? (string)js.code : null;

            return new LauschwerkException(code, js.name, message);
        }
    }
}
