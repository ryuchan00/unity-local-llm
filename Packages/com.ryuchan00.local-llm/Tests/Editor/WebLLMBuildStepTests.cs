using System.IO;
using System.Linq;
using LocalLLM.Editor;
using NUnit.Framework;
using UnityEditor.Build;

namespace LocalLLM.Tests
{
    public class WebLLMBuildStepTests
    {
        string temp;

        [SetUp]
        public void SetUp()
        {
            temp = Path.Combine(Path.GetTempPath(), "LocalLLMTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(temp, recursive: true);

        [TestCase("web-llm.js")]
        [TestCase("local-llm-loader.js")]
        [TestCase("web-llm.LICENSE.txt")]
        public void BundledFile_MatchesRecordedHash(string file)
        {
            var path = Path.Combine(WebLLMBuildStep.SourceDirectory(), file);

            Assert.DoesNotThrow(() => WebLLMBuildStep.Verify(path, file));
        }

        [Test]
        public void Verify_RejectsModifiedFile()
        {
            var path = Path.Combine(temp, WebLLMBuildStep.LoaderFile);
            File.WriteAllText(path, "tampered");

            Assert.Throws<BuildFailedException>(() => WebLLMBuildStep.Verify(path, WebLLMBuildStep.LoaderFile));
        }

        [Test]
        public void CopyTo_CopiesLoaderWebLLMAndLicense()
        {
            WebLLMBuildStep.CopyTo(temp);

            CollectionAssert.AreEquivalent(
                new[] { "web-llm.js", "local-llm-loader.js", "web-llm.LICENSE.txt" },
                Directory.GetFiles(temp).Select(Path.GetFileName));
        }
    }
}
