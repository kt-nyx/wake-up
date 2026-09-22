// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Verse;
using Injection = Verse.DefInjectionPackage.DefInjection;

namespace WakeUp;

// Detached native tables captured before translation application mutates records.
// Neither object references nor application outcomes belong in this format.
internal static class ParsedLanguageFormat
{
    internal const int MaximumPayload = 32 * 1024 * 1024;
    private const int MaximumCount = 262144, MaximumStringBytes = 4 * 1024 * 1024;
    private const int Magic = 0x324C5057;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    internal static byte[] EncodeKeyed(Dictionary<string, LoadedLanguage.KeyedReplacement> table)
    {
        CheckTable(table);
        return Encode(1, writer =>
        {
            WriteCount(writer, table.Count);
            foreach (var entry in table)
            {
                var value = entry.Value;
                if (value == null || value.GetType() != typeof(LoadedLanguage.KeyedReplacement))
                    throw new InvalidDataException("language-keyed-record");
                WriteText(writer, entry.Key); WriteText(writer, value.key); WriteText(writer, value.value);
                WriteText(writer, value.fileSource); writer.Write(value.fileSourceLine);
                WriteText(writer, value.fileSourceFullPath); writer.Write(value.isPlaceholder);
            }
        });
    }

    internal static Dictionary<string, LoadedLanguage.KeyedReplacement> DecodeKeyed(byte[] payload) => Decode(payload, 1, reader =>
    {
        int count = ReadCount(reader);
        var table = new Dictionary<string, LoadedLanguage.KeyedReplacement>(count);
        for (int i = 0; i < count; i++)
        {
            string key = ReadKey(reader);
            table.Add(key, new LoadedLanguage.KeyedReplacement
            {
                key = ReadText(reader), value = ReadText(reader), fileSource = ReadText(reader),
                fileSourceLine = reader.ReadInt32(), fileSourceFullPath = ReadText(reader), isPlaceholder = ReadBoolean(reader)
            });
        }
        return table;
    });

    internal static byte[] EncodeInjection(DefInjectionPackage package)
    {
        if (package.GetType() != typeof(DefInjectionPackage) || package.loadErrors == null || package.loadErrors.Count != 0 ||
            package.loadSyntaxSuggestions == null || package.loadSyntaxSuggestions.Count != 0)
            throw new InvalidDataException("language-injection-diagnostics");
        CheckTable(package.injections);
        return Encode(2, writer =>
        {
            writer.Write(package.usedOldRepSyntax); WriteCount(writer, package.injections.Count);
            foreach (var entry in package.injections)
            {
                var value = entry.Value;
                if (value == null || value.GetType() != typeof(Injection) || value.injected || value.normalizedPath != null ||
                    value.suggestedPath != null || value.replacedString != null || value.replacedList != null)
                    throw new InvalidDataException("language-injection-mutated");
                WriteText(writer, entry.Key); WriteText(writer, value.path); WriteText(writer, value.nonBackCompatiblePath);
                WriteText(writer, value.injection); WriteList(writer, value.fullListInjection);
                var comments = value.fullListInjectionComments;
                if (comments != null && comments.GetType() != typeof(List<Pair<int, string>>))
                    throw new InvalidDataException("language-comments-contract");
                WriteCount(writer, comments?.Count ?? -1, true);
                if (comments != null)
                    foreach (var comment in comments) { writer.Write(comment.First); WriteText(writer, comment.Second); }
                WriteText(writer, value.fileSource); writer.Write(value.isPlaceholder);
            }
        });
    }

    internal static DefInjectionPackage DecodeInjection(byte[] payload, Type defType) => Decode(payload, 2, reader =>
    {
        var package = new DefInjectionPackage(defType) { usedOldRepSyntax = ReadBoolean(reader) };
        int count = ReadCount(reader);
        for (int i = 0; i < count; i++)
        {
            string key = ReadKey(reader);
            var value = new Injection
            {
                path = ReadText(reader), nonBackCompatiblePath = ReadText(reader), injection = ReadText(reader),
                fullListInjection = ReadList(reader)
            };
            int comments = ReadCount(reader, true);
            if (comments >= 0)
            {
                value.fullListInjectionComments = new List<Pair<int, string>>(comments);
                for (int j = 0; j < comments; j++)
                    value.fullListInjectionComments.Add(new Pair<int, string>(reader.ReadInt32(), ReadText(reader)!));
            }
            value.fileSource = ReadText(reader); value.isPlaceholder = ReadBoolean(reader);
            package.injections.Add(key, value);
        }
        return package;
    });

    internal static byte[] EncodeStrings(Dictionary<string, List<string>> table)
    {
        CheckTable(table);
        return Encode(3, writer =>
        {
            WriteCount(writer, table.Count);
            foreach (var entry in table) { WriteText(writer, entry.Key); WriteList(writer, entry.Value); }
        });
    }

    internal static Dictionary<string, List<string>> DecodeStrings(byte[] payload) => Decode(payload, 3, reader =>
    {
        int count = ReadCount(reader);
        var table = new Dictionary<string, List<string>>(count);
        for (int i = 0; i < count; i++) table.Add(ReadKey(reader), ReadList(reader)!);
        return table;
    });

    internal static byte[] EncodeInstructions(List<List<ParsedInjectionInstructions.Operation>> files) => Encode(4, writer =>
    {
        WriteCount(writer, files.Count);
        foreach (var file in files)
        {
            WriteCount(writer, file.Count);
            foreach (var op in file)
            {
                if (op.Kind > 2) throw new InvalidDataException("language-instruction-kind");
                writer.Write(op.Kind); WriteText(writer, op.Path); WriteText(writer, op.Text); WriteList(writer, op.Values);
                WriteCount(writer, op.Comments?.Count ?? -1, true);
                if (op.Comments != null) foreach (var comment in op.Comments) { writer.Write(comment.First); WriteText(writer, comment.Second); }
            }
        }
    });
    internal static List<List<ParsedInjectionInstructions.Operation>> DecodeInstructions(byte[] payload) => Decode(payload, 4, reader =>
    {
        int files = ReadCount(reader); var result = new List<List<ParsedInjectionInstructions.Operation>>(files);
        for (int i = 0; i < files; i++)
        {
            int count = ReadCount(reader); var operations = new List<ParsedInjectionInstructions.Operation>(count);
            for (int j = 0; j < count; j++)
            {
                var op = new ParsedInjectionInstructions.Operation { Kind = reader.ReadByte(), Path = ReadKey(reader), Text = ReadText(reader), Values = ReadList(reader) };
                int comments = ReadCount(reader, true);
                if (comments >= 0)
                {
                    op.Comments = new List<Pair<int, string>>(comments);
                    for (int k = 0; k < comments; k++)
                    {
                        int position = reader.ReadInt32(); string text = ReadKey(reader);
                        if (op.Values == null || position < 0 || position > op.Values.Count) throw new InvalidDataException("language-instruction-comment");
                        op.Comments.Add(new Pair<int, string>(position, text));
                    }
                }
                if (op.Kind > 2 || op.Kind == 0 && (op.Text == null || op.Values != null || op.Comments != null)
                    || op.Kind == 1 && (op.Text != null || op.Values == null || op.Values.Any(v => v == null))
                    || op.Kind == 2 && (op.Path != "" || op.Text != null || op.Values != null || op.Comments != null))
                    throw new InvalidDataException("language-instruction-record");
                operations.Add(op);
            }
            result.Add(operations);
        }
        return result;
    });

    internal static byte[] EncodeLines(List<string> lines) => Encode(5, writer => WriteList(writer, lines));
    internal static List<string> DecodeLines(byte[] payload) => Decode(payload, 5, reader =>
    {
        var lines = ReadList(reader);
        if (lines == null || lines.Any(line => line == null)) throw new InvalidDataException("language-line-record");
        return lines;
    });

    internal static byte[] EncodeKeyedRows(List<DirectXmlLoaderSimple.XmlKeyValuePair> rows) => Encode(6, writer =>
    {
        WriteCount(writer, rows.Count);
        foreach (var row in rows) { WriteText(writer, row.key); WriteText(writer, row.value); writer.Write(row.lineNumber); }
    });
    internal static List<DirectXmlLoaderSimple.XmlKeyValuePair> DecodeKeyedRows(byte[] payload) => Decode(payload, 6, reader =>
    {
        int count = ReadCount(reader); var rows = new List<DirectXmlLoaderSimple.XmlKeyValuePair>(count);
        for (int i = 0; i < count; i++)
        {
            var row = new DirectXmlLoaderSimple.XmlKeyValuePair { key = ReadKey(reader), value = ReadKey(reader), lineNumber = reader.ReadInt32() };
            if (row.lineNumber < 0) throw new InvalidDataException("language-keyed-line");
            rows.Add(row);
        }
        return rows;
    });

    private static void CheckTable<T>(Dictionary<string, T> table)
    {
        if (table == null || table.GetType() != typeof(Dictionary<string, T>) ||
            (!ReferenceEquals(table.Comparer, EqualityComparer<string>.Default) && !ReferenceEquals(table.Comparer, StringComparer.Ordinal)))
            throw new InvalidDataException("language-table-contract");
    }

    private static byte[] Encode(byte kind, Action<BinaryWriter> write)
    {
        using var output = new MemoryStream();
        using var writer = new TextWriter(output);
        writer.Write(Magic); writer.Write(kind); write(writer); writer.Flush();
        if (output.Length > MaximumPayload) throw new InvalidDataException("language-payload-limit");
        return output.ToArray();
    }

    private static T Decode<T>(byte[] payload, byte kind, Func<BinaryReader, T> read)
    {
        if (payload == null || payload.Length > MaximumPayload) throw new InvalidDataException("language-payload-limit");
        try
        {
            using var input = new MemoryStream(payload, false);
            using var reader = new TextReader(input, payload);
            if (reader.ReadInt32() != Magic || reader.ReadByte() != kind) throw new InvalidDataException("language-format");
            T result = read(reader);
            if (input.Position != input.Length) throw new InvalidDataException("language-trailing-data");
            return result;
        }
        catch (Exception e) when (e is EndOfStreamException || e is ArgumentException)
        { throw new InvalidDataException("language-record-contract", e); }
    }

    private static void WriteCount(BinaryWriter writer, int count, bool nullable = false)
    {
        if (count < (nullable ? -1 : 0) || count > MaximumCount) throw new InvalidDataException("language-count-limit");
        writer.Write(count);
    }

    private static int ReadCount(BinaryReader reader, bool nullable = false)
    {
        int count = reader.ReadInt32();
        if (count < (nullable ? -1 : 0) || count > MaximumCount || count > (reader.BaseStream.Length - reader.BaseStream.Position) / 4)
            throw new InvalidDataException("language-count-limit");
        return count;
    }

    private static void WriteText(BinaryWriter writer, string? text)
    {
        if (writer.BaseStream.Position + 4L > MaximumPayload) throw new InvalidDataException("language-payload-limit");
        var symbols = ((TextWriter)writer).Symbols;
        if (text == null) { writer.Write(-1); return; }
        if (symbols.TryGetValue(text, out int existing)) { writer.Write(existing); return; }
        int length = Utf8.GetByteCount(text);
        if (length > MaximumStringBytes || symbols.Count >= MaximumPayload / 8 || writer.BaseStream.Position + 8L + length > MaximumPayload)
            throw new InvalidDataException("language-string-limit");
        symbols.Add(text, symbols.Count);
        writer.Write(-2); writer.Write(length); writer.Write(Utf8.GetBytes(text));
    }

    private static string? ReadText(BinaryReader reader)
    {
        var symbols = ((TextReader)reader).Symbols;
        int token = reader.ReadInt32();
        if (token == -1) return null;
        if (token >= 0)
        {
            if (token >= symbols.Count) throw new InvalidDataException("language-string-reference");
            return symbols[token];
        }
        if (token != -2 || symbols.Count >= MaximumPayload / 8) throw new InvalidDataException("language-string-reference");
        int length = reader.ReadInt32();
        if (length < 0 || length > MaximumStringBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("language-string-limit");
        var source = (TextReader)reader;
        string text = Utf8.GetString(source.Payload, checked((int)reader.BaseStream.Position), length);
        reader.BaseStream.Position += length;
        symbols.Add(text); return text;
    }

    // Native record provenance and predecessor snapshots repeat the same paths
    // and names many times. Encode each immutable string once per payload while
    // still visiting every current field and preserving exact ordinal contents.
    private sealed class TextWriter : BinaryWriter
    {
        internal readonly Dictionary<string, int> Symbols = new(StringComparer.Ordinal);
        internal TextWriter(Stream stream) : base(stream, Utf8, true) { }
    }
    private sealed class TextReader : BinaryReader
    {
        internal readonly List<string> Symbols = new();
        internal readonly byte[] Payload;
        internal TextReader(Stream stream, byte[] payload) : base(stream, Utf8, true) { Payload = payload; }
    }

    private static string ReadKey(BinaryReader reader) => ReadText(reader) ?? throw new InvalidDataException("language-null-key");

    private static bool ReadBoolean(BinaryReader reader)
    {
        byte value = reader.ReadByte();
        if (value > 1) throw new InvalidDataException("language-boolean");
        return value != 0;
    }

    private static void WriteList(BinaryWriter writer, List<string>? list)
    {
        if (list != null && list.GetType() != typeof(List<string>)) throw new InvalidDataException("language-list-contract");
        WriteCount(writer, list?.Count ?? -1, true);
        if (list != null) foreach (string? value in list) WriteText(writer, value);
    }

    private static List<string>? ReadList(BinaryReader reader)
    {
        int count = ReadCount(reader, true);
        if (count == -1) return null;
        var list = new List<string>(count);
        for (int i = 0; i < count; i++) list.Add(ReadText(reader)!);
        return list;
    }
}
