// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WakeUp;

// Shared by the game and the standalone preparation window. These records are
// requests for a representation, never authority for a texture's shader role.
// The game checks its resolved consumer metadata before publishing any object.
public sealed class PreparationOptions
{
    public int Preset { get; set; } // 0 reset/native, 1 full, 2 half, 3 quarter
    public int Filter { get; set; } = 2;
    public int Anisotropy { get; set; } = 2;
    public float MipBias { get; set; }
    public bool Valid => Preset >= 0 && Preset <= 3 && Filter >= 0 && Filter <= 2
        && Anisotropy >= 0 && Anisotropy <= 16 && !float.IsNaN(MipBias)
        && !float.IsInfinity(MipBias) && MipBias >= -3 && MipBias <= 3;
    public int Divisor => 1 << Math.Max(0, Preset - 1);
    public string Identity => PreparationContract.Frame("quality-options-v1", Preset.ToString(System.Globalization.CultureInfo.InvariantCulture), Filter.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Anisotropy.ToString(System.Globalization.CultureInfo.InvariantCulture), MipBias.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
}

public sealed class PreparationPixels
{
    public int Width, Height, GraphicsFormat, TextureFormat, Mips, Filter, WrapU, WrapV, WrapW, Aniso;
    public float Bias;
    public bool Readable;
    public byte[] Pixels = Array.Empty<byte>();
}

public sealed class PreparationRecord
{
    public string Provider = "", Logical = "", Physical = "", SourceDigest = "", Active = "";
    public string Runtime = "", Helper = "", Role = "", RoleIdentity = "provisional";
    public PreparationOptions Options = new();
    public PreparationPixels Output = new();
}

public static class PreparationContract
{
    public const int MaximumRecord = 96 * 1024 * 1024;
    public const string RuntimeGog = "gog573|4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28|unity-gamma-srgb-pixels-full-mips-v2";
    public static string Hash(byte[] bytes)
    { return BitConverter.ToString(CacheDigest.HashInput(bytes)).Replace("-", ""); }
    public static string Frame(params string[] parts)
    {
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) foreach (string part in parts) writer.Write(part);
        return Hash(bytes.ToArray());
    }
    public static string Slot(string provider, string logical) => Frame("quality-slot-v1", provider, logical);
    public static string Stem(string logical)
    {
        string path = logical.Replace('\\', '/');
        if (path.StartsWith("Textures/", StringComparison.Ordinal)) path = path.Substring(9);
        return Path.ChangeExtension(path, null);
    }
    public static byte[] Encode(PreparationRecord record)
    {
        if (!record.Options.Valid || record.Options.Preset == 0 || record.Output.Pixels.Length < 1)
            throw new InvalidDataException("quality-record-options");
        using var bytes = new MemoryStream();
        using var w = new BinaryWriter(bytes, Encoding.UTF8, true);
        w.Write(0x32515557); // WUQ2
        foreach (string value in new[] { record.Provider, record.Logical, record.Physical, record.SourceDigest,
            record.Active, record.Runtime, record.Helper, record.Role, record.RoleIdentity })
        { if (Encoding.UTF8.GetByteCount(value) > 16384) throw new InvalidDataException("quality-record-field"); w.Write(value); }
        w.Write(record.Options.Preset); w.Write(record.Options.Filter); w.Write(record.Options.Anisotropy); w.Write(record.Options.MipBias);
        var e = record.Output;
        w.Write(e.Width); w.Write(e.Height); w.Write(e.GraphicsFormat); w.Write(e.TextureFormat); w.Write(e.Mips);
        w.Write(e.Filter); w.Write(e.WrapU); w.Write(e.WrapV); w.Write(e.WrapW); w.Write(e.Aniso); w.Write(e.Bias); w.Write(e.Readable);
        w.Write(e.Pixels.Length); w.Write(e.Pixels); w.Flush();
        if (bytes.Length > MaximumRecord) throw new InvalidDataException("quality-record-size");
        return bytes.ToArray();
    }
    public static PreparationRecord Decode(byte[] bytes)
    {
        if (bytes.Length < 80 || bytes.Length > MaximumRecord) throw new InvalidDataException("quality-record-size");
        using var r = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
        if (r.ReadInt32() != 0x32515557) throw new InvalidDataException("quality-record-version");
        string Read() { string value = r.ReadString(); if (Encoding.UTF8.GetByteCount(value) > 16384) throw new InvalidDataException("quality-record-field"); return value; }
        var record = new PreparationRecord { Provider = Read(), Logical = Read(), Physical = Read(), SourceDigest = Read(),
            Active = Read(), Runtime = Read(), Helper = Read(), Role = Read(), RoleIdentity = Read() };
        record.Options = new PreparationOptions { Preset = r.ReadInt32(), Filter = r.ReadInt32(), Anisotropy = r.ReadInt32(), MipBias = r.ReadSingle() };
        record.Output = new PreparationPixels { Width = r.ReadInt32(), Height = r.ReadInt32(), GraphicsFormat = r.ReadInt32(),
            TextureFormat = r.ReadInt32(), Mips = r.ReadInt32(), Filter = r.ReadInt32(), WrapU = r.ReadInt32(), WrapV = r.ReadInt32(),
            WrapW = r.ReadInt32(), Aniso = r.ReadInt32(), Bias = r.ReadSingle(), Readable = r.ReadBoolean() };
        int length = r.ReadInt32();
        if (length < 1 || length != r.BaseStream.Length - r.BaseStream.Position || !record.Options.Valid || record.Options.Preset == 0)
            throw new InvalidDataException("quality-record-layout");
        record.Output.Pixels = r.ReadBytes(length);
        return record;
    }
}
