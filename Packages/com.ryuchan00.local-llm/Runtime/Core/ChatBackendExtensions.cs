using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocalLLM
{
    public static class ChatBackendExtensions
    {
        /// <summary>
        /// Task-based Send. Throws ChatException on failure. When the token is cancelled, the task is
        /// cancelled once the backend has stopped, so the backend is idle again when the await returns.
        /// Call it from the main thread; cancellation from other threads is moved back to it.
        /// </summary>
        public static Task<ChatResult> SendAsync(
            this IChatBackend backend,
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options = null,
            Action<string> onToken = null,
            CancellationToken cancellationToken = default)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));

            var source = new TaskCompletionSource<ChatResult>();
            if (cancellationToken.IsCancellationRequested)
            {
                source.SetCanceled();
                return source.Task;
            }

            var registration = default(CancellationTokenRegistration);
            backend.Send(
                messages,
                options ?? ChatOptions.Default,
                token => onToken?.Invoke(token),
                result =>
                {
                    registration.Dispose();
                    source.TrySetResult(result);
                },
                error =>
                {
                    registration.Dispose();
                    if (error.Kind == ChatErrorKind.Cancelled && cancellationToken.IsCancellationRequested)
                        source.TrySetCanceled();
                    else
                        source.TrySetException(new ChatException(error));
                });

            // The backend may have finished synchronously (e.g. Busy); nothing left to cancel then.
            if (source.Task.IsCompleted) return source.Task;

            // Backends must be called on the main thread, but a token can fire on any thread
            // (CancelAfter uses a timer). Post the cancel to the context SendAsync was called on.
            var context = SynchronizationContext.Current;
            var callerThread = Thread.CurrentThread.ManagedThreadId;
            registration = cancellationToken.Register(() =>
            {
                if (context == null || Thread.CurrentThread.ManagedThreadId == callerThread) backend.Cancel();
                else context.Post(_ => backend.Cancel(), null);
            });
            return source.Task;
        }
    }
}
