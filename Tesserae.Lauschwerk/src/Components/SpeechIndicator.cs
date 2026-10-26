using System;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// A dot that lights while someone speaks, from an input's Cue events (<see cref="VoiceInput.IsSpeaking"/>),
    /// with the phrase count beside it. <see cref="Bind"/> it to an input; a new input replaces the old one.
    /// </summary>
    public sealed class SpeechIndicator : IComponent
    {
        private readonly HTMLElement _root;
        private readonly HTMLElement _text;
        private VoiceInput _input;
        private int        _phrases;

        public SpeechIndicator()
        {
            LauschwerkStyles.Ensure();
            _root = Span(Att("tss-lw-speaking"));
            _root.setAttribute("aria-live", "polite");
            _root.appendChild(Span(Att("tss-lw-dot")));
            _text = Span(Att());
            _root.appendChild(_text);
            Update();
        }

        /// <summary>Phrases heard since the input was bound.</summary>
        public int Phrases => _phrases;

        public bool IsSpeaking => _input is object && _input.IsSpeaking;

        public SpeechIndicator Bind(VoiceInput input)
        {
            if (_input is object)
            {
                _input.Changed     -= Update;
                _input.SpeechEnded -= Counted;
            }

            _input   = input;
            _phrases = 0;

            if (input is object)
            {
                input.Changed     += Update;
                input.SpeechEnded += Counted;
            }

            Update();
            return this;
        }

        private void Counted(ISpeechEvent e)
        {
            _phrases++;
            Update();
        }

        private void Update()
        {
            var on = IsSpeaking;
            _root.classList.toggle("tss-lw-on", on);
            _root.setAttribute("data-speaking", on ? "true" : "false");
            _text.textContent = (on ? "Speaking" : "Silent") + (_phrases > 0 ? " · " + _phrases + (_phrases == 1 ? " phrase" : " phrases") : "");
        }

        public HTMLElement Render() => _root;
    }

    /// <summary>
    /// The input's <see cref="VoiceInput.Mode"/> as a badge - neutral while raw (with the reason in its
    /// tooltip), primary while the models run. <see cref="Bind"/> it to an input.
    /// </summary>
    public sealed class ModeBadge : IComponent
    {
        private readonly Badge _badge = Badge("off").Outline();
        private VoiceInput _input;

        public ModeBadge()
        {
            _badge.Class("tss-lw-mode");
        }

        public ModeBadge Bind(VoiceInput input)
        {
            if (_input is object) _input.Changed -= Update;
            _input = input;
            if (input is object) input.Changed += Update;
            Update();
            return this;
        }

        private void Update()
        {
            var element = _badge.Render();

            if (_input is null || _input.IsDisposed)
            {
                _badge.SetText("off").Neutral().Outline();
                element.setAttribute("data-mode", "off");
                return;
            }

            var mode = _input.Mode;
            var text = EnumNames.Describe(mode);

            if (mode == VoiceMode.Raw && _input.RawReason != RawReason.None) text += " (" + _input.RawReason.ToString().ToLower() + ")";

            _badge.SetText(text);
            element.setAttribute("data-mode", text);

            if (mode == VoiceMode.Raw) _badge.Neutral().Outline();
            else _badge.Primary().Filled();
        }

        public HTMLElement Render() => _badge.Render();
    }
}
