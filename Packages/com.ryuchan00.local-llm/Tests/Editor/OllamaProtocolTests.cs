using System;
using NUnit.Framework;
using UnityEngine.Networking;

namespace LocalLLM.Tests
{
    public class OllamaProtocolTests
    {
        static readonly ChatMessage[] OneMessage = { new ChatMessage(ChatRole.User, "x") };

        [Test]
        public void BuildRequestJson_ContainsModelMessagesAndStreamFlag()
        {
            var messages = new[]
            {
                new ChatMessage(ChatRole.System, "You are a guide."),
                new ChatMessage(ChatRole.User, "Hello \"quoted\" こんにちは"),
            };

            var json = OllamaProtocol.BuildRequestJson("llama3.2:3b", messages, ChatOptions.Default);

            StringAssert.Contains("\"model\":\"llama3.2:3b\"", json);
            StringAssert.Contains("\"stream\":true", json);
            StringAssert.Contains("\"think\":false", json);
            StringAssert.Contains("{\"role\":\"system\",\"content\":\"You are a guide.\"}", json);
            StringAssert.Contains("{\"role\":\"user\",\"content\":\"Hello \\\"quoted\\\" こんにちは\"}", json);
        }

        [Test]
        public void BuildRequestJson_WithoutOverrides_HasNoOptionalFields()
        {
            var json = OllamaProtocol.BuildRequestJson("m", OneMessage, ChatOptions.Default);

            StringAssert.DoesNotContain("format", json);
            StringAssert.DoesNotContain("options", json);
            StringAssert.DoesNotContain("keep_alive", json);
        }

        [Test]
        public void BuildRequestJson_MapsOptions()
        {
            var options = ChatOptions.Default.WithTemperature(0.5f).WithMaxTokens(32).WithThink(true);

            var json = OllamaProtocol.BuildRequestJson("m", OneMessage, options, keepAlive: "30m", contextLength: 4096);

            StringAssert.Contains("\"think\":true", json);
            StringAssert.Contains("\"keep_alive\":\"30m\"", json);
            StringAssert.Contains("\"options\":{\"temperature\":0.5,\"num_predict\":32,\"num_ctx\":4096}", json);
        }

        [Test]
        public void BuildRequestJson_WithSchema_AddsFormatObject()
        {
            var options = ChatOptions.Default.WithJsonSchema("{\"type\":\"object\"}");

            var json = OllamaProtocol.BuildRequestJson("m", OneMessage, options);

            StringAssert.Contains("\"format\":{\"type\":\"object\"}", json);
        }

        [Test]
        public void BuildRequestJson_RejectsEmptyModel()
        {
            Assert.Throws<ArgumentException>(() => OllamaProtocol.BuildRequestJson(" ", OneMessage, ChatOptions.Default));
        }

        [Test]
        public void BuildRequestJson_RejectsNullMessage()
        {
            Assert.Throws<ArgumentException>(
                () => OllamaProtocol.BuildRequestJson("m", new ChatMessage[] { null }, ChatOptions.Default));
        }

        [Test]
        public void ParseChunk_ContentChunk()
        {
            var chunk = OllamaProtocol.ParseChunk(
                "{\"model\":\"m\",\"message\":{\"role\":\"assistant\",\"content\":\"やあ\"},\"done\":false}");

            Assert.AreEqual("やあ", chunk.Content);
            Assert.IsFalse(chunk.Done);
            Assert.IsNull(chunk.Error);
            Assert.IsNull(chunk.Usage);
        }

        [Test]
        public void ParseChunk_DoneChunk_HasUsage()
        {
            var chunk = OllamaProtocol.ParseChunk(
                "{\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"done\":true,\"done_reason\":\"stop\"," +
                "\"prompt_eval_count\":120,\"prompt_eval_duration\":250000000,\"eval_count\":7}");

            Assert.IsTrue(chunk.Done);
            Assert.AreEqual(string.Empty, chunk.Content);
            Assert.AreEqual(120, chunk.Usage.PromptTokens);
            Assert.AreEqual(7, chunk.Usage.CompletionTokens);
            Assert.AreEqual(TimeSpan.FromMilliseconds(250), chunk.Usage.PromptTime);
        }

        [Test]
        public void ParseChunk_ErrorChunk()
        {
            var chunk = OllamaProtocol.ParseChunk("{\"error\":\"model 'x' not found\"}");

            Assert.AreEqual("model 'x' not found", chunk.Error);
        }

        [Test]
        public void ParseChunk_InvalidJson_ReturnsError()
        {
            var chunk = OllamaProtocol.ParseChunk("not json");

            Assert.IsNotNull(chunk.Error);
        }

        [Test]
        public void ParseChunk_DoneChunkWithoutCounts_HasUnknownUsage()
        {
            // Ollama omits prompt_eval_count when the whole prompt came from its cache.
            var chunk = OllamaProtocol.ParseChunk("{\"message\":{\"content\":\"\"},\"done\":true,\"eval_count\":3}");

            Assert.IsNull(chunk.Usage.PromptTokens);
            Assert.IsNull(chunk.Usage.PromptTime);
            Assert.AreEqual(3, chunk.Usage.CompletionTokens);
        }

        [Test]
        public void Classify_Success_ReturnsNull()
        {
            Assert.IsNull(OllamaProtocol.Classify(false, false, null, UnityWebRequest.Result.Success, 200, "m"));
        }

        [Test]
        public void Classify_TimedOut_ReturnsTimeout()
        {
            var error = OllamaProtocol.Classify(false, true, null, UnityWebRequest.Result.ConnectionError, 0, "m");

            Assert.AreEqual(ChatErrorKind.Timeout, error.Kind);
        }

        [TestCase("http://localhost:11434", false)]
        [TestCase("http://127.0.0.1:11434", false)]
        [TestCase("http://[::1]:11434", false)]
        [TestCase("https://ollama.example.com", false)]
        [TestCase("http://192.168.1.10:11434", true)]
        [TestCase("http://ollama.example.com", true)]
        public void IsCleartextRemote_FlagsPlainHttpToOtherHosts(string url, bool expected)
        {
            Assert.AreEqual(expected, OllamaProtocol.IsCleartextRemote(url));
        }

        [TestCase(true, null, UnityWebRequest.Result.ConnectionError, 0, ChatErrorKind.Cancelled)]
        [TestCase(false, "boom", UnityWebRequest.Result.Success, 200, ChatErrorKind.ServerError)]
        [TestCase(false, null, UnityWebRequest.Result.ConnectionError, 0, ChatErrorKind.ConnectionFailed)]
        [TestCase(false, null, UnityWebRequest.Result.ProtocolError, 404, ChatErrorKind.ModelNotFound)]
        [TestCase(false, null, UnityWebRequest.Result.ProtocolError, 500, ChatErrorKind.ServerError)]
        public void Classify_MapsFailures(
            bool cancelled, string streamError, UnityWebRequest.Result result, long code, ChatErrorKind expected)
        {
            var error = OllamaProtocol.Classify(cancelled, false, streamError, result, code, "m");

            Assert.AreEqual(expected, error.Kind);
            Assert.IsNotEmpty(error.Detail);
        }

        [Test]
        public void ValidateServerUrl_AcceptsHttpAndHttps()
        {
            Assert.AreEqual("http://localhost:11434/api/chat", OllamaProtocol.ChatEndpoint("http://localhost:11434/"));
            Assert.AreEqual("https://example.com/api/chat", OllamaProtocol.ChatEndpoint("https://example.com"));
        }

        [TestCase("file:///etc/passwd")]
        [TestCase("ftp://localhost")]
        [TestCase("localhost:11434")]
        [TestCase("")]
        public void ValidateServerUrl_RejectsOthers(string url)
        {
            Assert.Throws<ArgumentException>(() => OllamaProtocol.ChatEndpoint(url));
        }
    }
}
