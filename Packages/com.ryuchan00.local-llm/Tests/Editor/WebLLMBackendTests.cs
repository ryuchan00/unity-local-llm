using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LocalLLM.Tests
{
    public class WebLLMBackendTests
    {
        const string Model = "Qwen2.5-1.5B-Instruct-q4f16_1-MLC";
        static readonly ChatMessage[] OneMessage = { new ChatMessage(ChatRole.User, "hi") };

        FakeWebLLMBridge bridge;
        WebLLMBackend backend;
        List<string> tokens;
        ChatResult result;
        ChatError error;

        [SetUp]
        public void SetUp()
        {
            bridge = new FakeWebLLMBridge();
            backend = new WebLLMBackend(Model, "https://example.com/local-llm/", null, bridge);
            tokens = new List<string>();
            result = null;
            error = null;
        }

        [TearDown]
        public void TearDown() => backend.Dispose();

        void Send(ChatOptions options = null) =>
            backend.Send(OneMessage, options ?? ChatOptions.Default, tokens.Add, r => result = r, e => error = e);

        void LoadToReady()
        {
            backend.Load();
            bridge.Emit(WebLLMEvent.Ready, Model);
        }

        [Test]
        public void Constructor_PassesSettingsToBridge()
        {
            Assert.AreEqual("https://example.com/local-llm/", bridge.BaseUrl);
            Assert.AreEqual(string.Empty, bridge.AppConfigJson);
        }

        [Test]
        public void Load_WithoutWebGPU_FailsWithoutCallingInit()
        {
            bridge.WebGPUSupported = false;

            backend.Load();

            Assert.AreEqual(ChatErrorKind.WebGPUUnsupported, backend.LoadError.Kind);
            Assert.IsFalse(backend.IsBusy);
            CollectionAssert.IsEmpty(bridge.Calls);
        }

        [Test]
        public void Load_ReportsProgressThenReady()
        {
            backend.Load();
            Assert.IsTrue(backend.IsBusy);
            Assert.AreEqual(new[] { "Init:" + Model }, bridge.Calls);

            bridge.Emit(WebLLMEvent.Progress, "Fetching 10%");
            Assert.AreEqual("Fetching 10%", backend.Status);

            bridge.Emit(WebLLMEvent.Ready, Model);
            Assert.IsTrue(backend.IsReady);
            Assert.IsFalse(backend.IsBusy);
            Assert.AreEqual(string.Empty, backend.Status);
        }

        [Test]
        public void Load_TwiceWhileLoading_InitsOnce()
        {
            backend.Load();
            backend.Load();

            Assert.AreEqual(1, bridge.Calls.Count);
        }

        [Test]
        public void Load_Failure_AllowsRetry()
        {
            backend.Load();
            LogAssert.Expect(LogType.Error, "[LocalLLM] WebLLM load failed: network");
            bridge.Emit(WebLLMEvent.LoadError, "network");

            Assert.AreEqual(ChatErrorKind.LoadFailed, backend.LoadError.Kind);
            Assert.IsFalse(backend.IsBusy);

            backend.Load();
            Assert.AreEqual(2, bridge.Calls.Count);
            Assert.IsNull(backend.LoadError);
        }

        [Test]
        public void Send_BeforeReady_FailsWithNotReady()
        {
            Send();

            Assert.AreEqual(ChatErrorKind.NotReady, error.Kind);
            CollectionAssert.DoesNotContain(bridge.Calls, "Chat");
        }

        [Test]
        public void Send_StreamsTokensThenCompletes()
        {
            LoadToReady();

            Send();
            bridge.Emit(WebLLMEvent.Token, "Hel");
            bridge.Emit(WebLLMEvent.Token, "lo");
            bridge.Emit(WebLLMEvent.Complete, "{\"text\":\"Hello\",\"has_usage\":true,\"prompt_tokens\":5,\"completion_tokens\":2,\"time_to_first_token_s\":0.1}");

            Assert.AreEqual(new[] { "Hel", "lo" }, tokens);
            Assert.AreEqual("Hello", result.Text);
            Assert.AreEqual(5, result.Usage.PromptTokens);
            Assert.IsNull(error);
            Assert.IsFalse(backend.IsBusy);
        }

        [Test]
        public void Send_WhileGenerating_FailsWithBusy()
        {
            LoadToReady();
            Send();

            ChatError second = null;
            backend.Send(OneMessage, ChatOptions.Default, _ => { }, _ => { }, e => second = e);

            Assert.AreEqual(ChatErrorKind.Busy, second.Kind);
            Assert.IsNull(error);
        }

        [Test]
        public void Cancel_ReportsCancelledWhenEngineStops()
        {
            LoadToReady();
            Send();

            backend.Cancel();
            bridge.Emit(WebLLMEvent.Complete, "{\"text\":\"partial\",\"has_usage\":false}");

            CollectionAssert.Contains(bridge.Calls, "Cancel");
            Assert.AreEqual(ChatErrorKind.Cancelled, error.Kind);
            Assert.IsNull(result);
        }

        [Test]
        public void ChatError_ReportsGenerationFailedOnce()
        {
            LoadToReady();
            Send();

            LogAssert.Expect(LogType.Error, "[LocalLLM] WebLLM generation failed: out of memory");
            bridge.Emit(WebLLMEvent.ChatError, "out of memory");
            bridge.Emit(WebLLMEvent.Complete, "{\"text\":\"late\"}");

            Assert.AreEqual(ChatErrorKind.GenerationFailed, error.Kind);
            Assert.IsNull(result);
        }

        [Test]
        public void ChatError_AfterCancel_ReportsCancelled()
        {
            LoadToReady();
            Send();

            backend.Cancel();
            bridge.Emit(WebLLMEvent.ChatError, "interrupted");

            Assert.AreEqual(ChatErrorKind.Cancelled, error.Kind);
        }

        [Test]
        public void ThrowingTokenCallback_IsLoggedAndGenerationContinues()
        {
            LoadToReady();
            backend.Send(OneMessage, ChatOptions.Default,
                _ => throw new System.InvalidOperationException("user bug"), r => result = r, e => error = e);

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: user bug");
            bridge.Emit(WebLLMEvent.Token, "a");
            bridge.Emit(WebLLMEvent.Complete, "{\"text\":\"a\"}");

            Assert.AreEqual("a", result.Text);
            Assert.IsNull(error);
        }

        [Test]
        public void Dispose_UnloadsAndIgnoresLaterEvents()
        {
            LoadToReady();
            Send();

            backend.Dispose();
            bridge.Emit(WebLLMEvent.Complete, "{\"text\":\"late\"}");

            CollectionAssert.Contains(bridge.Calls, "Unload");
            Assert.IsNull(result);
            Assert.IsFalse(backend.IsReady);
            Assert.IsFalse(backend.IsBusy);
        }

        [Test]
        public void Send_AfterDispose_Throws()
        {
            backend.Dispose();

            Assert.Throws<System.ObjectDisposedException>(() => Send());
        }

        [Test]
        public void CacheOperation_WithNullCallback_DoesNotWedge()
        {
            Assert.Throws<System.ArgumentNullException>(() => backend.IsModelCached(null, _ => { }));

            bool? cached = null;
            backend.IsModelCached(c => cached = c, e => Assert.Fail(e.Detail));
            bridge.Emit(WebLLMEvent.CacheStatus, "false");

            Assert.IsFalse(cached);
        }

        [Test]
        public void Tokens_AfterCompletion_AreIgnored()
        {
            LoadToReady();
            Send();
            bridge.Emit(WebLLMEvent.Complete, "{\"text\":\"\"}");

            bridge.Emit(WebLLMEvent.Token, "late");

            CollectionAssert.IsEmpty(tokens);
        }

        [Test]
        public void IsModelCached_ReportsResult()
        {
            bool? cached = null;

            backend.IsModelCached(c => cached = c, e => Assert.Fail(e.Detail));
            bridge.Emit(WebLLMEvent.CacheStatus, "true");

            Assert.AreEqual(new[] { "CheckCache:" + Model }, bridge.Calls);
            Assert.IsTrue(cached);
        }

        [Test]
        public void DeleteCachedModel_ReportsDoneOrError()
        {
            var done = false;
            ChatError cacheError = null;

            backend.DeleteCachedModel(() => done = true, e => cacheError = e);
            bridge.Emit(WebLLMEvent.CacheError, "denied");

            Assert.IsFalse(done);
            Assert.AreEqual(ChatErrorKind.CacheFailed, cacheError.Kind);
        }
    }
}
