using System;
using System.Threading.Tasks;
using Tesserae;
using Tesserae.Alkove;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Alkove.AlkoveUI;
using static Tesserae.Alkove.Sample.SamplesHelper;

namespace Tesserae.Alkove.Sample
{
    [SampleDetails(Group = "Runtime and hosting", Order = 10, Icon = UIcons.Download)]
    public class ModelsAndSupportSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ModelsAndSupportSample()
        {
            var support = Readout("alk-support", "Checking...");
            var version = Readout("alk-version", "-");
            var models  = Readout("alk-models", "-");
            var mic     = Readout("alk-mic", "-");

            async Task CheckAsync()
            {
                var s = await AlkoveJs.SupportAsync();
                support.Text = "secure page: " + Yes(s.Secure) + ", AudioWorklet: " + Yes(s.Worklet) + ", WebAssembly SIMD: " + Yes(s.Simd) + (s.Full ? " - the models will run." : " - inputs will stay raw.");
                version.Text = "alkove " + AlkoveJs.Version + (AlkoveJs.BuiltFromSource ? ", built from source (sdk/)" : ", the npm release") + ", engine sha256 " + AlkoveJs.EngineSha256.Substring(0, 12) + "… from " + AlkoveJs.BaseUrl;
                models.Text  = AlkoveJs.ModelBaseUrl is null ? "Models from Hugging Face" : "Self-hosted models: " + AlkoveJs.ModelBaseUrl;
                mic.Text     = "Microphone permission: " + (await AlkoveJs.MicPermissionAsync()).ToString().ToLower();
            }

            var diags = VStack().WS().Id("alk-diags");
            Action<IAlkoveDiag> onDiag = d => diags.Add(TextBlock(d.level + " " + d.code + ": " + d.message).Small());

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ModelsAndSupportSample), UIcons.Download, "Where the models come from, what the browser supports, and preloading")
               .FlatSection(VStack().WS().Children(
                    Card(VStack().Children(
                        TextBlock("Nothing is fetched until something needs it. The SDK (a module, ~600 KB with its engine) loads on first use; the models (7.5 MB together) load per usage, checked by sha256 and kept in the browser's Cache Storage."),
                        TextBlock("This gallery hosts its own models: <AlkoveSelfHostModels>true</AlkoveSelfHostModels> in the csproj downloads them at build time, and AlkoveJs.UseSelfHostedModels() points the SDK at them. Without it they come from Hugging Face.").PT(8))).SetTitle("Overview"),
                    Card(VStack().WS().Gap(4.px()).Children(support, version, models, mic)).SetTitle("This browser"),
                    Card(VStack().WS().Gap(12.px()).Children(
                        ModelPreloader(AlkoveUsage.Call, voiceprint: true),
                        ModelPreloader(AlkoveUsage.Wake, typedWords: true),
                        SampleHint("A settings screen preloads what the next input needs, so it runs its models from its first second rather than going out raw while they download."))).SetTitle("Preload"),
                    Card(VStack().WS().Children(
                        TextBlock("Global diagnostics (config.late, models.default-host, ...) appear here.").Small().Secondary(),
                        diags)).SetTitle("Diagnostics"),
                    Card(CallInputSample.Code(@"AlkoveJs.UseSelfHostedModels();                 // before the first input
await AlkoveJs.PreloadAsync(AlkoveUsage.Call, voiceprint: true, progress: (loaded, total) => ...);
var offline = await AlkoveJs.IsCachedAsync(AlkoveUsage.Call);")).SetTitle("Usage")))
               .SeeAlso(typeof(CallInputSample), typeof(ProcessFileSample));

            DomObserver.WhenMounted(_content.Render(), () =>
            {
                AlkoveJs.Diagnostic += onDiag;
                CheckAsync().FireAndForget();
                DomObserver.WhenRemoved(_content.Render(), () => AlkoveJs.Diagnostic -= onDiag);
            });
        }

        private static string Yes(bool value) => value ? "yes" : "no";

        public HTMLElement Render() => _content.Render();
    }
}
