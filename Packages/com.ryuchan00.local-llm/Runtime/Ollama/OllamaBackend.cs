using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LocalLLM
{
    /// <summary>
    /// Streams replies from an Ollama server. Intended for the Editor and standalone builds.
    /// Ollama has no authentication: keep it on localhost rather than exposing it to the network.
    /// </summary>
    public sealed class OllamaBackend : IChatBackend
    {
        readonly string serverUrl;
        readonly string model;
        readonly string keepAlive;
        readonly int contextLength;
        readonly int timeoutSeconds;
        UnityWebRequest current;
        bool cancelled;
        bool disposed;
        bool warnedCleartext;

        /// <param name="timeoutSeconds">Gives up after this many seconds, including model loading. 0 waits forever.</param>
        public OllamaBackend(
            string serverUrl = LocalLLMSettings.DefaultOllamaUrl,
            string model = LocalLLMSettings.DefaultOllamaModel,
            string keepAlive = null,
            int contextLength = 0,
            int timeoutSeconds = 600)
        {
            if (contextLength < 0) throw new ArgumentOutOfRangeException(nameof(contextLength));
            if (timeoutSeconds < 0) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            this.serverUrl = serverUrl;
            this.model = model;
            this.keepAlive = keepAlive;
            this.contextLength = contextLength;
            this.timeoutSeconds = timeoutSeconds;
        }

        public bool IsReady => !disposed;
        public bool IsBusy => current != null;
        public string Status => string.Empty;
        public ChatError LoadError => null;

        /// <summary>Nothing to load: Ollama loads the model on the first request.</summary>
        public void Load() { }

        public void Send(
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options,
            Action<string> onToken,
            Action<ChatResult> onComplete,
            Action<ChatError> onError)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OllamaBackend));
            if (onToken == null) throw new ArgumentNullException(nameof(onToken));
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
            if (onError == null) throw new ArgumentNullException(nameof(onError));

            if (IsBusy)
            {
                SafeCallback.Invoke(onError, new ChatError(ChatErrorKind.Busy, "A reply is already being generated."));
                return;
            }

            string endpoint, body;
            try
            {
                endpoint = OllamaProtocol.ChatEndpoint(serverUrl);
                body = OllamaProtocol.BuildRequestJson(model, messages, options, keepAlive, contextLength);
            }
            catch (ArgumentException e)
            {
                SafeCallback.Invoke(onError, new ChatError(ChatErrorKind.InvalidRequest, e.Message));
                return;
            }
            WarnIfCleartextRemote();

            var reply = new StringBuilder();
            ChatUsage usage = null;
            string streamError = null;
            void HandleLine(string line)
            {
                if (disposed) return;
                var chunk = OllamaProtocol.ParseChunk(line);
                if (chunk.Error != null) { streamError = chunk.Error; return; }
                if (chunk.Usage != null) usage = chunk.Usage;
                if (chunk.Content.Length == 0) return;
                reply.Append(chunk.Content);
                SafeCallback.Invoke(onToken, chunk.Content);
            }

            var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new StreamingLineDownloadHandler(HandleLine),
                timeout = timeoutSeconds,
            };
            request.SetRequestHeader("Content-Type", "application/json");

            UnityWebRequestAsyncOperation operation;
            try
            {
                operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
                request.Dispose();
                SafeCallback.Invoke(onError, new ChatError(ChatErrorKind.ConnectionFailed, e.Message));
                return;
            }

            current = request;
            cancelled = false;
            var startedAt = Time.realtimeSinceStartup;
            operation.completed += _ =>
            {
                var timedOut = timeoutSeconds > 0 && Time.realtimeSinceStartup - startedAt >= timeoutSeconds - 1;
                var error = OllamaProtocol.Classify(
                    cancelled, timedOut, streamError, request.result, request.responseCode, model);
                if (error?.Kind == ChatErrorKind.ConnectionFailed || error?.Kind == ChatErrorKind.ServerError)
                    // Log the endpoint and status only; never the conversation.
                    Debug.LogWarning($"[LocalLLM] Ollama request failed: {request.responseCode} {request.error} ({endpoint})");

                current = null;
                request.Dispose();
                if (disposed) return;
                if (error != null) SafeCallback.Invoke(onError, error);
                else SafeCallback.Invoke(onComplete, new ChatResult(reply.ToString(), usage));
            };
        }

        public void Cancel()
        {
            // A finished request delivers its reply; there is nothing left to stop.
            if (current == null || current.isDone) return;
            cancelled = true;
            current.Abort();
        }

        public void Dispose()
        {
            if (disposed) return;
            Cancel();
            disposed = true;
        }

        void WarnIfCleartextRemote()
        {
            if (warnedCleartext || !OllamaProtocol.IsCleartextRemote(serverUrl)) return;
            warnedCleartext = true;
            Debug.LogWarning("[LocalLLM] Talking to a remote Ollama over plain http: the conversation is not encrypted. Use https or localhost.");
        }
    }
}
