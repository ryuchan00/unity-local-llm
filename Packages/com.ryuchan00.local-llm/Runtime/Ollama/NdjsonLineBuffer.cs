using System.Collections.Generic;
using System.Text;

namespace LocalLLM
{
    /// <summary>
    /// Splits a newline-delimited byte stream into lines.
    /// Works on raw bytes so multibyte UTF-8 characters split across chunks decode correctly.
    /// </summary>
    sealed class NdjsonLineBuffer
    {
        const byte NewLine = (byte)'\n';

        readonly List<byte> pending = new List<byte>();

        public IReadOnlyList<string> Append(byte[] data, int length)
        {
            var lines = new List<string>();
            for (var i = 0; i < length; i++)
            {
                if (data[i] == NewLine)
                {
                    var line = TakePending();
                    if (line != null) lines.Add(line);
                }
                else
                {
                    pending.Add(data[i]);
                }
            }
            return lines;
        }

        /// <summary>Returns any trailing data that had no newline, or null.</summary>
        public string Flush() => TakePending();

        string TakePending()
        {
            var text = Encoding.UTF8.GetString(pending.ToArray()).Trim();
            pending.Clear();
            return text.Length > 0 ? text : null;
        }
    }
}
