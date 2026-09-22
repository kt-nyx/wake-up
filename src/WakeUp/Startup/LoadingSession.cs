// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace WakeUp;

// Observation contains labels and numbers only. It never owns the work, its
// exceptions, a mod, an asset, an iterator or a queued callback.
internal sealed class LoadingSession
{
    internal const int RecordLimit = 32768;
    internal const int DetailPerStageLimit = 512;
    internal const int DepthLimit = 16;
    internal const int ThreadLimit = 128;
    internal const int ErrorLimit = 256;
    internal const int StageLimit = 256;
    internal const int UnobservedNoteLimit = 32;
    internal const int PackageSummaryLimit = 1024;
    private const string UnknownPackage = "shared/unknown";
    private const string OtherStages = "Other stages (summary table limit)";
    private readonly object gate = new();
    private readonly Func<double> clock;
    private readonly bool recordTimings;
    private readonly double origin;
    private readonly Dictionary<int, ThreadWork> threads = new();
    private readonly List<Token> records = new();
    private readonly List<string> errors = new();
    private readonly Dictionary<string, long> unobservedNotes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StageCount> progress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> detailCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Totals> packageTotals = new(StringComparer.Ordinal) { [UnknownPackage] = new Totals() };
    private readonly Dictionary<string, Totals> stageTotals = new(StringComparer.Ordinal) { [OtherStages] = new Totals() };
    private bool active = true;
    private long sequence, version, omitted, errorCount, unfinished;
    private long unobserved, unobservedTextOmitted;
    private long detailOmitted, scopeOmitted, progressOmitted, packageOverflowSpans, stageOverflowSpans;
    private string stage = "Loading", work = "Waiting for observed work", package = "shared/unknown", outcome = "In progress";
    private long completed;
    private long? total;
    private int activeScopes;
    private double ended;

    internal LoadingSession(string name, bool recordTimings, Func<double>? clock = null)
    {
        Id = Guid.NewGuid().ToString("N");
        Name = Plain(name, 240);
        this.recordTimings = recordTimings;
        this.clock = clock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        origin = recordTimings ? this.clock() : 0;
    }

    internal string Id { get; }
    internal string Name { get; }
    internal bool IsActive { get { lock (gate) return active; } }
    internal long Omitted { get { lock (gate) return omitted; } }
    internal long Unobserved { get { lock (gate) return unobserved; } }
    internal long Version { get { lock (gate) return version; } }

    internal sealed class Token
    {
        internal readonly string SessionId, Stage, Work, Package;
        internal readonly long Sequence, ParentSequence;
        internal readonly int ThreadId, Depth;
        internal readonly double StartedSeconds;
        internal double InclusiveSeconds, ExclusiveSeconds;
        internal bool Ended;
        internal string Outcome = "Still running";
        internal string Error = "";
        // Accumulated union of intervals in which another observed same-thread
        // scope is nested inside this one. Grandchildren are not counted twice.
        internal double NestedSeconds;
        internal Token(string sessionId, long sequence, long parent, int threadId, int depth,
            string stage, string work, string package, double started)
        {
            SessionId = sessionId; Sequence = sequence; ParentSequence = parent;
            ThreadId = threadId; Depth = depth; Stage = stage; Work = work;
            Package = package; StartedSeconds = started;
        }
    }

    private sealed class ThreadWork
    {
        internal readonly List<Token> Active = new();
        internal double Updated;
    }

    private readonly struct StageCount
    {
        internal readonly string Work;
        internal readonly long Completed;
        internal readonly long? Total;
        internal StageCount(string work, long completed, long? total)
        { Work = work; Completed = completed; Total = total; }
    }

    internal readonly struct View
    {
        internal readonly string Stage, Work, Package;
        internal readonly long Completed, Errors, Omitted;
        internal readonly long? Total;
        internal readonly bool Active;
        internal readonly int ActiveScopes;
        internal View(string stage, string work, string package, long completed, long? total,
            long errors, bool active, int activeScopes, long omitted)
        {
            Stage = stage; Work = work; Package = package; Completed = completed; Total = total;
            Errors = errors; Active = active; ActiveScopes = activeScopes; Omitted = omitted;
        }
    }

    internal Token? Begin(string stage, string work, string package = "shared/unknown")
    {
        lock (gate)
        {
            if (!active) return null;
            version++;
            int threadId = Thread.CurrentThread.ManagedThreadId;
            if (!threads.TryGetValue(threadId, out ThreadWork? thread))
            {
                if (threads.Count == ThreadLimit) { omitted++; scopeOmitted++; return null; }
                threads.Add(threadId, thread = new ThreadWork());
            }
            if (thread.Active.Count == DepthLimit) { omitted++; scopeOmitted++; return null; }
            double now = Now();
            Advance(thread, now);
            var token = new Token(Id, ++sequence,
                thread.Active.Count == 0 ? 0 : thread.Active[thread.Active.Count - 1].Sequence,
                threadId, thread.Active.Count, Plain(stage, 160), Plain(work, 320), Plain(package, 240), now);
            thread.Active.Add(token);
            activeScopes++;
            if (recordTimings)
            {
                BeginTotals(token);
                detailCounts.TryGetValue(token.Stage, out int count);
                if (records.Count < RecordLimit && count < DetailPerStageLimit
                    && (count != 0 || detailCounts.Count < StageLimit))
                {
                    records.Add(token);
                    detailCounts[token.Stage] = count + 1;
                }
                else { omitted++; detailOmitted++; }
            }
            Publish(token);
            return token;
        }
    }

    internal void End(Token? token, Exception? exception)
    {
        lock (gate)
        {
            if (!active) return;
            if (token == null)
            {
                if (exception != null) AddError("Untracked work: " + Describe(exception));
                return;
            }
            // Repeated/finally calls and a token from another session are inert.
            if (token.SessionId != Id || token.Ended || !threads.TryGetValue(token.ThreadId, out ThreadWork? thread)) return;
            double now = Now();
            Advance(thread, now);
            Complete(token, now, exception == null ? "Completed" : "Error");
            if (exception != null)
            {
                token.Error = Describe(exception);
                AddError("Span " + token.Sequence + " (" + token.Package + "; " + token.Work + "): " + token.Error);
            }
            thread.Active.Remove(token);
            activeScopes--;
            if (thread.Active.Count == 0) threads.Remove(token.ThreadId);
            version++;
            PublishLatest();
        }
    }

    internal void Progress(string stage, string work, long completed, long? total)
    {
        lock (gate)
        {
            if (!active) return;
            string label = Plain(stage, 160);
            this.work = Plain(work, 320);
            if (this.stage != label) package = "shared/unknown";
            this.stage = label;
            this.completed = Math.Max(0, completed);
            // An inconsistent denominator is unknown, never a fabricated 100%.
            this.total = total.HasValue && total.Value >= this.completed ? total : null;
            if (progress.ContainsKey(label) || progress.Count < StageLimit)
                progress[label] = new StageCount(this.work, this.completed, this.total);
            else { omitted++; progressOmitted++; }
            version++;
        }
    }

    internal void Error(string message)
    {
        lock (gate) { if (active) AddError(message); }
    }

    // Missing coverage is separate from a game error. Counts describe notices,
    // not an invented number of calls inside an entirely unobserved region.
    internal void NoteUnobserved(string message)
    {
        lock (gate)
        {
            if (!active) return;
            unobserved++;
            string note = Plain(message, 512);
            if (unobservedNotes.TryGetValue(note, out long count)) unobservedNotes[note] = count + 1;
            else if (unobservedNotes.Count < UnobservedNoteLimit) unobservedNotes.Add(note, 1);
            else unobservedTextOmitted++;
            version++;
        }
    }

    internal void Finish(string outcome)
    {
        lock (gate)
        {
            if (!active) return;
            ended = Now();
            foreach (ThreadWork thread in threads.Values)
            {
                Advance(thread, ended);
                foreach (Token token in thread.Active)
                {
                    Complete(token, ended, "Unfinished at session end");
                    unfinished++;
                }
            }
            threads.Clear();
            activeScopes = 0;
            active = false;
            this.outcome = Plain(outcome, 512);
            stage = "Finished"; work = this.outcome; package = "shared/unknown";
            completed = 0; total = null;
            version++;
        }
    }

    internal View Snapshot()
    {
        lock (gate) return new View(stage, work, package, completed, total, errorCount, active, activeScopes, omitted);
    }

    internal readonly struct Context
    {
        internal readonly string Stage, Package;
        internal Context(string stage, string package) { Stage = stage; Package = package; }
    }

    // The GUI snapshot is intentionally global. Native marker inheritance must
    // instead use this thread's innermost live scope, including explicit scopes
    // such as a Def factory, Mod constructor or deferred callback. Nothing is
    // inherited from a completed span, another worker, or a progress message.
    internal Context? CurrentThreadContext()
    {
        lock (gate)
        {
            if (!active || !threads.TryGetValue(Thread.CurrentThread.ManagedThreadId, out ThreadWork? thread)
                || thread.Active.Count == 0) return null;
            Token token = thread.Active[thread.Active.Count - 1];
            return new Context(token.Stage, token.Package);
        }
    }

    private double Now() => recordTimings ? Math.Max(0, clock() - origin) : 0;

    private void Advance(ThreadWork thread, double now)
    {
        if (recordTimings)
        {
            double elapsed = Math.Max(0, now - thread.Updated);
            for (int i = 0; i < thread.Active.Count - 1; i++) thread.Active[i].NestedSeconds += elapsed;
        }
        thread.Updated = Math.Max(thread.Updated, now);
    }

    private void Complete(Token token, double now, string outcome)
    {
        token.InclusiveSeconds = Math.Max(0, now - token.StartedSeconds);
        token.ExclusiveSeconds = Math.Max(0, token.InclusiveSeconds - token.NestedSeconds);
        token.Outcome = outcome;
        token.Ended = true;
        if (recordTimings)
        {
            AddCompleted(packageTotals.TryGetValue(token.Package, out Totals? packageValue) ? packageValue : packageTotals[UnknownPackage], token);
            AddCompleted(stageTotals.TryGetValue(token.Stage, out Totals? stageValue) ? stageValue : stageTotals[OtherStages], token);
        }
    }

    private void BeginTotals(Token token)
    {
        if (!packageTotals.TryGetValue(token.Package, out Totals? packageValue))
        {
            if (packageTotals.Count < PackageSummaryLimit) packageTotals.Add(token.Package, packageValue = new Totals());
            else { packageValue = packageTotals[UnknownPackage]; packageOverflowSpans++; }
        }
        packageValue.Spans++;
        packageValue.Unfinished++;
        if (!stageTotals.TryGetValue(token.Stage, out Totals? stageValue))
        {
            if (stageTotals.Count < StageLimit) stageTotals.Add(token.Stage, stageValue = new Totals());
            else { stageValue = stageTotals[OtherStages]; stageOverflowSpans++; }
        }
        stageValue.Spans++;
        stageValue.Unfinished++;
    }

    private static void AddCompleted(Totals value, Token token)
    {
        if (token.Outcome != "Unfinished at session end") value.Unfinished--;
        value.Inclusive += token.InclusiveSeconds;
        value.Exclusive += token.ExclusiveSeconds;
    }

    private void AddError(string message)
    {
        errorCount++;
        if (errors.Count < ErrorLimit) errors.Add(Plain(message, 1024));
        version++;
    }

    private void Publish(Token token)
    {
        stage = token.Stage; work = token.Work; package = token.Package;
        if (progress.TryGetValue(stage, out StageCount value)) { completed = value.Completed; total = value.Total; }
        else { completed = 0; total = null; }
    }

    private void PublishLatest()
    {
        Token? latest = null;
        foreach (ThreadWork thread in threads.Values)
        {
            Token candidate = thread.Active[thread.Active.Count - 1];
            if (latest == null || candidate.Sequence > latest.Sequence) latest = candidate;
        }
        if (latest != null) Publish(latest);
        else { stage = "Loading"; work = "Waiting for next observed work"; package = "shared/unknown"; completed = 0; total = null; }
    }

    // Called on explicit inspection/export or session completion, never repaint.
    // Copy under the lock, then format outside it so reports cannot hold up work.
    internal string RenderReport()
    {
        string status;
        long missing, failures, incomplete, missedNotices, missedNoteText;
        long missingDetails, missingScopes, missingProgress, groupedPackages, groupedStages;
        double elapsed;
        bool running;
        string[] errorRows;
        KeyValuePair<string, StageCount>[] stages;
        KeyValuePair<string, long>[] missingNotes;
        KeyValuePair<string, Totals>[] packages, observedStages;
        Record[] spans;
        lock (gate)
        {
            running = active; elapsed = running ? Now() : ended; status = outcome;
            missing = omitted; failures = errorCount; incomplete = unfinished;
            missedNotices = unobserved; missedNoteText = unobservedTextOmitted;
            missingDetails = detailOmitted; missingScopes = scopeOmitted; missingProgress = progressOmitted;
            groupedPackages = packageOverflowSpans; groupedStages = stageOverflowSpans;
            packages = CopyTotals(packageTotals);
            observedStages = CopyTotals(stageTotals);
            errorRows = errors.ToArray();
            missingNotes = new KeyValuePair<string, long>[unobservedNotes.Count];
            ((ICollection<KeyValuePair<string, long>>)unobservedNotes).CopyTo(missingNotes, 0);
            stages = new KeyValuePair<string, StageCount>[progress.Count];
            ((ICollection<KeyValuePair<string, StageCount>>)progress).CopyTo(stages, 0);
            spans = new Record[records.Count];
            for (int i = 0; i < records.Count; i++) spans[i] = new Record(records[i]);
        }
        var text = new StringBuilder();
        text.AppendLine("Wake-Up loading session: " + Name).AppendLine("Session ID: " + Id)
            .AppendLine("Outcome: " + status + (running ? " (snapshot while loading)" : ""))
            .AppendLine("Timing collection: " + (recordTimings ? "enabled" : "off; current progress and errors only"));
        if (recordTimings) text.AppendLine("Elapsed since observation began: " + Seconds(elapsed) + " seconds (diagnostic, not menu startup time).");
        text.AppendLine("Retained spans: " + spans.Length + "/" + RecordLimit + "; omitted observations: " + missing
            + "; errors: " + failures + "; unfinished scopes at session end: " + incomplete + ".")
            .AppendLine("Omission breakdown: " + missingDetails + " detail rows (their aggregates are preserved); "
                + missingScopes + " untracked scopes (absent from aggregates); " + missingProgress + " stage-counter updates.")
            .AppendLine("Unobserved-region notices: " + missedNotices + "; notice text omitted: " + missedNoteText + ". These counts are notices, not an estimate of missing calls.")
            .AppendLine("Coverage: only instrumented work after this session began is observed. Unhooked methods, earlier work and gaps between spans are unobserved; shared/unknown means no reliable mod attribution.")
            .AppendLine("Inclusive time includes nested work. Exclusive time subtracts the union of observed same-thread nested intervals, so grandchildren are not subtracted twice. Unobserved nested work remains included.")
            .AppendLine("Durations are elapsed span intervals, including waits and gaps where loading yields a frame before continuing; they are not CPU execution time.")
            .AppendLine("Different threads can overlap. A parent may wait for work on another thread. Neither inclusive nor exclusive column sums equal whole loading or menu elapsed time. The independent menu observer is separate.")
            .AppendLine("Limits: " + DepthLimit + " active scopes/thread, " + ThreadLimit + " simultaneous observed threads, "
                + StageLimit + " stage counters, " + ErrorLimit + " error messages. Detail retention: first " + DetailPerStageLimit
                + " spans per stage within " + RecordLimit + " total rows. A noisy stage cannot use more than its quota; aggregate collection and current progress continue after detail limits.")
            .AppendLine("Completion describes observed boundaries, not proof that every game asset or later callback succeeded.");
        if (missingNotes.Length != 0)
        {
            text.AppendLine().AppendLine("Known missed / unobserved regions:");
            foreach (var item in missingNotes) text.AppendLine(item.Value + " notice(s): " + item.Key);
            if (missedNoteText != 0) text.AppendLine("Additional notices had distinct text beyond the " + UnobservedNoteLimit + "-note limit: " + missedNoteText);
        }
        if (stages.Length != 0)
        {
            text.AppendLine().AppendLine("Last reported work counters (units apply within each stage only):");
            foreach (var item in stages)
                text.AppendLine(item.Key + ": " + item.Value.Completed + " / "
                    + (item.Value.Total?.ToString(CultureInfo.InvariantCulture) ?? "unknown") + " — " + item.Value.Work);
        }
        if (failures != 0)
        {
            text.AppendLine().AppendLine("Observed errors:");
            foreach (string row in errorRows) text.AppendLine(row);
            if (failures > errorRows.Length) text.AppendLine("Additional error messages omitted: " + (failures - errorRows.Length));
        }
        if (recordTimings)
        {
            text.AppendLine().AppendLine("Aggregate coverage includes ALL observed spans, including spans omitted from detailed history. Work rejected by active-scope limits and unobserved regions is absent.")
                .AppendLine("Inclusive sums can count nested work repeatedly; exclusive sums can overlap across threads. Unfinished-at-end spans contribute partial durations; still-running spans contribute no duration yet.");
            AppendTotals(text, "Per-package observed span sums (seconds; NOT wall time):", "Package", packages);
            if (groupedPackages != 0)
                text.AppendLine("Package table limit: " + PackageSummaryLimit + ". " + groupedPackages
                    + " spans with additional package labels are included in shared/unknown totals. Retained detail rows preserve their labels.");
            AppendTotals(text, "Per-stage observed span sums (seconds; NOT wall time):", "Stage", observedStages);
            if (groupedStages != 0)
                text.AppendLine("Stage table limit: " + StageLimit + ". " + groupedStages + " spans are grouped under " + OtherStages + ".");
        }
        if (spans.Length != 0)
        {
            text.AppendLine().AppendLine("Spans in start order; seconds; zero parent means no observed same-thread parent:")
                .AppendLine("Sequence\tParent\tThread\tDepth\tStart\tInclusive\tExclusive\tPackage\tStage\tWork\tOutcome\tError");
            foreach (Record row in spans) row.AppendTo(text);
        }
        return text.ToString();
    }

    private sealed class Totals
    {
        internal long Spans, Unfinished;
        internal double Inclusive, Exclusive;
    }

    private static KeyValuePair<string, Totals>[] CopyTotals(Dictionary<string, Totals> source)
    {
        var rows = new KeyValuePair<string, Totals>[source.Count];
        int index = 0;
        foreach (var pair in source)
            rows[index++] = new KeyValuePair<string, Totals>(pair.Key, new Totals { Spans = pair.Value.Spans,
                Unfinished = pair.Value.Unfinished, Inclusive = pair.Value.Inclusive, Exclusive = pair.Value.Exclusive });
        Array.Sort(rows, (left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
        return rows;
    }

    private static void AppendTotals(StringBuilder text, string title, string label, KeyValuePair<string, Totals>[] rows)
    {
        text.AppendLine().AppendLine(title)
            .AppendLine(label + "\tObserved spans\tUnfinished / still running\tInclusive sum\tExclusive sum");
        foreach (var row in rows)
        {
            if (row.Value.Spans == 0) continue;
            text.Append(row.Key).Append('\t').Append(row.Value.Spans).Append('\t').Append(row.Value.Unfinished).Append('\t')
                .Append(Seconds(row.Value.Inclusive)).Append('\t').AppendLine(Seconds(row.Value.Exclusive));
        }
    }

    private readonly struct Record
    {
        private readonly long sequence, parent;
        private readonly int thread, depth;
        private readonly double started, inclusive, exclusive;
        private readonly bool ended;
        private readonly string package, stage, work, outcome, error;
        internal Record(Token token)
        {
            sequence = token.Sequence; parent = token.ParentSequence; thread = token.ThreadId; depth = token.Depth;
            started = token.StartedSeconds; inclusive = token.InclusiveSeconds; exclusive = token.ExclusiveSeconds;
            ended = token.Ended; package = token.Package; stage = token.Stage; work = token.Work; outcome = token.Outcome; error = token.Error;
        }
        internal void AppendTo(StringBuilder text)
        {
            text.Append(sequence).Append('\t').Append(parent).Append('\t').Append(thread).Append('\t').Append(depth).Append('\t')
                .Append(Seconds(started)).Append('\t').Append(ended ? Seconds(inclusive) : "pending").Append('\t')
                .Append(ended ? Seconds(exclusive) : "pending").Append('\t').Append(package).Append('\t').Append(stage)
                .Append('\t').Append(work).Append('\t').Append(outcome).Append('\t').AppendLine(error);
        }
    }

    private static string Seconds(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
    private static string Describe(Exception exception) => Plain(exception.GetType().FullName + ": " + exception.Message, 1024);
    private static string Plain(string? value, int limit)
    {
        if (string.IsNullOrEmpty(value)) return "unspecified";
        string result = value!.Length > limit ? value.Substring(0, limit) + "…" : value;
        return result.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
    }
}
