using System;
using System.Threading.Tasks;
using Transpose;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// Enrolls a wake word: the user says it a few times, each attempt shows as a take (✓ when it was the
    /// word), and at the end the word's thresholds are set on their voice. <see cref="Enrolled"/> gives the
    /// calibrated word - and a voiceprint, from the takes - to hand to a <see cref="WakeWordPanel"/> or a
    /// <see cref="WakeListener"/>.
    /// </summary>
    public sealed class WordEnrollmentPanel : IComponent
    {
        private readonly TextBox    _word;
        private readonly Dropdown   _language;
        private readonly Button     _toggle;
        private readonly Stack      _takes  = HStack().Wrap().Gap(6.px()).Class("tss-lw-takes");
        private readonly TextBlock  _status = TextBlock("").Small().Secondary().Class("tss-lw-enroll-word-status");
        private readonly IComponent _content;
        private readonly int        _count;

        private WordEnrollment _enrollment;
        private MediaStream    _stream;
        private EnrolledWord   _result;

        public WordEnrollmentPanel(string word = "Hey Nova", SpeechLanguage language = SpeechLanguage.English, int takes = 3)
        {
            _count    = takes;
            _word     = TextBox(word).W(220.px());
            _language = LanguagePicker.Create(language);
            _toggle   = Button("Enroll").Primary().SetIcon(UIcons.Microphone).OnClick(() =>
            {
                if (_enrollment is object) Cancel();
                else StartAsync().FireAndForget();
            });

            _content = VStack().WS().Gap(8.px()).Class("tss-lw-enroll-word").Children(
                HStack().WS().Wrap().AlignItemsCenter().Gap(8.px()).Children(_word, _language, _toggle),
                _takes,
                _status);

            _status.Text = "Say the word " + takes + " times, with a pause before each.";
            DomObserver.WhenMounted(_content.Render(), () => DomObserver.WhenRemoved(_content.Render(), Cancel));
        }

        public EnrolledWord Result => _result;

        public event Action<EnrolledWord> Enrolled;

        public WordEnrollmentPanel OnEnrolled(Action<EnrolledWord> handler)
        {
            Enrolled += handler;
            return this;
        }

        public async Task StartAsync()
        {
            if (_enrollment is object) return;

            _takes.Clear();
            _result = null;
            _toggle.SetText("Cancel");
            _status.Text = "Loading the models...";

            try
            {
                _stream = await Microphone.OpenAsync();
                _enrollment = await LauschwerkJs.EnrollWordAsync(_word.Text, LanguagePicker.Read(_language), _count, new CallInputSettings { Stream = _stream });

                var good = 0;
                _enrollment.Take += t =>
                {
                    if (t.Ok) good++;
                    _takes.Add(Badge((t.Ok ? "✓ " : "✗ ") + t.Score.ToString("0.00")).Tone(t.Ok ? BadgeTone.Success : BadgeTone.Neutral).Tooltip(t.Ok ? "Counted: /" + t.Pronunciation + "/" : "Not the word - counted as other speech"));
                    _status.Text = good + " of " + _count + " takes.";
                };
                _enrollment.Warning += (code, message) => _takes.Add(Badge(code).Warning().Tooltip(message));

                _status.Text = "Say \"" + _word.Text + "\" now.";
                _result = await _enrollment.DoneAsync();

                var t0 = _result.Word.Thresholds;
                _status.Text = "Enrolled: thresholds " + t0.pause.ToString("0.00") + " after a pause, " + t0.anywhere.ToString("0.00") + " anywhere" + (_result.Voiceprint is object ? ", with a voiceprint." : ".");
                _content.Render().setAttribute("data-enrolled", "true");
                Enrolled?.Invoke(_result);
            }
            catch (Exception e)
            {
                var error = LauschwerkException.FromJs(e);
                _status.Text = error.IsAbort ? "Cancelled." : (error.Code is object && error.Code.StartsWith("mic") ? VoiceInputPanel.Explain(error) : error.Message);
            }
            finally
            {
                _enrollment = null;
                Microphone.Stop(_stream);
                _stream = null;
                _toggle.SetText("Enroll");
            }
        }

        public void Cancel()
        {
            _enrollment?.Cancel();
        }

        public HTMLElement Render() => _content.Render();
    }
}
