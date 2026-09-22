// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WakeUp;

internal static class BackgroundLoadingFocus
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    private static readonly int ProcessId = CurrentProcessId();
    private static int CurrentProcessId() { using var process = Process.GetCurrentProcess(); return process.Id; }

    // Some Windows Unity players report focused=true after a minimized,
    // nonactivating launch until an actual focus transition occurs. Consult the
    // real foreground owner as well; never activate, show or alter any window.
    // Called during owned loading and its post-load wait for actual focus.
    internal static bool IsFocused()
    {
        if (!Application.isFocused) return false;
        if (Application.platform != RuntimePlatform.WindowsPlayer) return true;
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
            return foreground == ProcessId;
        }
        catch (DllNotFoundException) { return true; }
        catch (EntryPointNotFoundException) { return true; }
    }
}
