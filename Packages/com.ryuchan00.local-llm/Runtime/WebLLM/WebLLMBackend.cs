using System;
using System.Collections.Generic;
using UnityEngine;

namespace LocalLLM
{
    /// <summary>
    /// Runs the model inside the browser with WebLLM (WebGPU). WebGL builds only.
    /// The model is downloaded on the first Load and cached by the browser.
    /// Only one instance can be active at a time: Dispose the old one before creating another.
    /// </summary>
    public sealed class WebLLMBackend : IChatBackend
    {
        /// <summary>Folder in StreamingAssets the build step copies WebLLM into.</summary>
        public const string StreamingAssetsFolder = "local-llm";

        static WebLLMBackend active;

        readonly string modelId;
        readonly IWebLLMBridge bridge;

        bool loading;
        bool generating;
        bool cancelled;
        bool disposed;
        Action<string> onToken;
        Action<ChatResult> onComplete;
        Action<ChatError> onError;
        Action<bool> onCacheStatus;
        Action onCacheDeleted;
        Action<ChatError> onCacheError;

        /// <param name="modelId">A WebLLM model id, e.g. "Qwen2.5-1.5B-Instruct-q4f16_1-MLC".</param>
        /// <param name="baseUrl">Where local-llm-loader.js is served. Defaults to StreamingAssets/local-llm/.</param>
        /// <param name="appConfigJson">
        /// Custom WebLLM appConfig JSON, e.g. to self-host the model. Empty uses the pinned default.
        /// Treat it like code: its model_lib is WebAssembly that runs in the page.
        /// </param>
        public WebLLMBackend(
            string modelId = LocalLLMSettings.DefaultWebLLMModel,
            string baseUrl = null,
            string appConfigJson = null)
            : this(modelId, baseUrl, appConfigJson, new WebGLWebLLMBridge())
        {
        }

        internal WebLLMBackend(string modelId, string baseUrl, string appConfigJson, IWebLLMBridge bridge)
        {
            if (string.IsNullOrWhiteSpace(modelId)) throw new ArgumentException("Model id must not be empty.", nameof(modelId));
            this.modelId = modelId.Trim();
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            if (active != null && !active.disposed)
                Debug.LogWarning("[LocalLLM] A WebLLMBackend is already active; it stops receiving events. Dispose it first.");
            active = this;

            var url = string.IsNullOrWhiteSpace(baseUrl)
                ? $"{Application.streamingAssetsPath}/{StreamingAssetsFolder}/"
                : baseUrl;
            bridge.Setup(url, appConfigJson ?? string.Empty, OnEvent);
        }

        public bool IsReady { get; private set; }
        public bool IsBusy => loading || generating;
        public string Status { get; private set; } = string.Empty;
        public ChatError LoadError { get; private set; }

        /// <summary>The model actually loaded. Differs from the requested id when falling back to the f32 variant.</summary>
        public string LoadedModelId { get; private set; }

        public void Load()
        {
            ThrowIfDisposed();
            if (IsReady || loading) return;
            LoadError = null;
            if (!bridge.IsWebGPUSupported())
            {
                LoadError = new ChatError(ChatErrorKind.WebGPUUnsupported,
                    "This browser does not support WebGPU. Use a recent desktop Chrome or Edge.");
                return;
            }
            loading = true;
            Status = "Loading WebLLM...";
            bridge.Init(modelId);
        }

        public void Send(
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options,
            Action<string> onToken,
            Action<ChatResult> onComplete,
            Action<ChatError> onError)
        {
            ThrowIfDisposed();
            if (onToken == null) throw new ArgumentNullException(nameof(onToken));
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
            if (onError == null) throw new ArgumentNullException(nameof(onError));

            if (!IsReady)
            {
                SafeCallback.Invoke(onError, new ChatError(ChatErrorKind.NotReady, "Call Load and wait for IsReady first."));
                return;
            }
            if (generating)
            {
                SafeCallback.Invoke(onError, new ChatError(ChatErrorKind.Busy, "A reply is already being generated."));
                return;
            }

            string request;
            try
            {
                request = WebLLMProtocol.BuildRequestJson(messages, options);
            }
            catch (ArgumentException e)
            {
                SafeCallback.Invoke(onError, new ChatError(ChatErrorKind.InvalidRequest, e.Message));
                return;
            }

            this.onToken = onToken;
            this.onComplete = onComplete;
            this.onError = onError;
            generating = true;
            cancelled = false;
            bridge.Chat(request);
        }

        public void Cancel()
        {
            if (!generating || disposed) return;
            cancelled = true;
            bridge.Cancel();
        }

        /// <summary>Stops any reply without calling its callbacks and frees the model from GPU memory.</summary>
        public void Dispose()
        {
            if (disposed) return;
            if (generating) bridge.Cancel();
            disposed = true;
            loading = false;
            generating = false;
            IsReady = false;
            onToken = null;
            onComplete = null;
            onError = null;
            EndCacheOperation();
            bridge.Unload();
            if (active == this) active = null;
        }

        /// <summary>Checks whether the model is already in the browser cache (no download needed).</summary>
        public void IsModelCached(Action<bool> onResult, Action<ChatError> onError)
        {
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));
            if (!BeginCacheOperation(onError)) return;
            onCacheStatus = onResult;
            bridge.CheckCache(modelId);
        }

        /// <summary>Deletes the model from the browser cache to free storage.</summary>
        public void DeleteCachedModel(Action onDone, Action<ChatError> onError)
        {
            if (onDone == null) throw new ArgumentNullException(nameof(onDone));
            if (!BeginCacheOperation(onError)) return;
            onCacheDeleted = onDone;
            bridge.DeleteCache(modelId);
        }

        bool BeginCacheOperation(Action<ChatError> onError)
        {
            ThrowIfDisposed();
            if (onError == null) throw new ArgumentNullException(nameof(onError));
            if (onCacheError != null)
            {
                SafeCallback.Invoke(onError, new ChatError(ChatErrorKind.Busy, "Another cache operation is in progress."));
                return false;
            }
            onCacheError = onError;
            return true;
        }

        void OnEvent(WebLLMEvent kind, string payload)
        {
            if (disposed) return;
            switch (kind)
            {
                case WebLLMEvent.Progress:
                    Status = payload;
                    break;
                case WebLLMEvent.Ready:
                    loading = false;
                    IsReady = true;
                    LoadedModelId = payload;
                    Status = string.Empty;
                    break;
                case WebLLMEvent.LoadError:
                    loading = false;
                    Status = string.Empty;
                    LoadError = new ChatError(ChatErrorKind.LoadFailed, payload);
                    Debug.LogError($"[LocalLLM] WebLLM load failed: {payload}");
                    break;
                case WebLLMEvent.Token:
                    if (generating) SafeCallback.Invoke(onToken, payload);
                    break;
                case WebLLMEvent.Complete:
                    if (!generating) break;
                    if (cancelled) Finish(null, new ChatError(ChatErrorKind.Cancelled, "The request was cancelled."));
                    else Finish(WebLLMProtocol.ParseComplete(payload), null);
                    break;
                case WebLLMEvent.ChatError:
                    if (!generating) break;
                    if (cancelled)
                    {
                        // Interrupting can make the engine throw; the caller asked for this.
                        Finish(null, new ChatError(ChatErrorKind.Cancelled, "The request was cancelled."));
                        break;
                    }
                    Debug.LogError($"[LocalLLM] WebLLM generation failed: {payload}");
                    Finish(null, new ChatError(ChatErrorKind.GenerationFailed, payload));
                    break;
                case WebLLMEvent.CacheStatus:
                    var status = onCacheStatus;
                    EndCacheOperation();
                    SafeCallback.Invoke(status, payload == "true");
                    break;
                case WebLLMEvent.CacheDeleted:
                    var deleted = onCacheDeleted;
                    EndCacheOperation();
                    SafeCallback.Invoke(deleted);
                    break;
                case WebLLMEvent.CacheError:
                    var failed = onCacheError;
                    EndCacheOperation();
                    SafeCallback.Invoke(failed, new ChatError(ChatErrorKind.CacheFailed, payload));
                    break;
            }
        }

        void Finish(ChatResult result, ChatError error)
        {
            var complete = onComplete;
            var fail = onError;
            generating = false;
            onToken = null;
            onComplete = null;
            onError = null;
            if (error != null) SafeCallback.Invoke(fail, error);
            else SafeCallback.Invoke(complete, result);
        }

        void EndCacheOperation()
        {
            onCacheStatus = null;
            onCacheDeleted = null;
            onCacheError = null;
        }

        void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebLLMBackend));
        }
    }
}
