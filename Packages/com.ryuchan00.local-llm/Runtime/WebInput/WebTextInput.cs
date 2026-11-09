using System;
using System.Runtime.InteropServices;
using AOT;

namespace LocalLLM
{
    /// <summary>
    /// HTML text box overlaid on the WebGL canvas. Unity WebGL text fields cannot receive IME input,
    /// so this is how players type Japanese, Chinese or Korean. Does nothing outside WebGL builds.
    /// </summary>
    public static class WebTextInput
    {
        delegate void SubmitCallback(string text);

        /// <summary>True when typing must go through this overlay instead of Unity's text fields.</summary>
        public static bool IsAvailable =>
#if UNITY_WEBGL && !UNITY_EDITOR
            true;
#else
            false;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void LocalLLMTextInput_Create(string placeholder, string buttonLabel, int maxLength, SubmitCallback callback);
        [DllImport("__Internal")] static extern void LocalLLMTextInput_SetState(bool visible, bool enabled);
#else
        static void LocalLLMTextInput_Create(string placeholder, string buttonLabel, int maxLength, SubmitCallback callback) { }
        static void LocalLLMTextInput_SetState(bool visible, bool enabled) { }
#endif

        static bool? lastVisible;
        static bool? lastEnabled;

        /// <summary>Raised with the trimmed, non-empty text when the player presses Enter or the button.</summary>
        public static event Action<string> Submitted;

        public static void Create(string placeholder, string buttonLabel, int maxLength = 500)
        {
            ReleaseKeyboardCapture();
            LocalLLMTextInput_Create(placeholder ?? string.Empty, buttonLabel ?? string.Empty, maxLength, OnSubmit);
        }

        public static void SetState(bool visible, bool enabled)
        {
            if (lastVisible == visible && lastEnabled == enabled) return;
            lastVisible = visible;
            lastEnabled = enabled;
            LocalLLMTextInput_SetState(visible, enabled);
        }

        // Lets key events reach the HTML input instead of being swallowed by Unity.
        // WebGLInput lives in UnityEngine.WebGLModule, which packages cannot reference directly.
        // Best effort: the text box also stops its key events from propagating to Unity.
        static void ReleaseKeyboardCapture()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var property = Type.GetType("UnityEngine.WebGLInput, UnityEngine.WebGLModule")
                ?.GetProperty("captureAllKeyboardInput");
            property?.SetValue(null, false);
#endif
        }

        [MonoPInvokeCallback(typeof(SubmitCallback))]
        static void OnSubmit(string text) => Submitted?.Invoke(text ?? string.Empty);
    }
}
