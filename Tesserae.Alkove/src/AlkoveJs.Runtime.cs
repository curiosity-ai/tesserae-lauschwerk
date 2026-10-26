using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Alkove
{
    /// <summary>
    /// The entry point to alkove from C#: loading the SDK, where its models come from, and the factories
    /// for everything it does (see <c>AlkoveJs.cs</c>). Nothing is fetched until something needs it - the
    /// first input, file, word or <see cref="PreloadAsync"/> - so a page that never touches a microphone
    /// pays nothing.
    /// </summary>
    public static partial class AlkoveJs
    {
        private const string DEFAULT_ASSETS_PATH = "assets/js/alkove";

        /// <summary>Where <c>AlkoveSelfHostModels</c> puts the models (see buildTransitive/Tesserae.Alkove.targets).</summary>
        public const string SELF_HOSTED_MODELS_PATH = DEFAULT_ASSETS_PATH + "/models";

        private static string _assetsPath = DEFAULT_ASSETS_PATH;
        private static string _modelBaseUrl;
        private static Task   _loading;
        private static Action _offDiag;

        private static readonly List<Action> _pendingActions = new List<Action>();

        /// <summary>
        /// Where the staged SDK lives, relative to the page: the folder holding <c>alkove.js</c> and the
        /// versioned folder beside it. Defaults to <c>assets/js/alkove</c>, which is where the build
        /// copies it. Set it before anything loads.
        /// </summary>
        public static string AssetsPath
        {
            get => _assetsPath;
            set => _assetsPath = string.IsNullOrWhiteSpace(value) ? DEFAULT_ASSETS_PATH : value.TrimEnd('/');
        }

        /// <summary>The absolute URL of <see cref="AssetsPath"/>, resolved by the browser against <c>document.baseURI</c>, without a trailing slash.</summary>
        public static string BaseUrl => new URL(_assetsPath, document.baseURI).href.TrimEnd('/');

        /// <summary>
        /// Where the SDK downloads its models from. Null (the default) is Hugging Face, at the revision the
        /// bundled alkove pins - fine for trying it out, and each user then sends Hugging Face their IP.
        /// In production host them yourself: <see cref="UseSelfHostedModels"/>, or any URL serving the
        /// <c>.alk</c> files byte for byte (an SPA fallback page in their place fails the sha256 check).
        ///
        /// alkove reads this once, when the first model loads: set it before the first input, file or
        /// preload. A later change is ignored by the SDK and raises a <c>config.late</c> diagnostic.
        /// </summary>
        public static string ModelBaseUrl
        {
            get => _modelBaseUrl;
            set
            {
                _modelBaseUrl = string.IsNullOrWhiteSpace(value) ? null : new URL(value.TrimEnd('/') + "/", document.baseURI).href;
                if (IsLoaded && _modelBaseUrl is object) AlkoveLib.Alkove.configure(new AlkoveConfig { modelBaseUrl = _modelBaseUrl });
            }
        }

        /// <summary>
        /// Points <see cref="ModelBaseUrl"/> at the models the build copied in with
        /// <c>&lt;AlkoveSelfHostModels&gt;true&lt;/AlkoveSelfHostModels&gt;</c>.
        /// </summary>
        public static void UseSelfHostedModels(string path = SELF_HOSTED_MODELS_PATH) => ModelBaseUrl = path;

        /// <summary>True once the SDK has loaded and <c>window.alkove</c> is safe to call.</summary>
        public static bool IsLoaded => AlkoveWindow.alkove is object;

        /// <summary>The bundled alkove version, or null before it has loaded.</summary>
        public static string Version => IsLoaded ? AlkoveLib.version : null;

        /// <summary>
        /// Loads the SDK, at most once per page. Every factory awaits this, so a host rarely needs it -
        /// it is for warming the SDK up, or for reading <see cref="Version"/> early. A failed load is
        /// forgotten, so the next call tries again rather than inheriting the failure.
        /// </summary>
        public static Task LoadAsync()
        {
            if (_loading is null) _loading = LoadCoreAsync();
            return _loading;
        }

        private static async Task LoadCoreAsync()
        {
            try
            {
                // A module, and it has to be said: the URL ends in .js, so the loader's own sniffing would
                // pick a classic <script>, and the entry's import statement would be a syntax error.
                // Fully qualified, because the enclosing Tesserae namespace has a Require of its own.
                await Transpose.Require.RequireAsync(Transpose.RequireKind.Module, BaseUrl + "/alkove.js");

                if (!IsLoaded) throw new Exception("Loaded " + BaseUrl + "/alkove.js but window.alkove is not defined.");
            }
            catch
            {
                _loading = null;
                throw;
            }

            if (_modelBaseUrl is object) AlkoveLib.Alkove.configure(new AlkoveConfig { modelBaseUrl = _modelBaseUrl });

            var queued = _pendingActions.ToArray();
            _pendingActions.Clear();

            foreach (var action in queued)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    console.error("Tesserae.Alkove: a queued alkove call failed", exception);
                }
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> once the SDK is loaded - now if it is, otherwise queued until the
        /// load finishes. It does not start the load itself.
        /// </summary>
        public static void WhenLoaded(Action action)
        {
            if (action is null) return;

            if (IsLoaded)
            {
                action();
                return;
            }

            _pendingActions.Add(action);
        }

        private static event Action<IAlkoveDiag> _diagnostic;

        /// <summary>
        /// Diagnostics that belong to no input: <c>config.late</c>, <c>models.default-host</c>,
        /// <c>word.chain</c>. Every input also raises these through its own <see cref="VoiceInput.Diagnostic"/>.
        ///
        /// While anything listens here, alkove stops writing its global warnings to the console - the
        /// listener has them instead.
        /// </summary>
        public static event Action<IAlkoveDiag> Diagnostic
        {
            add
            {
                _diagnostic += value;
                WhenLoaded(SubscribeDiag);
            }
            remove
            {
                _diagnostic -= value;
                if (_diagnostic is null && _offDiag is object)
                {
                    _offDiag();
                    _offDiag = null;
                }
            }
        }

        private static void SubscribeDiag()
        {
            if (_offDiag is object || _diagnostic is null) return;
            _offDiag = AlkoveLib.Alkove.onDiag(d => _diagnostic?.Invoke(d));
        }

        /// <summary>
        /// Whether this browser can run alkove: a secure page (HTTPS or localhost), an AudioWorklet, and
        /// WebAssembly SIMD. Without the first two an input fails to open; without SIMD it opens and stays
        /// raw (<see cref="RawReason.Unsupported"/>). Loads the SDK, nothing else.
        /// </summary>
        public static async Task<AlkoveSupport> SupportAsync()
        {
            await LoadAsync();
            var s = AlkoveLib.Alkove.supported();
            return new AlkoveSupport(s.secure, s.worklet, s.simd);
        }

        /// <summary>The microphone permission, without asking for it: granted, denied or prompt.</summary>
        public static async Task<MicPermission> MicPermissionAsync()
        {
            await LoadAsync();
            var state = await PromiseHelper.ToTask<string>(AlkoveLib.Alkove.micPermission());
            return state == "granted" ? MicPermission.Granted : state == "denied" ? MicPermission.Denied : MicPermission.Prompt;
        }

        /// <summary>
        /// Downloads and compiles everything a usage needs ahead of time - a settings or onboarding
        /// screen - so the first input runs its models from its first second instead of going out raw
        /// while they load. <paramref name="progress"/> gets bytes loaded and the total.
        /// </summary>
        public static async Task PreloadAsync(AlkoveUsage usage, bool voiceprint = false, bool typedWords = false, Action<double, double> progress = null)
        {
            await LoadAsync();

            var options = new PreloadOptions();
            if (voiceprint) options.voiceprint = true;
            if (typedWords) options.typedWords = true;
            if (progress is object) options.onProgress = p => progress(p.loaded, p.total);

            try
            {
                await PromiseHelper.ToTask(AlkoveLib.Alkove.preload(UsageName(usage), options));
            }
            catch (Exception e)
            {
                throw AlkoveException.FromJs(e);
            }
        }

        /// <summary>True when everything a usage needs is already in the browser's cache: it can start offline.</summary>
        public static async Task<bool> IsCachedAsync(AlkoveUsage usage, bool voiceprint = false, bool typedWords = false)
        {
            await LoadAsync();

            var options = new UsageOptions();
            if (voiceprint) options.voiceprint = true;
            if (typedWords) options.typedWords = true;

            return await PromiseHelper.ToTask<bool>(AlkoveLib.Alkove.cached(UsageName(usage), options));
        }

        private static string UsageName(AlkoveUsage usage) => usage == AlkoveUsage.Wake ? "wake" : usage == AlkoveUsage.Agent ? "agent" : "call";
    }

    /// <summary>What a preload is for: which models it fetches.</summary>
    public enum AlkoveUsage
    {
        /// <summary>A call: Focus and Cue.</summary>
        Call,

        /// <summary>A voice agent: Focus and Cue.</summary>
        Agent,

        /// <summary>A wake word: Spot.</summary>
        Wake
    }

    public enum MicPermission
    {
        Prompt,
        Granted,
        Denied
    }

    /// <summary>What <see cref="AlkoveJs.SupportAsync"/> found.</summary>
    public sealed class AlkoveSupport
    {
        internal AlkoveSupport(bool secure, bool worklet, bool simd)
        {
            Secure  = secure;
            Worklet = worklet;
            Simd    = simd;
        }

        /// <summary>HTTPS or localhost: the microphone needs it.</summary>
        public bool Secure { get; }

        /// <summary>An AudioWorklet (Safari 14.1 and later).</summary>
        public bool Worklet { get; }

        /// <summary>WebAssembly SIMD: without it an input stays raw.</summary>
        public bool Simd { get; }

        /// <summary>Everything: the models will run.</summary>
        public bool Full => Secure && Worklet && Simd;
    }
}
