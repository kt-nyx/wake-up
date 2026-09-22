// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Security;

namespace WakeUp;

internal static class WarmSourceSize
{
    // Native DirectoryInfo enumeration already initializes FileInfo's size.
    // Length reuses that data; forcing Refresh here repeats owner-thread IO.
    // A provider-created FileInfo without initialized data keeps the public
    // getter's ordinary per-file query. No additional scan or private layout.
    // This is only a reservation hint: the worker checks its opened file's
    // actual length before allocating, then authenticates all source bytes.
    internal static bool TryGet(FileSystemInfo? source, out long bytes)
    {
        bytes = 0;
        if (!(source is FileInfo file)) return false;
        try { bytes = file.Length; return bytes > 0; }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is NotSupportedException || error is SecurityException)
        { return false; }
    }
}
