using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LocalLLM
{
    /// <summary>
    /// Minimal JSON writing helpers. JsonUtility cannot omit unset values or embed a raw schema object,
    /// so requests are written by hand.
    /// </summary>
    static class JsonText
    {
        public static string Quote(string value)
        {
            var builder = new StringBuilder((value?.Length ?? 0) + 2);
            AppendQuoted(builder, value);
            return builder.ToString();
        }

        public static void AppendQuoted(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    // Keeps the JSON safe to embed in an HTML script block.
                    case '/': builder.Append("\\/"); break;
                    default:
                        if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("x4"));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }

        /// <summary>
        /// True when text is one balanced JSON object with nothing after it. Checks nesting and strings,
        /// which is enough to stop a value from closing the object early and adding fields to a request.
        /// </summary>
        public static bool IsSingleObject(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != '{') return false;
            var closers = new Stack<char>();
            var inString = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; break;
                    case '{': closers.Push('}'); break;
                    case '[': closers.Push(']'); break;
                    case '}':
                    case ']':
                        if (closers.Count == 0 || closers.Pop() != c) return false;
                        // The outer object closed: only whitespace may follow.
                        if (closers.Count == 0) return text.Substring(i + 1).Trim().Length == 0;
                        break;
                }
            }
            return false;
        }

        public static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        public static void AppendMessages(StringBuilder builder, IEnumerable<ChatMessage> messages)
        {
            builder.Append('[');
            var first = true;
            foreach (var message in messages)
            {
                if (message == null) throw new System.ArgumentException("Messages must not contain null.", nameof(messages));
                if (!first) builder.Append(',');
                first = false;
                builder.Append("{\"role\":");
                AppendQuoted(builder, message.Role);
                builder.Append(",\"content\":");
                AppendQuoted(builder, message.Content);
                builder.Append('}');
            }
            builder.Append(']');
        }
    }
}
