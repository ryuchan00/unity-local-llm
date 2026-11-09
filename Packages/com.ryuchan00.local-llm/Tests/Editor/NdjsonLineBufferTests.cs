using System.Text;
using NUnit.Framework;

namespace LocalLLM.Tests
{
    public class NdjsonLineBufferTests
    {
        static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

        [Test]
        public void Append_CompleteLines_ReturnsEachLine()
        {
            var buffer = new NdjsonLineBuffer();
            var data = Bytes("{\"a\":1}\n{\"b\":2}\n");

            var lines = buffer.Append(data, data.Length);

            Assert.AreEqual(new[] { "{\"a\":1}", "{\"b\":2}" }, lines);
        }

        [Test]
        public void Append_PartialLine_IsHeldUntilNewline()
        {
            var buffer = new NdjsonLineBuffer();
            var first = Bytes("{\"a\":");
            var second = Bytes("1}\n");

            Assert.IsEmpty(buffer.Append(first, first.Length));
            Assert.AreEqual(new[] { "{\"a\":1}" }, buffer.Append(second, second.Length));
        }

        [Test]
        public void Append_MultibyteCharSplitAcrossChunks_DecodesCorrectly()
        {
            var buffer = new NdjsonLineBuffer();
            var data = Bytes("こんにちは\n");
            // "こ" is 3 bytes in UTF-8; split in the middle of it.
            var head = new byte[1];
            var tail = new byte[data.Length - 1];
            System.Array.Copy(data, 0, head, 0, 1);
            System.Array.Copy(data, 1, tail, 0, tail.Length);

            Assert.IsEmpty(buffer.Append(head, head.Length));
            Assert.AreEqual(new[] { "こんにちは" }, buffer.Append(tail, tail.Length));
        }

        [Test]
        public void Append_RespectsLengthArgument()
        {
            var buffer = new NdjsonLineBuffer();
            var data = Bytes("abc\nGARBAGE");

            Assert.AreEqual(new[] { "abc" }, buffer.Append(data, 4));
            Assert.IsNull(buffer.Flush());
        }

        [Test]
        public void Append_BlankLines_AreSkipped()
        {
            var buffer = new NdjsonLineBuffer();
            var data = Bytes("\n\r\n x \n");

            Assert.AreEqual(new[] { "x" }, buffer.Append(data, data.Length));
        }

        [Test]
        public void Flush_ReturnsRemainderWithoutNewline()
        {
            var buffer = new NdjsonLineBuffer();
            var data = Bytes("{\"error\":\"x\"}");
            buffer.Append(data, data.Length);

            Assert.AreEqual("{\"error\":\"x\"}", buffer.Flush());
            Assert.IsNull(buffer.Flush());
        }
    }
}
