using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// The few rules the components need that Tesserae's own components do not already give them - the
    /// meter's track and fill, the speaking dot, the waveform's frame - injected once as a &lt;style&gt;
    /// the first time a component renders. Every colour is a <c>--tss-*</c> token, so the components
    /// follow light and dark mode and the custom themes without rules of their own; the three values
    /// with no token are declared as this sheet's own variables (<c>--tss-lw-*</c>) so a host or a
    /// theme can restate them.
    ///
    /// A rule is chained to its component's root class, so a theme's single-class rule never has to fight
    /// a two-class one here.
    /// </summary>
    internal static class LauschwerkStyles
    {
        private const string ID = "tss-lw-styles";

        private static bool _watchingTheme;

        /// <summary>
        /// Bumped on every Tesserae theme change. A component that draws with resolved colours (the
        /// waveform's canvas) compares it each frame instead of subscribing to the static
        /// <c>Theme.OnThemeChanged</c> itself, which would hold every component ever built for the life
        /// of the page.
        /// </summary>
        internal static int ThemeGeneration { get; private set; }

        internal static void Ensure()
        {
            if (!_watchingTheme)
            {
                _watchingTheme = true;
                Theme.OnThemeChanged += () => ThemeGeneration++;
            }

            if (document.getElementById(ID) is object) return;

            var style = (HTMLStyleElement)document.createElement("style");
            style.id = ID;
            style.textContent = CSS;
            document.head.appendChild(style);
        }

        private const string CSS = @"
:root {
  --tss-lw-meter-track: rgba(127, 127, 127, 0.18);
  --tss-lw-meter-hot: var(--tss-danger-background-color, #d13438);
  --tss-lw-speech: var(--tss-success-background-color, #107c10);
}
.tss-lw-meter { display: flex; align-items: center; gap: 8px; min-width: 0; }
.tss-lw-meter .tss-lw-meter-track { position: relative; flex: 1 1 auto; min-width: 40px; height: 8px; border-radius: 4px; background: var(--tss-lw-meter-track); overflow: hidden; }
.tss-lw-meter .tss-lw-meter-fill { position: absolute; left: 0; top: 0; bottom: 0; width: 0; border-radius: 4px; background: var(--tss-primary-background-color); transition: width 60ms linear; }
.tss-lw-meter .tss-lw-meter-peak { position: absolute; top: 0; bottom: 0; width: 2px; background: var(--tss-default-foreground-color); opacity: 0.5; }
.tss-lw-meter.tss-lw-meter-hot .tss-lw-meter-fill { background: var(--tss-lw-meter-hot); }
.tss-lw-meter .tss-lw-meter-label { flex: 0 0 auto; min-width: 64px; font-size: 12px; color: var(--tss-secondary-foreground-color); }
.tss-lw-meter .tss-lw-meter-value { flex: 0 0 auto; min-width: 56px; text-align: right; font-size: 12px; font-variant-numeric: tabular-nums; color: var(--tss-secondary-foreground-color); }
.tss-lw-speaking { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; }
.tss-lw-speaking .tss-lw-dot { width: 10px; height: 10px; border-radius: 50%; background: var(--tss-lw-meter-track); transition: background 120ms, box-shadow 120ms; }
.tss-lw-speaking.tss-lw-on .tss-lw-dot { background: var(--tss-lw-speech); box-shadow: 0 0 0 4px color-mix(in srgb, var(--tss-lw-speech) 25%, transparent); }
.tss-lw-waveform { position: relative; width: 100%; height: 96px; border: 1px solid var(--tss-default-border-color); border-radius: 6px; background: var(--tss-default-background-color); overflow: hidden; }
.tss-lw-waveform canvas { position: absolute; inset: 0; width: 100%; height: 100%; display: block; }
.tss-lw-live { position: relative; width: 100%; height: 96px; border: 1px solid var(--tss-default-border-color); border-radius: 6px; overflow: hidden; }
.tss-lw-live.tss-lw-live-frameless { border-color: transparent; border-radius: 0; }
.tss-lw-live canvas { position: absolute; inset: 0; width: 100%; height: 100%; display: block; }
.tss-lw-takes { display: flex; gap: 6px; flex-wrap: wrap; }
@media (prefers-reduced-motion: reduce) {
  .tss-lw-meter .tss-lw-meter-fill, .tss-lw-speaking .tss-lw-dot { transition: none; }
}
";
    }

    /// <summary>
    /// Runs a callback once per animation frame while an element is in the document, and stops when it
    /// leaves - re-arming, so an element moved between containers keeps animating. A component that is
    /// not on screen costs nothing.
    /// </summary>
    internal sealed class MountedLoop
    {
        private readonly HTMLElement _element;
        private readonly System.Action _frame;
        private bool _running;
        private bool _stopped;

        internal MountedLoop(HTMLElement element, System.Action frame)
        {
            _element = element;
            _frame   = frame;
            Arm();
        }

        private void Arm()
        {
            if (_stopped) return;
            DomObserver.WhenMounted(_element, () =>
            {
                if (_stopped || _running) return;
                _running = true;
                Tick(0);
                DomObserver.WhenRemoved(_element, () =>
                {
                    _running = false;
                    Arm();
                });
            });
        }

        private void Tick(double _)
        {
            if (!_running || _stopped) return;

            try
            {
                _frame();
            }
            catch (System.Exception e)
            {
                console.error("Tesserae.Lauschwerk: a frame callback threw", e);
            }

            window.requestAnimationFrame(Tick);
        }

        /// <summary>Stops for good.</summary>
        internal void Stop()
        {
            _stopped = true;
            _running = false;
        }
    }
}
