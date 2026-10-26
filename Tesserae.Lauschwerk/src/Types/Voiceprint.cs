using System;
using Transpose;
using Transpose.Core;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// A voiceprint: what Timbre learned of one person's voice. Give it to an input to keep only that
    /// voice (<see cref="VoiceInputSettings.Voiceprint"/>), to a wake listener so only that voice wakes it,
    /// or compare two with <see cref="Timbre.Verify"/>.
    ///
    /// Store it with <see cref="ToJson"/> and bring it back with <see cref="FromJson"/>. A stored print is
    /// biometric data (GDPR art. 9): treat it as such. A print is bound to the Timbre model that made it;
    /// after an lauschwerk upgrade that changes the model, <see cref="FromJson"/> answers null and the print
    /// has to be learned again.
    /// </summary>
    public sealed class Voiceprint
    {
        internal Voiceprint(IVoiceprintJs js)
        {
            Js = js;
        }

        internal IVoiceprintJs Js { get; }

        /// <summary>The model id it was made by (name and sha256 prefix).</summary>
        public string Model => Js.model;

        /// <summary><c>raw</c> when learned from the microphone, <c>focus</c> when learned behind Focus.</summary>
        public string Source => Js.source;

        /// <summary>The seconds of voice the print was made from. Verification is stricter for a short print.</summary>
        public double Seconds
        {
            get
            {
                var centers = Js.centers;
                var total = 0.0;
                if (centers is null) return total;
                for (var i = 0; i < centers.Length; i++) total += centers[i].seconds;
                return total;
            }
        }

        /// <summary>The cosine similarity of the closest pair of centers, -1 to 1.</summary>
        public double Similarity(Voiceprint other) => Js.similarity(other.Js);

        /// <summary>The print as lauschwerk's <c>VoiceprintJSON</c>, as a string - what to store.</summary>
        public string ToJson() => es5.JSON.stringify(Js);

        /// <summary>
        /// A stored print, or null when it was made by another Timbre model (learn it again). Throws on
        /// a string that is not an lauschwerk voiceprint. The SDK has to be loaded: inside an async flow,
        /// <c>await LauschwerkJs.LoadAsync()</c> first, or use <see cref="FromJsonAsync"/>.
        /// </summary>
        public static Voiceprint FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            if (!LauschwerkJs.IsLoaded) throw new InvalidOperationException("lauschwerk is not loaded yet: await LauschwerkJs.LoadAsync() or use Voiceprint.FromJsonAsync.");
            var js = LauschwerkLib.Voiceprint.fromJSON(json);
            return js is null ? null : new Voiceprint(js);
        }

        /// <summary><see cref="FromJson"/>, loading the SDK first.</summary>
        public static async System.Threading.Tasks.Task<Voiceprint> FromJsonAsync(string json)
        {
            await LauschwerkJs.LoadAsync();
            return FromJson(json);
        }

        /// <summary>One print from several of the same voice.</summary>
        public static Voiceprint Merge(params Voiceprint[] prints)
        {
            var js = new IVoiceprintJs[prints.Length];
            for (var i = 0; i < prints.Length; i++) js[i] = prints[i].Js;
            return new Voiceprint(LauschwerkLib.Voiceprint.merge(js));
        }

        internal static Voiceprint Wrap(IVoiceprintJs js) => js is null ? null : new Voiceprint(js);
    }
}
