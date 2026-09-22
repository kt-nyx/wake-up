// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

namespace WakeUp;

// Lower native WaveStream operations, before SampleChannel converts
// bytes to floats. Position getter results are recorded, never used as cursors.
internal sealed class PreparedAudioData
{
    internal const int Version = 4;
    internal const string CacheIdentity = "lower-native-byte-transcript-v4";
    internal const int MaximumBytes = 2 * 1024 * 1024;
    internal const int MaximumOperations = 256;
    internal const int HeaderBytes = 52, OperationBytes = 21;
    internal long Length;
    internal int Channels, Frequency;
    internal int Encoding, BitsPerSample, BlockAlign, AverageBytesPerSecond, ExtraSize;
    // The pinned WAV parser returns WaveFormatExtraData even for plain PCM.
    // Keep its entire inline marshalled array, including bytes beyond cbSize.
    // Vorbis returns the base WaveFormat instead. Do not exchange these types.
    internal int FormatKind;
    internal byte[] ExtraData = Array.Empty<byte>();
    private static readonly FieldInfo FormatTagField = FormatField("waveFormatTag");
    private static readonly FieldInfo ChannelsField = FormatField("channels");
    private static readonly FieldInfo FrequencyField = FormatField("sampleRate");
    private static readonly FieldInfo AverageField = FormatField("averageBytesPerSecond");
    private static readonly FieldInfo BlockField = FormatField("blockAlign");
    private static readonly FieldInfo BitsField = FormatField("bitsPerSample");
    private static readonly FieldInfo ExtraSizeField = FormatField("extraSize");
    private static readonly FieldInfo ExtraDataField = typeof(WaveFormatExtraData).GetField("extraData", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static FieldInfo FormatField(string name) => typeof(WaveFormat).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal bool HasSupportedFormat
    {
        get
        {
            if (Channels < 1 || Channels > 32 || Frequency < 1 || Frequency > 768000
                || BlockAlign < 1 || BlockAlign > short.MaxValue || AverageBytesPerSecond < 1
                || ExtraSize < 0 || ExtraSize > 100 || ExtraData == null
                || (FormatKind == 0 ? ExtraSize != 0 || ExtraData.Length != 0
                    : FormatKind != 1 || ExtraData.Length != 100)) return false;
            bool linear = ((Encoding == 1 && (BitsPerSample == 8 || BitsPerSample == 16 || BitsPerSample == 24 || BitsPerSample == 32))
                || (Encoding == 3 && (BitsPerSample == 32 || BitsPerSample == 64)))
                && BlockAlign == Channels * (BitsPerSample / 8)
                && AverageBytesPerSecond == (long)Frequency * BlockAlign;
            if (Encoding == 1 || Encoding == 3) return linear;
            if (FormatKind != 1) return false;
            // These are encoded source formats, never cached ACM output. The
            // original native conversion still decides codec availability.
            if (Encoding == 6 || Encoding == 7) return BitsPerSample == 8
                && BlockAlign == Channels && AverageBytesPerSecond == (long)Frequency * BlockAlign;
            if (Encoding == 2) return BitsPerSample == 4 && ExtraSize >= 4
                && U16(0) > 0 && U16(2) > 0 && 4 + 4 * U16(2) == ExtraSize;
            if (Encoding == 17) return BitsPerSample == 4 && ExtraSize == 2 && U16(0) > 0;
            if (Encoding == 49) return BitsPerSample == 0 && ExtraSize == 2 && U16(0) > 0;
            if (Encoding != 0xfffe || ExtraSize != 22 || U16(0) < 1 || U16(0) > BitsPerSample) return false;
            var subFormat = new Guid(ExtraData.Skip(6).Take(16).ToArray());
            bool pcm = subFormat == new Guid("00000001-0000-0010-8000-00aa00389b71");
            bool ieee = subFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
            return ((pcm && (BitsPerSample == 8 || BitsPerSample == 16 || BitsPerSample == 24 || BitsPerSample == 32))
                    || (ieee && (BitsPerSample == 32 || BitsPerSample == 64) && U16(0) == BitsPerSample))
                && BlockAlign == Channels * (BitsPerSample / 8)
                && AverageBytesPerSecond == (long)Frequency * BlockAlign;
        }
    }
    private int U16(int offset) => ExtraData[offset] | ExtraData[offset + 1] << 8;

    internal bool CaptureFormat(WaveFormat format)
    {
        if (format.GetType() != typeof(WaveFormat) && format.GetType() != typeof(WaveFormatExtraData)) return false;
        Encoding = (int)format.Encoding; BitsPerSample = format.BitsPerSample;
        Channels = format.Channels; Frequency = format.SampleRate; BlockAlign = format.BlockAlign;
        AverageBytesPerSecond = format.AverageBytesPerSecond; ExtraSize = format.ExtraSize;
        FormatKind = format is WaveFormatExtraData ? 1 : 0;
        ExtraData = format is WaveFormatExtraData extra ? (byte[])extra.ExtraData.Clone() : Array.Empty<byte>();
        return HasSupportedFormat;
    }

    internal WaveFormat RestoreFormat()
    {
        if (!HasSupportedFormat) throw new InvalidDataException("audio-metadata");
        // The original cold WAV parser supplied these fields. Recreate its
        // exact pinned layout without calling any patchable NAudio constructor
        // or helper while warm admission is being decided.
        var format = (WaveFormat)FormatterServices.GetUninitializedObject(FormatKind == 0 ? typeof(WaveFormat) : typeof(WaveFormatExtraData));
        FormatTagField.SetValue(format, (WaveFormatEncoding)Encoding);
        ChannelsField.SetValue(format, (short)Channels); FrequencyField.SetValue(format, Frequency);
        AverageField.SetValue(format, AverageBytesPerSecond); BlockField.SetValue(format, (short)BlockAlign);
        BitsField.SetValue(format, (short)BitsPerSample); ExtraSizeField.SetValue(format, (short)ExtraSize);
        if (FormatKind == 1) ExtraDataField.SetValue(format, (byte[])ExtraData.Clone());
        return format;
    }

    internal bool FormatMatches(WaveFormat format)
        => Encoding == (int)format.Encoding && BitsPerSample == format.BitsPerSample
            && Channels == format.Channels && Frequency == format.SampleRate
            && BlockAlign == format.BlockAlign && AverageBytesPerSecond == format.AverageBytesPerSecond
            && ExtraSize == format.ExtraSize
            && (FormatKind == 0 ? format.GetType() == typeof(WaveFormat)
                : format.GetType() == typeof(WaveFormatExtraData) && ExtraData.SequenceEqual(((WaveFormatExtraData)format).ExtraData));
    internal readonly List<Operation> Operations = new();
    internal enum OperationKind : byte { Read, GetPosition, SetPosition }
    internal sealed class Operation
    {
        internal OperationKind Kind;
        internal int Offset, Count, Returned;
        internal long Value;
        internal byte[] Bytes = Array.Empty<byte>();
    }

    internal byte[] Encode()
    {
        Validate();
        int size = HeaderBytes + ExtraData.Length;
        foreach (Operation op in Operations) size += OperationBytes + op.Bytes.Length;
        using var stream = new MemoryStream(size);
        using var writer = new BinaryWriter(stream);
        writer.Write(Version); writer.Write(Length); writer.Write(Channels); writer.Write(Frequency);
        writer.Write(Encoding); writer.Write(BitsPerSample); writer.Write(BlockAlign);
        writer.Write(AverageBytesPerSecond); writer.Write(ExtraSize);
        writer.Write(FormatKind); writer.Write(ExtraData.Length);
        writer.Write(Operations.Count);
        writer.Write(ExtraData);
        foreach (Operation op in Operations)
        {
            writer.Write((byte)op.Kind); writer.Write(op.Offset); writer.Write(op.Count);
            writer.Write(op.Returned); writer.Write(op.Value); writer.Write(op.Bytes);
        }
        writer.Flush();
        return stream.ToArray();
    }

    internal static PreparedAudioData Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length < HeaderBytes || bytes.Length > MaximumBytes)
            throw new InvalidDataException("audio-size");
        using var stream = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadInt32() != Version) throw new InvalidDataException("audio-version");
        var data = new PreparedAudioData { Length = reader.ReadInt64(), Channels = reader.ReadInt32(), Frequency = reader.ReadInt32(),
            Encoding = reader.ReadInt32(), BitsPerSample = reader.ReadInt32(), BlockAlign = reader.ReadInt32(),
            AverageBytesPerSecond = reader.ReadInt32(), ExtraSize = reader.ReadInt32() };
        data.FormatKind = reader.ReadInt32();
        int extraLength = reader.ReadInt32();
        int count = reader.ReadInt32();
        if (extraLength != (data.FormatKind == 0 ? 0 : 100) || extraLength > stream.Length - stream.Position)
            throw new InvalidDataException("audio-extra-layout");
        data.ExtraData = reader.ReadBytes(extraLength);
        if (count < 1 || count > MaximumOperations) throw new InvalidDataException("audio-operation-count");
        for (int i = 0; i < count; i++)
        {
            if (stream.Length - stream.Position < OperationBytes) throw new InvalidDataException("audio-truncated");
            var op = new Operation { Kind = (OperationKind)reader.ReadByte(), Offset = reader.ReadInt32(),
                Count = reader.ReadInt32(), Returned = reader.ReadInt32(), Value = reader.ReadInt64() };
            if (op.Kind == OperationKind.Read)
            {
                if (op.Returned < 0 || op.Returned > MaximumBytes || op.Returned > stream.Length - stream.Position)
                    throw new InvalidDataException("audio-returned-size");
                op.Bytes = reader.ReadBytes(op.Returned);
            }
            data.Operations.Add(op);
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("audio-trailing-data");
        data.Validate();
        return data;
    }

    internal void Validate()
    {
        if (Length < 1 || !HasSupportedFormat)
            throw new InvalidDataException("audio-metadata");
        if (Operations.Count < 1 || Operations.Count > MaximumOperations) throw new InvalidDataException("audio-operation-count");
        long size = HeaderBytes + ExtraData.Length;
        bool anyBytes = false;
        foreach (Operation op in Operations)
        {
            if (op == null || op.Bytes == null) throw new InvalidDataException("audio-operation");
            if (op.Kind == OperationKind.Read)
            {
                if (op.Offset < 0 || op.Count < 0 || (long)op.Offset + op.Count > MaximumBytes
                    || op.Returned < 0 || op.Returned > op.Count || op.Bytes.Length != op.Returned || op.Value != 0)
                    throw new InvalidDataException("audio-read-layout");
                anyBytes |= op.Returned > 0;
            }
            else if ((op.Kind != OperationKind.GetPosition && op.Kind != OperationKind.SetPosition)
                || op.Offset != 0 || op.Count != 0 || op.Returned != 0 || op.Bytes.Length != 0)
                throw new InvalidDataException("audio-position-layout");
            size += OperationBytes + op.Bytes.Length;
            if (size > MaximumBytes) throw new InvalidDataException("audio-size");
        }
        if (!anyBytes) throw new InvalidDataException("audio-empty-transcript");
    }

    internal PreparedAudioData Copy()
    {
        var result = new PreparedAudioData { Length = Length, Channels = Channels, Frequency = Frequency,
            Encoding = Encoding, BitsPerSample = BitsPerSample, BlockAlign = BlockAlign,
            AverageBytesPerSecond = AverageBytesPerSecond, ExtraSize = ExtraSize,
            FormatKind = FormatKind, ExtraData = (byte[])ExtraData.Clone() };
        result.Operations.AddRange(Operations.Select(op => new Operation { Kind = op.Kind, Offset = op.Offset,
            Count = op.Count, Returned = op.Returned, Value = op.Value, Bytes = (byte[])op.Bytes.Clone() }));
        return result;
    }
}
