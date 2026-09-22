// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WakeUp;

// One authenticated owned-store envelope contains a load's source groups.
// Each group still has its complete source/text/predecessor key and its existing
// validated format. The bundle only amortizes filesystem operations; it never
// substitutes the load context for a group's identity or retains native objects.
internal static class ParsedLanguageBundle
{
    internal const int MaximumPayload = 128 * 1024 * 1024;
    internal const int MaximumGroups = 32768;
    private const int Magic = 0x31424C50;
    internal static byte[] Encode(Dictionary<string, byte[]> groups)
    {
        if (groups.Count > MaximumGroups) throw new InvalidDataException("language-bundle-count");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Magic); writer.Write(groups.Count);
        foreach (var group in groups)
        {
            if (!OwnedCacheStore.IsKey(group.Key) || group.Value.Length < 1 || group.Value.Length > ParsedLanguageFormat.MaximumPayload
                || stream.Length + 68L + group.Value.Length > MaximumPayload)
                throw new InvalidDataException("language-bundle-bound");
            writer.Write(Encoding.ASCII.GetBytes(group.Key)); writer.Write(group.Value.Length); writer.Write(group.Value);
        }
        writer.Flush(); return stream.ToArray();
    }
    internal static Dictionary<string, byte[]> Decode(byte[] payload)
    {
        if (payload.Length < 8 || payload.Length > MaximumPayload) throw new InvalidDataException("language-bundle-size");
        using var stream = new MemoryStream(payload, false);
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);
        if (reader.ReadInt32() != Magic) throw new InvalidDataException("language-bundle-version");
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumGroups || count > (stream.Length - stream.Position) / 69)
            throw new InvalidDataException("language-bundle-count");
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            if (stream.Length - stream.Position < 68) throw new InvalidDataException("language-bundle-truncated");
            string key = Encoding.ASCII.GetString(reader.ReadBytes(64));
            int length = reader.ReadInt32();
            if (!OwnedCacheStore.IsKey(key) || result.ContainsKey(key) || length < 1
                || length > ParsedLanguageFormat.MaximumPayload || length > stream.Length - stream.Position)
                throw new InvalidDataException("language-bundle-entry");
            result.Add(key, reader.ReadBytes(length));
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("language-bundle-trailing");
        return result;
    }
}
