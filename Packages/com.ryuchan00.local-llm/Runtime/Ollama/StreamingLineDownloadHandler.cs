using System;
using UnityEngine.Networking;

namespace LocalLLM
{
    /// <summary>Delivers each received NDJSON line as soon as it arrives (called on the main thread).</summary>
    sealed class StreamingLineDownloadHandler : DownloadHandlerScript
    {
        readonly NdjsonLineBuffer buffer = new NdjsonLineBuffer();
        readonly Action<string> onLine;

        public StreamingLineDownloadHandler(Action<string> onLine) : base(new byte[4096])
        {
            this.onLine = onLine ?? throw new ArgumentNullException(nameof(onLine));
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0) return true;
            foreach (var line in buffer.Append(data, dataLength)) onLine(line);
            return true;
        }

        protected override void CompleteContent()
        {
            var rest = buffer.Flush();
            if (rest != null) onLine(rest);
        }
    }
}
