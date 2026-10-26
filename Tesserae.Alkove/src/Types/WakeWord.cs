using Transpose;
using Transpose.Core;

namespace Tesserae.Alkove
{
    /// <summary>
    /// A wake word Spot can hear: compiled from typed text (<see cref="AlkoveJs.CompileWordAsync"/>, no
    /// recording needed) or enrolled from the user saying it (<see cref="AlkoveJs.EnrollWord"/>, which
    /// sets its thresholds on their voice).
    ///
    /// Store it with <see cref="ToJson"/>; a stored word is accepted anywhere a word is. A word is bound
    /// to the Spot model that compiled it - after an upgrade that changes the model, using it fails with
    /// <c>word.outdated</c> and its text has to be compiled again.
    /// </summary>
    public sealed class WakeWord
    {
        private readonly IWordJs _js;
        private readonly string  _json;

        internal WakeWord(IWordJs js)
        {
            _js = js;
        }

        private WakeWord(string json, string text)
        {
            _json = json;
            Text  = text;
        }

        /// <summary>The text it was compiled from - the word reported in a wake.</summary>
        public string Text { get => _js is object ? _js.text : _text; private set => _text = value; }
        private string _text;

        /// <summary>The languages it was compiled for (codes: en, fr, es, it, pt, ca, ro).</summary>
        public string[] Languages => _js is object ? _js.lang : new string[0];

        /// <summary>Its pronunciations in IPA, likeliest first.</summary>
        public string[] Pronunciations
        {
            get
            {
                if (_js is null) return new string[0];
                var phonemes = _js.phonemes;
                var ipa = new string[phonemes.Length];
                for (var i = 0; i < phonemes.Length; i++) ipa[i] = phonemes[i].ipa;
                return ipa;
            }
        }

        /// <summary>The log-probability a detection has to beat, after a pause and anywhere.</summary>
        public IThresholds Thresholds => _js?.thresholds;

        /// <summary>True when enrolled (thresholds set on the user's voice), false when typed.</summary>
        public bool Calibrated => _js is object && _js.calibrated;

        /// <summary>
        /// Lex's warnings about the text (<c>text.digits</c>, <c>word.too-short</c>, ...) as
        /// <c>code: message</c>. Not kept by <see cref="ToJson"/>.
        /// </summary>
        public string[] Warnings
        {
            get
            {
                var warnings = _js?.warnings;
                if (warnings is null) return new string[0];
                var lines = new string[warnings.Length];
                for (var i = 0; i < warnings.Length; i++) lines[i] = warnings[i].code + ": " + warnings[i].message;
                return lines;
            }
        }

        /// <summary>alkove's <c>WordJSON</c> as a string - what to store.</summary>
        public string ToJson() => _js is object ? es5.JSON.stringify(_js) : _json;

        /// <summary>
        /// A stored word. It is checked against the bundled Spot when it is used, not here: a word compiled
        /// for another Spot fails then with <c>word.outdated</c>.
        /// </summary>
        public static WakeWord FromJson(string json)
        {
            var parsed = (IWordJs)es5.JSON.parse(json);
            return new WakeWord(json, parsed.text);
        }

        /// <summary>What crosses to alkove: the word object, or the stored JSON it parses itself.</summary>
        internal object ToJs() => _js is object ? (object)_js : _json;

        public override string ToString() => Text;
    }
}
