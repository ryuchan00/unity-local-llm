using System;
using UnityEngine;

namespace LocalLLM
{
    /// <summary>
    /// Where to run the model. Serializable so it can be edited in the Inspector, or built in code.
    /// </summary>
    [Serializable]
    public sealed class LocalLLMSettings
    {
        public const string DefaultOllamaUrl = "http://localhost:11434";
        public const string DefaultOllamaModel = "llama3.2:3b";
        public const string DefaultWebLLMModel = "Qwen2.5-1.5B-Instruct-q4f16_1-MLC";

        [Header("Editor / Standalone (Ollama)")]
        [SerializeField] string ollamaUrl = DefaultOllamaUrl;
        [SerializeField] string ollamaModel = DefaultOllamaModel;
        [Tooltip("How long Ollama keeps the model in memory after a request, e.g. \"30m\". " +
                 "Unloading also drops the prompt cache. Empty uses the server default (5m).")]
        [SerializeField] string ollamaKeepAlive = "30m";
        [Tooltip("Context window in tokens. Keep it fixed: changing it makes Ollama reload the model. 0 uses the server default.")]
        [SerializeField] int ollamaContextLength = 4096;
        [Tooltip("Gives up on a request after this many seconds, including loading the model. 0 waits forever.")]
        [SerializeField] int ollamaTimeoutSeconds = 600;

        [Header("WebGL (WebLLM)")]
        [SerializeField] string webLLMModel = DefaultWebLLMModel;
        [Tooltip("Optional WebLLM appConfig JSON, e.g. to serve the model from your own server. Empty uses the built-in, pinned config.")]
        [SerializeField, TextArea(2, 6)] string webLLMAppConfigJson = string.Empty;

        public LocalLLMSettings() { }

        public LocalLLMSettings(
            string ollamaUrl = DefaultOllamaUrl,
            string ollamaModel = DefaultOllamaModel,
            string ollamaKeepAlive = "30m",
            int ollamaContextLength = 4096,
            int ollamaTimeoutSeconds = 600,
            string webLLMModel = DefaultWebLLMModel,
            string webLLMAppConfigJson = "")
        {
            if (ollamaContextLength < 0) throw new ArgumentOutOfRangeException(nameof(ollamaContextLength));
            if (ollamaTimeoutSeconds < 0) throw new ArgumentOutOfRangeException(nameof(ollamaTimeoutSeconds));
            this.ollamaTimeoutSeconds = ollamaTimeoutSeconds;
            this.ollamaUrl = ollamaUrl;
            this.ollamaModel = ollamaModel;
            this.ollamaKeepAlive = ollamaKeepAlive;
            this.ollamaContextLength = ollamaContextLength;
            this.webLLMModel = webLLMModel;
            this.webLLMAppConfigJson = webLLMAppConfigJson;
        }

        public string OllamaUrl => ollamaUrl;
        public string OllamaModel => ollamaModel;
        public string OllamaKeepAlive => ollamaKeepAlive;
        public int OllamaContextLength => ollamaContextLength;
        public int OllamaTimeoutSeconds => ollamaTimeoutSeconds;
        public string WebLLMModel => webLLMModel;
        public string WebLLMAppConfigJson => webLLMAppConfigJson;
    }
}
