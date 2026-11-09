using NUnit.Framework;

namespace LocalLLM.Tests
{
    public class JsonTextTests
    {
        [TestCase("plain", "\"plain\"")]
        [TestCase("quote \" and \\", "\"quote \\\" and \\\\\"")]
        [TestCase("line\nbreak\ttab\r", "\"line\\nbreak\\ttab\\r\"")]
        [TestCase("\u0001", "\"\\u0001\"")]
        [TestCase("日本語", "\"日本語\"")]
        [TestCase("</script>", "\"<\\/script>\"")]
        public void Quote_EscapesForJson(string input, string expected)
        {
            Assert.AreEqual(expected, JsonText.Quote(input));
        }

        [Test]
        public void Quote_NullBecomesEmptyString()
        {
            Assert.AreEqual("\"\"", JsonText.Quote(null));
        }

        [Test]
        public void Number_UsesInvariantCulture()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.AreEqual("0.5", JsonText.Number(0.5f));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
