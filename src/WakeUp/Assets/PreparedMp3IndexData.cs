// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;

namespace WakeUp;

// Only the successful native frame scan is represented. Decoder state and
// decoded samples never enter this cache.
internal sealed class PreparedMp3IndexData
{
    internal const string Identity = "mp3-native-index-v1";
    internal const int MaximumRows = 65536, MaximumBytes = 2 * 1024 * 1024;
    internal long SourceLength, StartPosition, DataLength, InitialSamples, TotalSamples, TerminalPosition;
    internal int Frequency, Channels;
    internal readonly List<Row> Rows = new();
    internal sealed class Row
    {
        internal long FilePosition, SamplePosition;
        internal int SampleCount, ByteCount;
    }
    internal void Validate()
    {
        if (SourceLength < 1 || StartPosition < 0 || StartPosition > SourceLength || DataLength < 1
            || DataLength > SourceLength || InitialSamples != 0 || Frequency < 1 || Channels < 1 || Channels > 2
            || Rows.Count < 1 || Rows.Count > MaximumRows || TerminalPosition < StartPosition || TerminalPosition > SourceLength)
            throw new InvalidDataException("mp3-index-metadata");
        long samples = InitialSamples, position = StartPosition;
        foreach (Row row in Rows)
        {
            if (row.FilePosition != position || row.SamplePosition != samples || row.SampleCount < 1 || row.SampleCount > 1152
                || row.ByteCount < 4 || row.FilePosition > SourceLength - row.ByteCount)
                throw new InvalidDataException("mp3-index-row");
            position = checked(position + row.ByteCount);
            samples = checked(samples + row.SampleCount);
        }
        if (samples != TotalSamples || TerminalPosition < position) throw new InvalidDataException("mp3-index-terminal");
    }
    internal byte[] Encode()
    {
        Validate();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(1); writer.Write(SourceLength); writer.Write(StartPosition); writer.Write(DataLength);
        writer.Write(InitialSamples); writer.Write(TotalSamples); writer.Write(TerminalPosition);
        writer.Write(Frequency); writer.Write(Channels); writer.Write(Rows.Count);
        foreach (Row row in Rows) { writer.Write(row.FilePosition); writer.Write(row.SamplePosition); writer.Write(row.SampleCount); writer.Write(row.ByteCount); }
        return stream.ToArray();
    }
    internal static PreparedMp3IndexData Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 64 || bytes.Length > MaximumBytes) throw new InvalidDataException("mp3-index-size");
        using var stream = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadInt32() != 1) throw new InvalidDataException("mp3-index-version");
        var result = new PreparedMp3IndexData { SourceLength = reader.ReadInt64(), StartPosition = reader.ReadInt64(),
            DataLength = reader.ReadInt64(), InitialSamples = reader.ReadInt64(), TotalSamples = reader.ReadInt64(),
            TerminalPosition = reader.ReadInt64(), Frequency = reader.ReadInt32(), Channels = reader.ReadInt32() };
        int count = reader.ReadInt32();
        if (count < 1 || count > MaximumRows || stream.Length - stream.Position != count * 24L)
            throw new InvalidDataException("mp3-index-row-count");
        for (int i = 0; i < count; i++) result.Rows.Add(new Row { FilePosition = reader.ReadInt64(), SamplePosition = reader.ReadInt64(),
            SampleCount = reader.ReadInt32(), ByteCount = reader.ReadInt32() });
        result.Validate();
        return result;
    }
}
