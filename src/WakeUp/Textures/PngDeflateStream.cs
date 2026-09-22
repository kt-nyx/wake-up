// Copyright (c) 2026 kt-nyx and contributors. GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Threading;

namespace WakeUp;

// Original streaming RFC 1951 decoder. The caller owns zlib framing/Adler checking.
// One decode supplies PNG scanlines, with a fixed 32 KiB history and small tables.
// Unlike an EOF-tolerant inflater, a missing final block is always an error.
internal sealed class PngDeflateStream : Stream
{
    private readonly Stream source;
    private readonly CancellationToken cancellation;
    private readonly long maximumOutputBytes;
    private readonly bool leaveOpen;
    private readonly byte[] history = new byte[32768];
    private uint bits;
    private int bitCount, historyPosition, storedRemaining, copyRemaining, copyDistance;
    private long outputBytes;
    private bool blockOpen, finalBlock, finished, disposed;
    private int blockType;
    private Huffman? literals, distances;

    private static readonly int[] LengthBase = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258 };
    private static readonly int[] LengthExtra = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };
    private static readonly int[] DistanceBase = { 1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577 };
    private static readonly int[] DistanceExtra = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };
    private static readonly int[] CodeLengthOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };
    private static readonly Huffman FixedLiterals = CreateFixedLiterals();
    private static readonly Huffman FixedDistances = CreateFixedDistances();

    internal PngDeflateStream(Stream source, long maximumOutputBytes, CancellationToken cancellation, bool leaveOpen = true)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        if (!source.CanRead) throw new ArgumentException("A readable raw DEFLATE stream is required.", nameof(source));
        if (maximumOutputBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        this.maximumOutputBytes = maximumOutputBytes;
        this.cancellation = cancellation;
        this.leaveOpen = leaveOpen;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(offset));
        EnsureReadable();
        int written = 0;
        while (written < count)
        {
            if (copyRemaining > 0)
            {
                // The match's complete length and history distance were checked
                // when its symbol was read. Copy at most that bounded match
                // (258 bytes), without re-entering ReadByte for every byte.
                cancellation.ThrowIfCancellationRequested();
                int take = Math.Min(copyRemaining, count - written);
                if (take > maximumOutputBytes - outputBytes) throw Invalid("output-limit");
                int destination = historyPosition;
                int from = (destination - copyDistance) & 32767;
                for (int i = 0; i < take; i++)
                {
                    // Sequential read/write is required: a short-distance
                    // match consumes bytes emitted earlier in this same run.
                    byte copied = history[from];
                    history[destination] = copied;
                    buffer[offset + written + i] = copied;
                    from = (from + 1) & 32767;
                    destination = (destination + 1) & 32767;
                }
                historyPosition = destination;
                outputBytes += take; copyRemaining -= take; written += take;
                continue;
            }
            int value = ReadByte();
            if (value < 0) break;
            buffer[offset + written++] = (byte)value;
        }
        return written;
    }

    public override int ReadByte()
    {
        EnsureReadable();
        while (!finished)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!blockOpen) OpenBlock();
            if (blockType == 0)
            {
                if (storedRemaining == 0) { EndBlock(); continue; }
                int value = ReadBits(8);
                storedRemaining--;
                return Emit(value);
            }
            if (copyRemaining > 0)
            {
                int value = history[(historyPosition - copyDistance) & 32767];
                copyRemaining--;
                return Emit(value);
            }
            int symbol = literals!.Read(this);
            if (symbol < 256) return Emit(symbol);
            if (symbol == 256) { EndBlock(); continue; }
            if (symbol > 285) throw Invalid("reserved-length");
            int lengthIndex = symbol - 257;
            copyRemaining = LengthBase[lengthIndex] + ReadBits(LengthExtra[lengthIndex]);
            int distanceSymbol = distances!.Read(this);
            if (distanceSymbol >= DistanceBase.Length) throw Invalid("reserved-distance");
            copyDistance = DistanceBase[distanceSymbol] + ReadBits(DistanceExtra[distanceSymbol]);
            if (copyDistance > Math.Min(outputBytes, 32768L)) throw Invalid("distance-before-history");
            if (copyRemaining > maximumOutputBytes - outputBytes) throw Invalid("output-limit");
        }
        return -1;
    }

    private int Emit(int value)
    {
        if (outputBytes >= maximumOutputBytes) throw Invalid("output-limit");
        history[historyPosition] = (byte)value;
        historyPosition = (historyPosition + 1) & 32767;
        outputBytes++;
        return value;
    }

    private void OpenBlock()
    {
        finalBlock = ReadBits(1) != 0;
        blockType = ReadBits(2);
        blockOpen = true;
        if (blockType == 0)
        {
            // Huffman lookahead can already hold whole bytes of LEN. Discard
            // only the remainder of the current byte, preserving those bytes.
            int alignment = bitCount & 7;
            bits >>= alignment; bitCount -= alignment;
            int length = ReadBits(16), complement = ReadBits(16);
            if ((length ^ complement) != 65535) throw Invalid("stored-length");
            if (length > maximumOutputBytes - outputBytes) throw Invalid("output-limit");
            storedRemaining = length;
        }
        else if (blockType == 1)
        { literals = FixedLiterals; distances = FixedDistances; }
        else if (blockType == 2) ReadDynamicTables();
        else throw Invalid("reserved-block-type");
    }

    private void EndBlock()
    {
        blockOpen = false;
        literals = distances = null;
        if (finalBlock) finished = true;
        // Bytes following the final block are intentionally not interpreted.
        // The native PNG decoder accepts trailing DEFLATE bytes on this contract.
    }

    private void ReadDynamicTables()
    {
        int literalCount = ReadBits(5) + 257, distanceCount = ReadBits(5) + 1;
        int codeLengthCount = ReadBits(4) + 4;
        if (literalCount > 286) throw Invalid("literal-count");
        var codeLengths = new int[19];
        for (int i = 0; i < codeLengthCount; i++) codeLengths[CodeLengthOrder[i]] = ReadBits(3);
        var codeTree = new Huffman(codeLengths, allowEmpty: false, allowSingle: false);
        var lengths = new int[literalCount + distanceCount];
        for (int offset = 0; offset < lengths.Length;)
        {
            cancellation.ThrowIfCancellationRequested();
            int symbol = codeTree.Read(this);
            if (symbol <= 15) { lengths[offset++] = symbol; continue; }
            int count, value;
            if (symbol == 16)
            {
                if (offset == 0) throw Invalid("repeat-without-length");
                value = lengths[offset - 1]; count = ReadBits(2) + 3;
            }
            else if (symbol == 17) { value = 0; count = ReadBits(3) + 3; }
            else if (symbol == 18) { value = 0; count = ReadBits(7) + 11; }
            else throw Invalid("code-length-symbol");
            if (count > lengths.Length - offset) throw Invalid("code-length-repeat");
            while (count-- > 0) lengths[offset++] = value;
        }
        if (lengths[256] == 0) throw Invalid("missing-end-symbol");
        var literalLengths = new int[literalCount];
        var distanceLengths = new int[distanceCount];
        Array.Copy(lengths, literalLengths, literalCount);
        Array.Copy(lengths, literalCount, distanceLengths, 0, distanceCount);
        literals = new Huffman(literalLengths, allowEmpty: false, allowSingle: true);
        // A literal-only block may define no distance codes. Attempting a match
        // with that empty tree still fails locally when the symbol is requested.
        distances = new Huffman(distanceLengths, allowEmpty: true, allowSingle: true);
    }

    private int ReadBits(int count)
    {
        if (!TryBufferBits(count)) throw Invalid("truncated-before-final-block-end");
        int result = (int)(bits & ((1u << count) - 1));
        bits >>= count; bitCount -= count;
        return result;
    }

    // A failed lookahead leaves every real bit available for a shorter final
    // code. No zero padding is inserted and no bits are consumed here.
    private bool TryBufferBits(int count)
    {
        while (bitCount < count)
        {
            cancellation.ThrowIfCancellationRequested();
            int value = source.ReadByte();
            if (value < 0) return false;
            bits |= (uint)value << bitCount;
            bitCount += 8;
        }
        return true;
    }

    private sealed class Huffman
    {
        private readonly int[] counts = new int[16];
        private readonly int[] symbols;
        private readonly int maximumLength;
        private readonly int prefixBits;
        // Low four bits are the consumed length; zero is a slow-path entry.
        // The remaining bits hold the symbol. At most 512 entries (one KiB).
        private readonly ushort[] prefixes;
        internal Huffman(int[] lengths, bool allowEmpty, bool allowSingle)
        {
            int used = 0;
            foreach (int length in lengths)
            {
                if (length < 0 || length > 15) throw Invalid("huffman-length");
                if (length == 0) continue;
                counts[length]++; used++; maximumLength = Math.Max(maximumLength, length);
            }
            if (used == 0 && !allowEmpty) throw Invalid("empty-huffman-tree");
            int left = 1;
            for (int length = 1; length <= 15; length++)
            { left = (left << 1) - counts[length]; if (left < 0) throw Invalid("oversubscribed-huffman-tree"); }
            if (used != 0 && left != 0 && !(allowSingle && maximumLength == 1)) throw Invalid("incomplete-huffman-tree");
            symbols = new int[used];
            var offsets = new int[16];
            for (int length = 1; length < 15; length++) offsets[length + 1] = offsets[length] + counts[length];
            for (int symbol = 0; symbol < lengths.Length; symbol++)
                if (lengths[symbol] != 0) symbols[offsets[lengths[symbol]]++] = symbol;

            prefixBits = Math.Min(9, maximumLength);
            prefixes = prefixBits == 0 ? Array.Empty<ushort>() : new ushort[1 << prefixBits];
            int first = 0, index = 0;
            for (int length = 1; length <= prefixBits; length++)
            {
                for (int ordinal = 0; ordinal < counts[length]; ordinal++)
                {
                    int code = first + ordinal, reversed = 0;
                    for (int bit = 0; bit < length; bit++)
                    { reversed = (reversed << 1) | (code & 1); code >>= 1; }
                    ushort entry = (ushort)((symbols[index + ordinal] << 4) | length);
                    for (int prefix = reversed; prefix < prefixes.Length; prefix += 1 << length)
                        prefixes[prefix] = entry;
                }
                index += counts[length]; first = (first + counts[length]) << 1;
            }
        }
        internal int Read(PngDeflateStream stream)
        {
            if (prefixBits != 0 && stream.TryBufferBits(prefixBits))
            {
                int entry = prefixes[(int)(stream.bits & (uint)(prefixes.Length - 1))];
                int consumed = entry & 15;
                if (consumed != 0)
                {
                    stream.bits >>= consumed; stream.bitCount -= consumed;
                    return entry >> 4;
                }
            }
            // Long codes, unused single-code prefixes and short EOF lookahead
            // use the original validated canonical walk over actual bits.
            int code = 0, first = 0, index = 0;
            for (int length = 1; length <= maximumLength; length++)
            {
                code = (code << 1) | stream.ReadBits(1);
                int count = counts[length];
                if (code >= first && code - first < count) return symbols[index + code - first];
                index += count; first = (first + count) << 1;
            }
            throw Invalid("invalid-huffman-code");
        }
    }

    private static Huffman CreateFixedLiterals()
    {
        var lengths = new int[288];
        for (int i = 0; i < lengths.Length; i++) lengths[i] = i < 144 ? 8 : i < 256 ? 9 : i < 280 ? 7 : 8;
        return new Huffman(lengths, false, false);
    }
    private static Huffman CreateFixedDistances()
    {
        var lengths = new int[32];
        for (int i = 0; i < lengths.Length; i++) lengths[i] = 5;
        return new Huffman(lengths, false, false);
    }
    private void EnsureReadable()
    {
        if (disposed) throw new ObjectDisposedException(nameof(PngDeflateStream));
        cancellation.ThrowIfCancellationRequested();
    }
    private static InvalidDataException Invalid(string reason) => new InvalidDataException("first-build-deflate:" + reason);
    protected override void Dispose(bool disposing)
    {
        if (!disposed) { disposed = true; if (disposing && !leaveOpen) source.Dispose(); }
        base.Dispose(disposing);
    }
    public override bool CanRead => !disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => outputBytes; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
