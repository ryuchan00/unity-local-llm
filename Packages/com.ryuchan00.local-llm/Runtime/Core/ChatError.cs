using System;

namespace LocalLLM
{
    public enum ChatErrorKind
    {
        /// <summary>Cancel() was called.</summary>
        Cancelled,
        /// <summary>A reply is already being generated.</summary>
        Busy,
        /// <summary>Send() was called before the model finished loading.</summary>
        NotReady,
        /// <summary>The request was invalid (empty model name, bad server URL, ...).</summary>
        InvalidRequest,
        /// <summary>Could not reach the Ollama server.</summary>
        ConnectionFailed,
        /// <summary>The request took longer than the configured timeout.</summary>
        Timeout,
        /// <summary>The server does not have the requested model.</summary>
        ModelNotFound,
        /// <summary>The server returned an error.</summary>
        ServerError,
        /// <summary>The browser has no usable WebGPU.</summary>
        WebGPUUnsupported,
        /// <summary>The model could not be downloaded or initialized.</summary>
        LoadFailed,
        /// <summary>The engine failed while generating.</summary>
        GenerationFailed,
        /// <summary>Checking or deleting the browser's model cache failed.</summary>
        CacheFailed,
    }

    /// <summary>Why a request failed. Detail is English text meant for logs, not for players.</summary>
    public sealed class ChatError
    {
        public ChatError(ChatErrorKind kind, string detail)
        {
            Kind = kind;
            Detail = string.IsNullOrEmpty(detail) ? kind.ToString() : detail;
        }

        public ChatErrorKind Kind { get; }
        public string Detail { get; }

        public override string ToString() => $"{Kind}: {Detail}";
    }

    /// <summary>Thrown by the async API when a request fails.</summary>
    public sealed class ChatException : Exception
    {
        public ChatException(ChatError error) : base(error?.ToString())
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public ChatError Error { get; }
    }
}
