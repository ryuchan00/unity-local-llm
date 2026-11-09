using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LocalLLM
{
    /// <summary>Event codes sent from LocalLLM.jslib. Keep in sync with the constants there.</summary>
    enum WebLLMEvent
    {
        Progress = 0,
        Ready = 1,
        Token = 2,
        Complete = 3,
        ChatError = 4,
        LoadError = 5,
        CacheStatus = 6,
        CacheDeleted = 7,
        CacheError = 8,
    }

    static class WebLLMProtocol
    {
        [Serializable]
        sealed class CompletePayload
        {
            public string text;
            public bool has_usage;
            public int prompt_tokens;
            public int completion_tokens;
            public double time_to_first_token_s;
        }

        public static string BuildRequestJson(IEnumerable<ChatMessage> messages, ChatOptions options)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            options = options ?? ChatOptions.Default;

            var json = new StringBuilder(256);
            json.Append("{\"messages\":");
            JsonText.AppendMessages(json, messages);
            if (options.Temperature.HasValue) json.Append(",\"temperature\":").Append(JsonText.Number(options.Temperature.Value));
            if (options.MaxTokens.HasValue) json.Append(",\"max_tokens\":").Append(options.MaxTokens.Value);
            if (options.JsonSchema != null)
            {
                // WebLLM takes the schema as a string.
                json.Append(",\"json_schema\":");
                JsonText.AppendQuoted(json, options.JsonSchema);
            }
            json.Append('}');
            return json.ToString();
        }

        public static ChatResult ParseComplete(string payload)
        {
            CompletePayload parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<CompletePayload>(payload);
            }
            catch (ArgumentException)
            {
                // Fall through: keep the raw text rather than losing the reply.
            }
            if (parsed == null) return new ChatResult(payload, ChatUsage.Unknown);

            var usage = parsed.has_usage
                ? new ChatUsage(parsed.prompt_tokens, parsed.completion_tokens,
                    TimeSpan.FromMilliseconds(Math.Round(parsed.time_to_first_token_s * 1000)))
                : ChatUsage.Unknown;
            return new ChatResult(parsed.text, usage);
        }
    }
}
