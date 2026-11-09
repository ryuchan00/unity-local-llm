using System;

namespace LocalLLM
{
    /// <summary>
    /// Token counts and timing reported by the engine. Any value can be null when the engine did not report it.
    /// </summary>
    public sealed class ChatUsage
    {
        public static readonly ChatUsage Unknown = new ChatUsage(null, null, null);

        public ChatUsage(int? promptTokens, int? completionTokens, TimeSpan? promptTime)
        {
            PromptTokens = promptTokens;
            CompletionTokens = completionTokens;
            PromptTime = promptTime;
        }

        /// <summary>
        /// Prompt tokens as reported by the engine. Ollama counts the whole prompt;
        /// WebLLM counts only the part it did not reuse from the previous turn.
        /// </summary>
        public int? PromptTokens { get; }

        public int? CompletionTokens { get; }

        /// <summary>
        /// Time spent processing the prompt before the first token. A short time on a long prompt
        /// means the engine reused its cache of the shared prefix.
        /// </summary>
        public TimeSpan? PromptTime { get; }
    }

    /// <summary>A finished reply.</summary>
    public sealed class ChatResult
    {
        public ChatResult(string text, ChatUsage usage)
        {
            Text = text ?? string.Empty;
            Usage = usage ?? ChatUsage.Unknown;
        }

        public string Text { get; }
        public ChatUsage Usage { get; }
    }
}
