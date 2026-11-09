using NUnit.Framework;

namespace LocalLLM.Tests
{
    public class ChatOptionsTests
    {
        [Test]
        public void Default_HasNoOverrides()
        {
            var options = ChatOptions.Default;

            Assert.IsNull(options.Temperature);
            Assert.IsNull(options.MaxTokens);
            Assert.IsNull(options.JsonSchema);
            Assert.IsFalse(options.Think);
        }

        [Test]
        public void With_ReturnsNewInstanceAndKeepsOriginal()
        {
            var original = ChatOptions.Default;

            var changed = original
                .WithTemperature(0.2f)
                .WithMaxTokens(64)
                .WithJsonSchema("{\"type\":\"object\"}")
                .WithThink(true);

            Assert.AreNotSame(original, changed);
            Assert.IsNull(original.Temperature);
            Assert.AreEqual(0.2f, changed.Temperature);
            Assert.AreEqual(64, changed.MaxTokens);
            Assert.AreEqual("{\"type\":\"object\"}", changed.JsonSchema);
            Assert.IsTrue(changed.Think);
        }

        [Test]
        public void WithTemperature_RejectsNegative()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ChatOptions.Default.WithTemperature(-0.1f));
        }

        [Test]
        public void WithMaxTokens_RejectsZero()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ChatOptions.Default.WithMaxTokens(0));
        }

        [Test]
        public void WithJsonSchema_RejectsNonObject()
        {
            Assert.Throws<System.ArgumentException>(() => ChatOptions.Default.WithJsonSchema("[1]"));
        }

        [TestCase("{}, \"stream\":false, \"z\":{}")]
        [TestCase("{\"a\":1}}")]
        [TestCase("{\"a\":\"}")]
        [TestCase("{\"a\":[1}")]
        public void WithJsonSchema_RejectsAnythingButOneObject(string schema)
        {
            Assert.Throws<System.ArgumentException>(() => ChatOptions.Default.WithJsonSchema(schema));
        }

        [Test]
        public void WithJsonSchema_AcceptsNestedObjectWithBracesInStrings()
        {
            const string schema = "{\"type\":\"object\",\"description\":\"a } and \\\" {\",\"properties\":{\"x\":{\"enum\":[\"[\",\"]\"]}}}";

            Assert.AreEqual(schema, ChatOptions.Default.WithJsonSchema(schema).JsonSchema);
        }

        [Test]
        public void WithJsonSchema_EmptyMeansFreeText()
        {
            var options = ChatOptions.Default.WithJsonSchema("{}").WithJsonSchema("  ");

            Assert.IsNull(options.JsonSchema);
        }
    }
}
