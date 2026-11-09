using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LocalLLM
{
    readonly struct OllamaChunk
    {
        public OllamaChunk(string content, bool done, string error, ChatUsage usage)
        {
            Content = content ?? string.Empty;
            Done = done;
            Error = error;
            Usage = usage;
        }

        public string Content { get; }
        public bool Done { get; }
        public string Error { get; }
        /// <summary>Only set on the final chunk.</summary>
        public ChatUsage Usage { get; }
    }

    /// <summary>JSON encoding/decoding and error mapping for the Ollama /api/chat endpoint.</summary>
    static class OllamaProtocol
    {
        const long NanosecondsPerTick = 100;

        [Serializable]
        sealed class ResponseMessage
        {
            public string content;
        }

        [Serializable]
        sealed class Response
        {
            public ResponseMessage message;
            public bool done;
            public string error;
            public int prompt_eval_count;
            public long prompt_eval_duration;
            public int eval_count;
        }

        /// <summary>
        /// True for plain http to a host other than this machine: the conversation would cross the network unencrypted.
        /// </summary>
        public static bool IsCleartextRemote(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback;
        }

        /// <summary>Validates the server URL and returns the chat endpoint. Only http and https are allowed.</summary>
        public static string ChatEndpoint(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException($"Server URL must be an absolute http(s) URL: '{baseUrl}'", nameof(baseUrl));
            return baseUrl.TrimEnd('/') + "/api/chat";
        }

        public static string BuildRequestJson(
            string model,
            IEnumerable<ChatMessage> messages,
            ChatOptions options,
            string keepAlive = null,
            int contextLength = 0)
        {
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Model name must not be empty.", nameof(model));
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            options = options ?? ChatOptions.Default;

            var json = new StringBuilder(256);
            json.Append("{\"model\":");
            JsonText.AppendQuoted(json, model.Trim());
            json.Append(",\"messages\":");
            JsonText.AppendMessages(json, messages);
            json.Append(",\"stream\":true");
            json.Append(",\"think\":").Append(options.Think ? "true" : "false");
            if (!string.IsNullOrWhiteSpace(keepAlive))
            {
                json.Append(",\"keep_alive\":");
                JsonText.AppendQuoted(json, keepAlive.Trim());
            }
            AppendModelOptions(json, options, contextLength);
            // The schema is already validated as an object by ChatOptions; embed it as raw JSON.
            if (options.JsonSchema != null) json.Append(",\"format\":").Append(options.JsonSchema);
            json.Append('}');
            return json.ToString();
        }

        static void AppendModelOptions(StringBuilder json, ChatOptions options, int contextLength)
        {
            var fields = new List<string>();
            if (options.Temperature.HasValue) fields.Add("\"temperature\":" + JsonText.Number(options.Temperature.Value));
            if (options.MaxTokens.HasValue) fields.Add("\"num_predict\":" + options.MaxTokens.Value);
            if (contextLength > 0) fields.Add("\"num_ctx\":" + contextLength);
            if (fields.Count == 0) return;
            json.Append(",\"options\":{").Append(string.Join(",", fields)).Append('}');
        }

        public static OllamaChunk ParseChunk(string line)
        {
            Response response;
            try
            {
                response = JsonUtility.FromJson<Response>(line);
            }
            catch (ArgumentException e)
            {
                return new OllamaChunk(null, true, $"Invalid response from server: {e.Message}", null);
            }

            if (response == null) return new OllamaChunk(null, true, "Empty response from server.", null);
            if (!string.IsNullOrEmpty(response.error)) return new OllamaChunk(null, true, response.error, null);

            // JsonUtility turns omitted fields into 0; Ollama omits the prompt counts on a full cache hit.
            var usage = response.done
                ? new ChatUsage(
                    response.prompt_eval_count > 0 ? response.prompt_eval_count : (int?)null,
                    response.eval_count > 0 ? response.eval_count : (int?)null,
                    response.prompt_eval_duration > 0
                        ? TimeSpan.FromTicks(response.prompt_eval_duration / NanosecondsPerTick)
                        : (TimeSpan?)null)
                : null;
            return new OllamaChunk(response.message?.content, response.done, null, usage);
        }

        /// <summary>Maps the outcome of a request to an error, or null on success.</summary>
        public static ChatError Classify(
            bool cancelled, bool timedOut, string streamError, UnityWebRequest.Result result, long responseCode, string model)
        {
            if (cancelled) return new ChatError(ChatErrorKind.Cancelled, "The request was cancelled.");
            if (timedOut && result != UnityWebRequest.Result.Success)
                return new ChatError(ChatErrorKind.Timeout, "Ollama did not finish within the timeout.");
            if (streamError != null) return new ChatError(ChatErrorKind.ServerError, streamError);
            if (result == UnityWebRequest.Result.ConnectionError)
                return new ChatError(ChatErrorKind.ConnectionFailed, "Could not connect to Ollama. Is `ollama serve` running?");
            // Ollama answers 404 for an unknown model; DownloadHandlerScript does not receive the error body.
            if (responseCode == 404)
                return new ChatError(ChatErrorKind.ModelNotFound, $"Model '{model}' was not found. Run `ollama pull {model}`.");
            if (result != UnityWebRequest.Result.Success)
                return new ChatError(ChatErrorKind.ServerError, $"Request failed with HTTP {responseCode}.");
            return null;
        }
    }
}
