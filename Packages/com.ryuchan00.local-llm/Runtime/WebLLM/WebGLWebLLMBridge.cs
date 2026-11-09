using System;
using System.Runtime.InteropServices;
using AOT;

namespace LocalLLM
{
    /// <summary>
    /// Calls LocalLLM.jslib. jslib callbacks must be static, so only the most recently set up
    /// backend receives events: use one WebLLMBackend at a time.
    /// </summary>
    sealed class WebGLWebLLMBridge : IWebLLMBridge
    {
        delegate void EventCallback(int kind, string payload);

        static Action<WebLLMEvent, string> handler;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int LocalLLM_IsWebGPUSupported();
        [DllImport("__Internal")] static extern void LocalLLM_Setup(string baseUrl, string appConfigJson, EventCallback callback);
        [DllImport("__Internal")] static extern void LocalLLM_Init(string modelId);
        [DllImport("__Internal")] static extern void LocalLLM_Chat(string requestJson);
        [DllImport("__Internal")] static extern void LocalLLM_Cancel();
        [DllImport("__Internal")] static extern void LocalLLM_Unload();
        [DllImport("__Internal")] static extern void LocalLLM_CheckCache(string modelId);
        [DllImport("__Internal")] static extern void LocalLLM_DeleteCache(string modelId);
#else
        // Outside WebGL there is no browser; report WebGPU as missing so Load fails cleanly.
        static int LocalLLM_IsWebGPUSupported() => 0;
        static void LocalLLM_Setup(string baseUrl, string appConfigJson, EventCallback callback) { }
        static void LocalLLM_Init(string modelId) => NotInBrowser(WebLLMEvent.LoadError);
        static void LocalLLM_Chat(string requestJson) => NotInBrowser(WebLLMEvent.ChatError);
        static void LocalLLM_Cancel() { }
        static void LocalLLM_Unload() { }
        static void LocalLLM_CheckCache(string modelId) => NotInBrowser(WebLLMEvent.CacheError);
        static void LocalLLM_DeleteCache(string modelId) => NotInBrowser(WebLLMEvent.CacheError);
        static void NotInBrowser(WebLLMEvent kind) => handler?.Invoke(kind, "WebLLM only runs in WebGL builds.");
#endif

        public bool IsWebGPUSupported() => LocalLLM_IsWebGPUSupported() != 0;

        public void Setup(string baseUrl, string appConfigJson, Action<WebLLMEvent, string> onEvent)
        {
            handler = onEvent ?? throw new ArgumentNullException(nameof(onEvent));
            LocalLLM_Setup(baseUrl, appConfigJson, HandleEvent);
        }

        public void Init(string modelId) => LocalLLM_Init(modelId);
        public void Chat(string requestJson) => LocalLLM_Chat(requestJson);
        public void Cancel() => LocalLLM_Cancel();
        public void Unload() => LocalLLM_Unload();
        public void CheckCache(string modelId) => LocalLLM_CheckCache(modelId);
        public void DeleteCache(string modelId) => LocalLLM_DeleteCache(modelId);

        [MonoPInvokeCallback(typeof(EventCallback))]
        static void HandleEvent(int kind, string payload) => handler?.Invoke((WebLLMEvent)kind, payload ?? string.Empty);
    }
}
