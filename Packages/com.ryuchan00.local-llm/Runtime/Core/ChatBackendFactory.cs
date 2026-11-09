using System;

namespace LocalLLM
{
    /// <summary>
    /// Picks WebLLM on WebGL builds and Ollama everywhere else (Editor, standalone).
    /// Define LOCALLLM_FORCE_OLLAMA to make a WebGL build use Ollama, e.g. on machines without WebGPU.
    /// </summary>
    public static class ChatBackendFactory
    {
        public static bool UsesWebLLM =>
#if UNITY_WEBGL && !UNITY_EDITOR && !LOCALLLM_FORCE_OLLAMA
            true;
#else
            false;
#endif

        public static IChatBackend Create(LocalLLMSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return UsesWebLLM
                ? new WebLLMBackend(settings.WebLLMModel, settings.WebLLMBaseUrl, settings.WebLLMAppConfigJson)
                : (IChatBackend)new OllamaBackend(
                    settings.OllamaUrl, settings.OllamaModel, settings.OllamaKeepAlive,
                    settings.OllamaContextLength, settings.OllamaTimeoutSeconds);
        }
    }
}
