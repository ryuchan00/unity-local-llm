using System;
using System.Collections.Generic;

namespace LocalLLM
{
    /// <summary>
    /// An inference engine the game can talk to.
    /// All callbacks run on the main thread. After Send, onToken is called zero or more times,
    /// then exactly one of onComplete / onError. Exceptions thrown by callbacks are logged, not rethrown.
    /// Dispose stops any reply without calling its callbacks and releases the model.
    /// </summary>
    public interface IChatBackend : IDisposable
    {
        /// <summary>True once Send can be called (model loaded).</summary>
        bool IsReady { get; }

        /// <summary>True while loading the model or generating a reply.</summary>
        bool IsBusy { get; }

        /// <summary>Human readable progress, e.g. while downloading the model. Empty when idle.</summary>
        string Status { get; }

        /// <summary>Why the last Load failed, or null.</summary>
        ChatError LoadError { get; }

        /// <summary>Starts loading the model. No-op when already ready or loading.</summary>
        void Load();

        void Send(
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options,
            Action<string> onToken,
            Action<ChatResult> onComplete,
            Action<ChatError> onError);

        /// <summary>
        /// Stops the current reply. Its onError receives ChatErrorKind.Cancelled once the engine has stopped,
        /// which can be a frame later; IsBusy stays true until then. If the reply had already finished,
        /// onComplete is called as usual.
        /// </summary>
        void Cancel();
    }
}
