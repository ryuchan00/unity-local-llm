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

        // The jslib checks these hashes itself when it has to load the files through blob: URLs.
        [TestCase("web-llm.js")]
        [TestCase("local-llm-loader.js")]
        public void Jslib_RecordsSameHash(string file)
        {
            var jslib = File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(WebLLMBuildStep.SourceDirectory()), "Runtime", "WebLLM", "Plugins", "LocalLLM.jslib"));

            StringAssert.Contains($"\"{file}\": \"{WebLLMBuildStep.Sha256[file]}\"", jslib);
        }

        [Test]
        public void Verify_RejectsModifiedFile()
        {
            var path = Path.Combine(temp, WebLLMBuildStep.LoaderFile);
            File.WriteAllText(path, "tampered");

            Assert.Throws<BuildFailedException>(() => WebLLMBuildStep.Verify(path, WebLLMBuildStep.LoaderFile));
        }

        [Test]
        public void Sha256Hex_IgnoresCrlfConversion()
        {
            var lf = Path.Combine(temp, "lf.js");
            var crlf = Path.Combine(temp, "crlf.js");
            File.WriteAllText(lf, "a" + (char)10 + "b" + (char)10);
            File.WriteAllText(crlf, "a" + (char)13 + (char)10 + "b" + (char)13 + (char)10);

            Assert.AreEqual(WebLLMBuildStep.Sha256Hex(lf), WebLLMBuildStep.Sha256Hex(crlf));
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
