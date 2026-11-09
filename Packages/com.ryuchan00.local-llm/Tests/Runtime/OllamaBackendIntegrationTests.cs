using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace LocalLLM.Tests
{
    /// <summary>
    /// Talks to a real Ollama server. Skipped when none is running at the default URL.
    /// Set LOCALLLM_TEST_MODEL to choose the model (default: llama3.2:3b).
    /// </summary>
    [Category("Integration")]
    public class OllamaBackendIntegrationTests
    {
        static string Model => System.Environment.GetEnvironmentVariable("LOCALLLM_TEST_MODEL") ?? LocalLLMSettings.DefaultOllamaModel;

        [UnitySetUp]
        public IEnumerator SkipWithoutServer()
        {
            using (var request = UnityWebRequest.Get(LocalLLMSettings.DefaultOllamaUrl + "/api/version"))
            {
                request.timeout = 2;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    Assert.Ignore("Ollama is not running at " + LocalLLMSettings.DefaultOllamaUrl);
            }
        }

        static IEnumerator WaitUntilIdle(IChatBackend backend)
        {
            var deadline = Time.realtimeSinceStartup + 120f;
            while (backend.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(backend.IsBusy, "Timed out waiting for Ollama.");
        }

        [UnityTest]
        public IEnumerator Send_StreamsTokensAndReportsUsage()
        {
            var backend = new OllamaBackend(model: Model, keepAlive: "5m");
            var tokens = new List<string>();
            ChatResult result = null;
            ChatError error = null;
            var messages = new[] { new ChatMessage(ChatRole.User, "Reply with just the word: yes") };

            backend.Send(messages, ChatOptions.Default.WithMaxTokens(8).WithTemperature(0f),
                tokens.Add, r => result = r, e => error = e);
            yield return WaitUntilIdle(backend);

            Assert.IsNull(error, error?.ToString());
            Assert.IsNotEmpty(tokens);
            Assert.AreEqual(string.Concat(tokens), result.Text);
            Assert.Greater(result.Usage.PromptTokens, 0);
            Assert.Greater(result.Usage.CompletionTokens, 0);
        }

        [UnityTest]
        public IEnumerator Send_WithSchema_ReturnsJson()
        {
            var backend = new OllamaBackend(model: Model);
            ChatResult result = null;
            ChatError error = null;
            var options = ChatOptions.Default.WithTemperature(0f).WithJsonSchema(
                "{\"type\":\"object\",\"properties\":{\"answer\":{\"type\":\"string\"}},\"required\":[\"answer\"]}");
            var messages = new[] { new ChatMessage(ChatRole.User, "What color is the sky? Answer in JSON.") };

            backend.Send(messages, options, _ => { }, r => result = r, e => error = e);
            yield return WaitUntilIdle(backend);

            Assert.IsNull(error, error?.ToString());
            StringAssert.StartsWith("{", result.Text.Trim());
            StringAssert.Contains("\"answer\"", result.Text);
        }

        [UnityTest]
        public IEnumerator Cancel_ReportsCancelled()
        {
            var backend = new OllamaBackend(model: Model);
            ChatError error = null;
            ChatResult result = null;
            var messages = new[] { new ChatMessage(ChatRole.User, "Count from 1 to 500, one number per line.") };

            backend.Send(messages, ChatOptions.Default, _ => backend.Cancel(), r => result = r, e => error = e);
            yield return WaitUntilIdle(backend);

            Assert.IsNull(result);
            Assert.AreEqual(ChatErrorKind.Cancelled, error.Kind);
        }

        [UnityTest]
        public IEnumerator Send_UnknownModel_ReportsModelNotFound()
        {
            var backend = new OllamaBackend(model: "no-such-model:0b");
            ChatError error = null;

            backend.Send(new[] { new ChatMessage(ChatRole.User, "hi") }, ChatOptions.Default,
                _ => { }, _ => { }, e => error = e);
            yield return WaitUntilIdle(backend);

            Assert.AreEqual(ChatErrorKind.ModelNotFound, error.Kind);
        }

        [UnityTest]
        public IEnumerator Send_ServerDown_ReportsConnectionFailed()
        {
            var backend = new OllamaBackend("http://127.0.0.1:9", Model);
            ChatError error = null;

            backend.Send(new[] { new ChatMessage(ChatRole.User, "hi") }, ChatOptions.Default,
                _ => { }, _ => { }, e => error = e);
            yield return WaitUntilIdle(backend);

            Assert.AreEqual(ChatErrorKind.ConnectionFailed, error.Kind);
        }

        [Test]
        public void Send_FileUrl_IsRejectedWithoutRequest()
        {
            var backend = new OllamaBackend("file:///etc/passwd", Model);
            ChatError error = null;

            backend.Send(new[] { new ChatMessage(ChatRole.User, "hi") }, ChatOptions.Default,
                _ => { }, _ => { }, e => error = e);

            Assert.AreEqual(ChatErrorKind.InvalidRequest, error.Kind);
            Assert.IsFalse(backend.IsBusy);
        }
    }
}
