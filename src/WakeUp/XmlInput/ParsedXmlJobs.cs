// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace WakeUp;

// Only private, independent input work belongs here. The caller owns indexed
// results and publishes them in source order after every worker has stopped.
internal static class ParsedXmlJobs
{
    internal static void Run(int count, Action<int> action, bool parallel)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (!parallel || count <= 1)
        {
            for (int index = 0; index < count; index++) action(index);
            return;
        }

        int next = -1;
        ExceptionDispatchInfo? failure = null;
        void Record(Exception exception) => Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(exception), null);
        void Work()
        {
            try
            {
                while (Volatile.Read(ref failure) == null)
                {
                    int index = Interlocked.Increment(ref next);
                    if (index >= count || Volatile.Read(ref failure) != null) return;
                    action(index);
                }
            }
            catch (Exception exception) { Record(exception); }
        }

        var workers = new[]
        {
            new Thread(Work) { IsBackground = true, Name = "Wake-Up parsed XML 1" },
            new Thread(Work) { IsBackground = true, Name = "Wake-Up parsed XML 2" }
        };
        int started = 0;
        try
        {
            foreach (Thread worker in workers)
            {
                worker.Start();
                started++;
            }
            Work();
        }
        catch (Exception exception) { Record(exception); }
        finally
        {
            for (int index = 0; index < started; index++)
            {
                // A caller interruption must not leave private workers writing
                // after the caller has started its ordinary-loading fallback.
                while (true)
                {
                    try { workers[index].Join(); break; }
                    catch (ThreadInterruptedException exception) { Record(exception); }
                }
            }
        }
        failure?.Throw();
    }
}
