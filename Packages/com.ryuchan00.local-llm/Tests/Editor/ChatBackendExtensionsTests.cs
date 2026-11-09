using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace LocalLLM.Tests
{
    public class ChatBackendExtensionsTests
    {
        sealed class ManualBackend : IChatBackend
        {
            public Action<string> OnToken;
            public Action<ChatResult> OnComplete;
            public Action<ChatError> OnError;
            public int CancelCount;

            public bool IsReady => true;
            public bool IsBusy => OnComplete != null;
            public string Status => string.Empty;
            public ChatError LoadError => null;
            public void Load() { }
            public void Dispose() { }

            public void Send(IReadOnlyList<ChatMessage> messages, ChatOptions options,
                Action<string> onToken, Action<ChatResult> onComplete, Action<ChatError> onError)
            {
                OnToken = onToken;
                OnComplete = onComplete;
                OnError = onError;
            }

            public void Cancel()
            {
                CancelCount++;
                OnError?.Invoke(new ChatError(ChatErrorKind.Cancelled, "cancelled"));
            }
        }

        sealed class QueueContext : SynchronizationContext
        {
            readonly Queue<(SendOrPostCallback callback, object state)> posted =
                new Queue<(SendOrPostCallback callback, object state)>();

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (posted) posted.Enqueue((d, state));
            }

            public void RunAll()
            {
                while (true)
                {
                    (SendOrPostCallback callback, object state) item;
                    lock (posted)
                    {
                        if (posted.Count == 0) return;
                        item = posted.Dequeue();
                    }
                    item.callback(item.state);
                }
            }
        }

        static readonly ChatMessage[] OneMessage = { new ChatMessage(ChatRole.User, "hi") };

        [Test]
        public void SendAsync_CompletesWithResult()
        {
            var backend = new ManualBackend();
            var tokens = new List<string>();

            var task = backend.SendAsync(OneMessage, null, tokens.Add);
            backend.OnToken("a");
            backend.OnComplete(new ChatResult("a", ChatUsage.Unknown));

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual("a", task.Result.Text);
            Assert.AreEqual(new[] { "a" }, tokens);
        }

        [Test]
        public void SendAsync_FailsWithChatException()
        {
            var backend = new ManualBackend();

            var task = backend.SendAsync(OneMessage);
            backend.OnError(new ChatError(ChatErrorKind.ConnectionFailed, "down"));

            var exception = Assert.Throws<AggregateException>(() => task.Wait()).InnerException as ChatException;
            Assert.IsNotNull(exception);
            Assert.AreEqual(ChatErrorKind.ConnectionFailed, exception.Error.Kind);
        }

        [Test]
        public void SendAsync_CancellationTokenCancelsBackend()
        {
            var backend = new ManualBackend();
            using (var cts = new CancellationTokenSource())
            {
                var task = backend.SendAsync(OneMessage, cancellationToken: cts.Token);
                cts.Cancel();

                Assert.AreEqual(1, backend.CancelCount);
                Assert.IsTrue(task.IsCanceled);
            }
        }

        [Test]
        public void SendAsync_CancelFromOtherThread_RunsOnCapturedContext()
        {
            var backend = new ManualBackend();
            var context = new QueueContext();
            var previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                using (var cts = new CancellationTokenSource())
                {
                    var task = backend.SendAsync(OneMessage, cancellationToken: cts.Token);

                    System.Threading.Tasks.Task.Run(() => cts.Cancel()).Wait();
                    Assert.AreEqual(0, backend.CancelCount, "Cancel must not run on the worker thread.");
                    Assert.IsFalse(task.IsCompleted, "The task waits for the backend to stop.");

                    context.RunAll();
                    Assert.AreEqual(1, backend.CancelCount);
                    Assert.IsTrue(task.IsCanceled);
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        [Test]
        public void SendAsync_AlreadyCancelledToken_DoesNotSend()
        {
            var backend = new ManualBackend();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                var task = backend.SendAsync(OneMessage, cancellationToken: cts.Token);

                Assert.IsTrue(task.IsCanceled);
                Assert.IsNull(backend.OnComplete);
            }
        }
    }
}
