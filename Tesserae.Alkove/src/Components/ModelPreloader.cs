using System;
using System.Threading.Tasks;
using Transpose;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Alkove
{
    /// <summary>
    /// A settings-screen row that downloads and compiles what a usage needs ahead of time, so the first
    /// input runs its models from its first second. Says whether everything is already cached (the input
    /// can then start offline) and shows the download's progress.
    /// </summary>
    public sealed class ModelPreloader : IComponent
    {
        private readonly AlkoveUsage       _usage;
        private readonly bool              _voiceprint;
        private readonly bool              _typedWords;
        private readonly Button            _button;
        private readonly ProgressIndicator _progress = ProgressIndicator();
        private readonly TextBlock         _status   = TextBlock("").Small().Secondary().Class("tss-alkove-preload-status");
        private readonly IComponent        _content;

        public ModelPreloader(AlkoveUsage usage, bool voiceprint = false, bool typedWords = false)
        {
            _usage      = usage;
            _voiceprint = voiceprint;
            _typedWords = typedWords;
            _button     = Button("Download").SetIcon(UIcons.Download).OnClick(() => PreloadAsync().FireAndForget());

            _content = VStack().WS().Gap(6.px()).Class("tss-alkove-preload").Children(
                HStack().WS().AlignItemsCenter().Gap(8.px()).Children(TextBlock(Describe()).SemiBold().W(1).Grow(), _button),
                _progress.WS(),
                _status);

            DomObserver.WhenMounted(_content.Render(), () => CheckAsync().FireAndForget());
        }

        public event Action Ready;

        private string Describe()
        {
            var models = _usage == AlkoveUsage.Wake ? "Spot" : "Focus and Cue";
            if (_voiceprint) models += ", Timbre";
            if (_typedWords) models += ", Lex";
            return _usage + ": " + models;
        }

        private async Task CheckAsync()
        {
            var cached = await AlkoveJs.IsCachedAsync(_usage, _voiceprint, _typedWords);
            if (cached) Done("Cached: works offline.");
            else _status.Text = "Not downloaded yet.";
        }

        public async Task PreloadAsync()
        {
            _button.Disabled();
            _status.Text = "Downloading...";

            try
            {
                await AlkoveJs.PreloadAsync(_usage, _voiceprint, _typedWords, (loaded, total) =>
                {
                    _progress.Progress((float)(total > 0 ? loaded / total * 100 : 0));
                    _status.Text = Math.Round(loaded / 1e6, 1) + " of " + Math.Round(total / 1e6, 1) + " MB";
                });

                Done("Ready: downloaded, checked and compiled.");
                Ready?.Invoke();
            }
            catch (Exception e)
            {
                _status.Text = "Failed: " + AlkoveException.FromJs(e).Message;
                _button.Disabled(false);
            }
        }

        private void Done(string text)
        {
            _progress.Progress(100);
            _status.Text = text;
            _button.SetText("Ready").SetIcon(UIcons.Check).Disabled();
            _content.Render().setAttribute("data-ready", "true");
        }

        public HTMLElement Render() => _content.Render();
    }
}
