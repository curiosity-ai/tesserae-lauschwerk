using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Alkove
{
    /// <summary>
    /// Opens the microphone the way alkove itself does - echo cancellation on, the browser's noise
    /// suppression and automatic gain off (they would process the voice before Focus does) - for a host or
    /// a component that wants the raw track too, to meter it beside the processed one. alkove never stops
    /// a stream it did not open: stop it with <see cref="Stop"/>.
    /// </summary>
    public static class Microphone
    {
        public static async Task<MediaStream> OpenAsync(string deviceId = null)
        {
            var audio = new MediaTrackConstraints { echoCancellation = true, channelCount = 1 };

            // Transpose.Core's MediaTrackConstraints predates these two (both are standard and in every
            // engine alkove supports), so they are set by name - the one string-keyed write here.
            Script.Set(audio, "noiseSuppression", false);
            Script.Set(audio, "autoGainControl", false);

            if (!string.IsNullOrEmpty(deviceId)) audio.deviceId = deviceId;

            try
            {
                return await navigator.mediaDevices.getUserMedia(new MediaStreamConstraints { audio = audio }).ToTask();
            }
            catch (System.Exception e)
            {
                var error = AlkoveException.FromJs(e);
                var code = error.Name == "NotAllowedError" || error.Name == "SecurityError" ? AlkoveException.MIC_DENIED
                         : error.Name == "NotFoundError" || error.Name == "OverconstrainedError" ? AlkoveException.MIC_NOT_FOUND
                         : error.Name == "NotReadableError" ? AlkoveException.MIC_BUSY
                         : error.Code;
                throw new AlkoveException(code, error.Name, error.Message);
            }
        }

        /// <summary>Stops every track of a stream - the microphone light goes off.</summary>
        public static void Stop(MediaStream stream)
        {
            if (stream is null) return;
            foreach (var track in stream.getTracks()) track.stop();
        }
    }
}
