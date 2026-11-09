using System;

namespace LocalLLM
{
    /// <summary>
    /// Per-request generation settings. Immutable: each With method returns a new instance.
    /// Unset values (null) leave the engine default in place.
    /// </summary>
    public sealed class ChatOptions
    {
        public static readonly ChatOptions Default = new ChatOptions(null, null, null, false);

        ChatOptions(float? temperature, int? maxTokens, string jsonSchema, bool think)
        {
            Temperature = temperature;
            MaxTokens = maxTokens;
            JsonSchema = jsonSchema;
            Think = think;
        }

        public float? Temperature { get; }

        /// <summary>Upper bound on generated tokens.</summary>
        public int? MaxTokens { get; }

        /// <summary>JSON schema object. When set, the reply is constrained to JSON matching it.</summary>
        public string JsonSchema { get; }

        /// <summary>Lets thinking models (e.g. qwen3 on Ollama) reason before answering. Ignored by WebLLM.</summary>
        public bool Think { get; }

        public ChatOptions WithTemperature(float? value)
        {
            if (value < 0f) throw new ArgumentOutOfRangeException(nameof(value), "Temperature must not be negative.");
            return new ChatOptions(value, MaxTokens, JsonSchema, Think);
        }

        public ChatOptions WithMaxTokens(int? value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "MaxTokens must be positive.");
            return new ChatOptions(Temperature, value, JsonSchema, Think);
        }

        /// <param name="schema">A JSON schema object, or null / whitespace for free text.</param>
        public ChatOptions WithJsonSchema(string schema)
        {
            var trimmed = string.IsNullOrWhiteSpace(schema) ? null : schema.Trim();
            // The schema is embedded as raw JSON in requests, so it must be exactly one object.
            if (trimmed != null && !JsonText.IsSingleObject(trimmed))
                throw new ArgumentException("JSON schema must be a single JSON object.", nameof(schema));
            return new ChatOptions(Temperature, MaxTokens, trimmed, Think);
        }

        public ChatOptions WithThink(bool value) => new ChatOptions(Temperature, MaxTokens, JsonSchema, value);
    }
}
