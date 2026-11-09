using System;

namespace LocalLLM
{
    /// <summary>Calls into the browser. Replaced by a fake in tests so the backend logic runs without a browser.</summary>
    interface IWebLLMBridge
    {
        bool IsWebGPUSupported();

        /// <param name="baseUrl">URL of the folder holding local-llm-loader.js and web-llm.js.</param>
        /// <param name="appConfigJson">Custom WebLLM appConfig, or empty for the pinned default.</param>
        void Setup(string baseUrl, string appConfigJson, Action<WebLLMEvent, string> onEvent);

        void Init(string modelId);
        void Chat(string requestJson);
        void Cancel();

        /// <summary>Frees the engine and its GPU memory.</summary>
        void Unload();
        void CheckCache(string modelId);
        void DeleteCache(string modelId);
    }
}
