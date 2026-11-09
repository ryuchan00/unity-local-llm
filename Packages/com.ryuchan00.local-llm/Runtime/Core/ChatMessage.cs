using System;

namespace LocalLLM
{
    /// <summary>Role names used by both Ollama and WebLLM (OpenAI style).</summary>
    public static class ChatRole
    {
        public const string System = "system";
        public const string User = "user";
        public const string Assistant = "assistant";
    }

    /// <summary>One turn of a conversation.</summary>
    [Serializable]
    public sealed class ChatMessage
    {
        // Serialized fields stay private so the message cannot change after it is created.
        [UnityEngine.SerializeField] string role;
        [UnityEngine.SerializeField] string content;

        public ChatMessage(string role, string content)
        {
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role must not be empty.", nameof(role));
            this.role = role;
            this.content = content ?? string.Empty;
        }

        public string Role => role;
        public string Content => content;
    }
}
