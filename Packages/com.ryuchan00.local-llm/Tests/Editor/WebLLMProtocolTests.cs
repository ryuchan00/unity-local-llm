using System;
using NUnit.Framework;

namespace LocalLLM.Tests
{
    public class WebLLMProtocolTests
    {
        static readonly ChatMessage[] OneMessage = { new ChatMessage(ChatRole.User, "hi \"there\"") };

        [Test]
        public void BuildRequestJson_FreeText()
        {
            var json = WebLLMProtocol.BuildRequestJson(OneMessage, ChatOptions.Default);

            Assert.AreEqual("{\"messages\":[{\"role\":\"user\",\"content\":\"hi \\\"there\\\"\"}]}", json);
        }

        [Test]
        public void BuildRequestJson_MapsOptions()
        {
            var options = ChatOptions.Default
                .WithTemperature(0.7f)
                .WithMaxTokens(16)
                .WithJsonSchema("{\"type\":\"object\"}");

            var json = WebLLMProtocol.BuildRequestJson(OneMessage, options);

            StringAssert.Contains("\"temperature\":0.7", json);
            StringAssert.Contains("\"max_tokens\":16", json);
            // WebLLM takes the schema as a string.
            StringAssert.Contains("\"json_schema\":\"{\\\"type\\\":\\\"object\\\"}\"", json);
        }

        [Test]
        public void ParseComplete_WithUsage()
        {
            var result = WebLLMProtocol.ParseComplete(
                "{\"text\":\"done\",\"has_usage\":true,\"prompt_tokens\":12,\"completion_tokens\":3,\"time_to_first_token_s\":0.25}");

            Assert.AreEqual("done", result.Text);
            Assert.AreEqual(12, result.Usage.PromptTokens);
            Assert.AreEqual(3, result.Usage.CompletionTokens);
            Assert.AreEqual(TimeSpan.FromMilliseconds(250), result.Usage.PromptTime);
        }

        [Test]
        public void ParseComplete_WithoutUsage()
        {
            var result = WebLLMProtocol.ParseComplete("{\"text\":\"done\",\"has_usage\":false}");

            Assert.AreEqual("done", result.Text);
            Assert.IsNull(result.Usage.PromptTokens);
        }

        [Test]
        public void ParseComplete_InvalidJson_ReturnsRawText()
        {
            var result = WebLLMProtocol.ParseComplete("not json");

            Assert.AreEqual("not json", result.Text);
        }
    }
}
