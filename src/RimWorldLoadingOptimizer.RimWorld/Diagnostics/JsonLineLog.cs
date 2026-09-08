// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System.Globalization;
using System.IO;
using System.Text;

namespace RimWorldLoadingOptimizer.RimWorld;

// Shared encoding and best-effort file I/O for startup receipts. Callers own
// their event fields; logging never determines whether an optimization runs.
internal static class JsonLineLog
{
    internal static string Quote(string text) => "\"" + Escape(text) + "\"";

    internal static string Escape(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            switch (character)
            {
                case '"':
                    result.Append("\\\"");
                    break;
                case '\\':
                    result.Append("\\\\");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                        result.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        result.Append(character);
                    break;
            }
        }
        return result.ToString();
    }

    // fields contains complete JSON members, without a leading comma.
    internal static void WriteEvent(string? path, string kind, string? fields = null)
        => Append(path, "{\"event\":" + Quote(kind)
            + (string.IsNullOrEmpty(fields) ? "" : "," + fields) + "}");

    internal static void WriteReceipt(string? path, string kind, string reason, string? fields = null)
        => WriteEvent(path, kind, "\"reason\":" + Quote(reason)
            + (string.IsNullOrEmpty(fields) ? "" : "," + fields));

    internal static void Append(string? path, string json)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, json + System.Environment.NewLine, new UTF8Encoding(false));
        }
        catch { /* A diagnostic write failure must not interrupt game loading. */ }
    }
}
