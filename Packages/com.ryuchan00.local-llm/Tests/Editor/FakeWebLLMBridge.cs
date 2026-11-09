using System;
using System.Collections.Generic;

namespace LocalLLM.Tests
{
    sealed class FakeWebLLMBridge : IWebLLMBridge
    {
        Action<WebLLMEvent, string> onEvent;

        public bool WebGPUSupported = true;
        public readonly List<string> Calls = new List<string>();
        public string LastRequestJson;
        public string BaseUrl;
        public string AppConfigJson;

        public bool IsWebGPUSupported() => WebGPUSupported;

        public void Setup(string baseUrl, string appConfigJson, Action<WebLLMEvent, string> handler)
        {
            BaseUrl = baseUrl;
            AppConfigJson = appConfigJson;
            onEvent = handler;
        }

        public void Init(string modelId) => Calls.Add("Init:" + modelId);

        public void Chat(string requestJson)
        {
            Calls.Add("Chat");
            LastRequestJson = requestJson;
        }

        public void Cancel() => Calls.Add("Cancel");

        public void Unload() => Calls.Add("Unload");

        public void CheckCache(string modelId) => Calls.Add("CheckCache:" + modelId);

        public void DeleteCache(string modelId) => Calls.Add("DeleteCache:" + modelId);

        public void Emit(WebLLMEvent kind, string payload = "") => onEvent(kind, payload);
    }
}
