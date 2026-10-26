using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Transpose.Core;
using Tesserae;
using Tesserae.Lauschwerk;
using Tesserae.Themes.Curiosity;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Lauschwerk.Sample
{
    /// <summary>
    /// A gallery app exercising each Tesserae.Lauschwerk feature so it can be eyeballed in a browser,
    /// shaped like Tesserae's own sample gallery: a sidebar of pages on the left, one page per
    /// feature on the right, and a route per page so any of them can be linked to or opened in its
    /// own tab.
    ///
    /// Every page works with a real microphone, and every page also works without one: the speech
    /// recordings under <c>assets/audio/</c> (synthesized by tools/Lauschwerk.TestAudio) can be played to
    /// the models as files, which is also how the browser checks drive the gallery - Chromium's fake
    /// capture device plays one of them as the microphone.
    ///
    /// The gallery hosts its own models (<c>LauschwerkSelfHostModels</c> in the csproj), so the visitor's
    /// browser only ever talks to this site.
    /// </summary>
    internal static class App
    {
        private const string _sidebarOpenStateKey = "tss-lw-sidebar-open-close";

        private const string _themeKey = "tss-lw-curiosity-theme";

        private static bool IsCuriosity => Theme.CustomTheme is object;

        // ?theme=curiosity wins, so a page can be linked to - and driven by a browser check - in either look.
        private static bool WantsCuriosity()
        {
            var fromUrl = new URLSearchParams(window.location.search).get("theme");

            if (fromUrl is object) return fromUrl == "curiosity";

            return localStorage.getItem(_themeKey) == bool.TrueString;
        }

        private static async Task SetCuriosityAsync(bool on)
        {
            if (on) await Theme.SetCustomTheme(CuriosityTheme.Instance);
            else    await Theme.ClearCustomTheme();

            localStorage.setItem(_themeKey, on.ToString());
        }

        private static void Main()
        {
            LauschwerkJs.UseSelfHostedModels();

            document.body.style.overflow = "hidden";

            // Ensure the viewport meta tag is present so that mobile browsers use the device width
            // instead of rendering at a desktop width and scaling down.
            if (document.head.querySelector("meta[name='viewport']") is null)
            {
                var viewportMeta = document.createElement("meta");
                viewportMeta["name"]    = "viewport";
                viewportMeta["content"] = "width=device-width, initial-scale=1.0, maximum-scale=5.0";
                document.head.appendChild(viewportMeta);
            }

            // Adds/removes the tss-mobile class on body whenever the viewport is 768px or narrower.
            Theme.EnableMobileDetection(breakpoint: 768);

            var allSidebarItems     = new List<ISidebarItem>();
            var sampleToSidebarItem = new Dictionary<SamplePage, ISidebarItem>();

            void SelectSidebar(ISidebarItem toSelect)
            {
                allSidebarItems.ForEach(i => i.IsSelected = i == toSelect);
            }

            var currentPage = new SettableObservable<SamplePage>(null);

            currentPage.Observe(selected =>
            {
                if (selected is object && sampleToSidebarItem.TryGetValue(selected, out var item))
                {
                    SelectSidebar(item);
                }
            });

            var sidebar = Sidebar();

            sidebar.AddHeader(new SidebarText("header", "Lauschwerk", "TSS", textSize: TextSize.XLarge, textWeight: TextWeight.Bold));

            var searchBox = new SidebarSearchBox("search", "Search...");
            searchBox.OnSearch(term => sidebar.Search(term));
            sidebar.AddHeader(searchBox);

            sidebar.AddHeader(new SidebarButton("SOURCE_CODE", UIcons.CodeBranch, "Source Code",
                    new SidebarCommand(UIcons.ArrowUpRightFromSquare).Tooltip("Open repository on GitHub")
                       .OnClick(() => window.open("https://github.com/theolivenbaum/lauschwerk", "_blank")))
               .CommandsAlwaysVisible()
               .OnClick(() => window.open("https://github.com/theolivenbaum/lauschwerk", "_blank")));

            // A page is only built when it is opened and torn down when it is left, so one visit opens at
            // most that page's microphone - and every navigation exercises the components' teardown.
            var contentArea = Defer(currentPage, async page => page is null
                ? (IComponent)CenteredCardWithBackground(Message("Tesserae.Lauschwerk", "On-device voice isolation, voice activity, wake words and voiceprints for Tesserae. Pick a feature from the list.").Icon(UIcons.Microphone))
                : VStack().S().ScrollY().Children((await page.ContentGenerator()).WS().MinHeight(100.percent())));

            // On a phone the sidebar is a page (Sidebar.AsPage) that takes turns with the content, the
            // same shape as Tesserae's own gallery: picking a feature steps it aside, and this bar's back
            // button brings it back. The bar collapses itself on a desktop, where the sidebar is a rail.
            var pageBar = SidebarPageBar(sidebar)
               .Brand(TextBlock("TSS").Bold().Foreground(Theme.Primary.Background))
               .SetTitle("Lauschwerk");

            currentPage.Observe(page => pageBar.SetTitle(page is object ? page.Name : "Lauschwerk"));

            var content     = VStack().Children(pageBar, contentArea.WS().H(1).Grow());
            var pageContent = HStack().Class("tss-page-layout").S().Children(sidebar.HS(), content.HS().W(1).Grow());

            MountToBody(pageContent);

            // Important: reflection only works here because the metadata is emitted inline with the
            // JavaScript rather than into a separate .meta.js file - i.e. tps.json needs
            //     "reflection": { "disabled": false, "target": "inline" }
            var samples = typeof(ISample).Assembly.GetTypes()
               .Where(t => typeof(ISample).IsAssignableFrom(t) && !t.IsInterface)
               .Select(sampleType =>
                {
                    var details = sampleType.GetCustomAttributes(typeof(SampleDetailsAttribute), true).FirstOrDefault() as SampleDetailsAttribute;
                    var group   = details is object ? details.Group : "Others";
                    var order   = details is object ? details.Order : 0;
                    var icon    = details is object ? details.Icon : UIcons.Circle;

                    return new SamplePage(sampleType.Name, SamplePage.FormatName(sampleType), group, order, icon,
                        async () => await Activator.CreateInstanceAsync(sampleType) as IComponent);
                })
               .ToDictionary(s => s.Name, s => s);

            // A page is always open, so the command that closes the rail is hidden with the brand's own
            var openClose = new SidebarCommand(UIcons.AngleLeft).Tooltip("Close Sidebar").Class("tss-sidebar-close-command");

            openClose.OnClick(() =>
            {
                sidebar.Toggle();

                if (sidebar.IsClosed)
                {
                    openClose.SetIcon(UIcons.AngleRight).Tooltip("Open Sidebar");
                    localStorage.setItem(_sidebarOpenStateKey, false.ToString());
                }
                else
                {
                    openClose.SetIcon(UIcons.AngleLeft).Tooltip("Close Sidebar");
                    localStorage.setItem(_sidebarOpenStateKey, true.ToString());
                }
            });

            var lightDark = new SidebarCommand(Theme.IsDark ? UIcons.Moon : UIcons.Sun).Tooltip(Theme.IsDark ? "Dark Mode" : "Light Mode");

            lightDark.OnClick(() =>
            {
                if (Theme.IsDark)
                {
                    Theme.Light();
                    lightDark.SetIcon(UIcons.Sun).Tooltip("Light Mode");
                }
                else
                {
                    Theme.Dark();
                    lightDark.SetIcon(UIcons.Moon).Tooltip("Dark Mode");
                }
            });

            // The Curiosity custom theme restyles every component, light and dark; the package's components
            // draw with Tesserae's tokens, so they follow it (and the sun/moon switch) on their own.
            var curiosity = new SidebarCommand(UIcons.Palette).Tooltip(IsCuriosity ? "Curiosity theme" : "Default theme");

            curiosity.OnClick(async () =>
            {
                await SetCuriosityAsync(!IsCuriosity);
                curiosity.Tooltip(IsCuriosity ? "Curiosity theme" : "Default theme");
            });

            if (WantsCuriosity()) SetCuriosityAsync(true).FireAndForget();

            sidebar.AddFooter(new SidebarCommands("CONFIG", curiosity, lightDark, openClose));

            var groupIndex = 0;

            foreach (var group in samples.Values.GroupBy(s => s.Group).OrderBy(g => g.Key))
            {
                sidebar.AddContent(new SidebarSeparator(group.Key + groupIndex++, group.Key));

                var itemIndex = 0;

                foreach (var item in group.OrderBy(s => s.Order).ThenBy(s => s.Name.ToLower()))
                {
                    var sidebarItem = new SidebarButton(item.Name + itemIndex++, item.Icon, item.Name,
                        new SidebarCommand(UIcons.ArrowUpRightFromSquare).Tooltip("Open in new tab").OnClick(() => window.open($"#/view/{item.Name}", "_blank")));

                    sidebarItem.OnClick(() =>
                    {
                        // Push updates the URL without reloading, so the page has a route of its own
                        // to link to, and the browser's back button walks the pages visited.
                        Router.Push($"#/view/{item.Name}");

                        currentPage.Value = item;
                    });

                    sidebar.AddContent(sidebarItem);
                    allSidebarItems.Add(sidebarItem);
                    sampleToSidebarItem[item] = sidebarItem;
                }
            }

            Router.Register("home", "/", _ => currentPage.Value = null);

            foreach (var kv in samples)
            {
                Router.Register($"#/view/{kv.Key.Replace(" ", "%20")}", _ => currentPage.Value = kv.Value);
            }

            // Follows the window between the two layouts. On a phone the page opens on whatever the
            // address names, and on the list when it names nothing. Back on a desktop the rail
            // returns to the user's own open/closed preference.
            void ApplyLayoutMode()
            {
                var isMobile = Theme.IsMobileMode;

                sidebar.AsPage(isMobile);

                if (isMobile)
                {
                    if (currentPage.Value is object) sidebar.ShowContent();
                    else                             sidebar.ShowSidebar();
                }
                else
                {
                    var sidebarOpenState = bool.TryParse(localStorage.getItem(_sidebarOpenStateKey), out var v) ? v : true;
                    sidebar.Closed(!sidebarOpenState);

                    openClose.SetIcon(sidebarOpenState ? UIcons.AngleLeft : UIcons.AngleRight)
                       .Tooltip(sidebarOpenState ? "Close Sidebar" : "Open Sidebar");
                }
            }

            ApplyLayoutMode();
            Theme.OnMobileModeChanged += ApplyLayoutMode;

            // A route reached by a link or the browser's back button is what the user asked for, so on
            // a phone it gets the screen.
            currentPage.ObserveFutureChanges(page =>
            {
                if (sidebar.IsPage && page is object) sidebar.ShowContent();
            });

            Router.Initialize();

            // Forcibly match the current route at first load: the routes were only just registered,
            // and we want them matched against the current URL without changing it.
            Router.Refresh(onDone: Router.ForceMatchCurrent);
        }
    }
}
