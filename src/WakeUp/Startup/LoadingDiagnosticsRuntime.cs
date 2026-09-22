// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Verse;

namespace WakeUp;

// Explicit, one-launch diagnostics. No asset/document is retained after Capture.
internal static class LoadingDiagnosticsRuntime
{
    internal const long MaximumBytes = 256L * 1024 * 1024;
    internal const long MaximumFileBytes = 96L * 1024 * 1024;
    private const string RequestName = "xml-export-next-launch.request";
    private static readonly object gate = new();
    private static readonly HashSet<string> captured = new(StringComparer.Ordinal);
    private static bool armed, sourcesWritten, completionQueued;
    private static string launchDirectory = "";
    private static long written;
    internal static string ExportDirectory { get; private set; } = "";
    internal static string Status { get; private set; } = "XML export is off. Explicitly request the next launch in settings.";

    internal static string RequestNextLaunch(string root)
    {
        try
        {
            string folder = Path.Combine(root, "WakeUp");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, RequestName), "Explicit one-launch combined/processed XML and source export request.\n", Encoding.UTF8);
            return Status = "XML export requested for the next launch. Restart once; output: " + Path.Combine(folder, "XmlExports") + ". Up to three launches / 256 MiB total.";
        }
        catch (Exception e) { return Status = "Could not request XML export: " + e.Message; }
    }
    internal static string CancelRequest(string root)
    {
        try
        {
            File.Delete(Path.Combine(root, "WakeUp", RequestName));
            return Status = "Pending next-launch XML export request cancelled. An export already executing is unaffected.";
        }
        catch (Exception e) { return Status = "Could not cancel XML export request: " + e.Message; }
    }
    internal static void Initialize(string root)
    {
        lock (gate)
        {
            armed = false; sourcesWritten = false; completionQueued = false; written = 0; captured.Clear(); launchDirectory = "";
            ExportDirectory = Path.Combine(root, "WakeUp", "XmlExports");
            try
            {
                string request = Path.Combine(root, "WakeUp", RequestName);
                if (!File.Exists(request)) return;
                // Consume before any work: a failed or interrupted startup cannot
                // silently cause another expensive export on each later launch.
                File.Delete(request);
                Directory.CreateDirectory(ExportDirectory);
                Prune(0);
                launchDirectory = Path.Combine(ExportDirectory, "export-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(launchDirectory);
                armed = true;
                Status = "Requested XML export armed for observed combined/processed stages: " + launchDirectory;
            }
            catch (Exception e) { Status = "Requested XML export could not start: " + e.Message; }
        }
    }
    internal static void ObserveStartupCompletion(Action<Action>? schedule = null)
    {
        string requestDirectory;
        lock (gate)
        {
            if (!armed || completionQueued) return;
            completionQueued = true;
            requestDirectory = launchDirectory;
        }
        // GOG's native loading event performs XML combination and patching
        // before it drains this existing post-event callback queue. Register
        // only for an explicit request, independently of display/profiling.
        // No guarded method is patched and native callback order is unchanged.
        (schedule ?? LongEventHandler.ExecuteWhenFinished)(() =>
        {
            lock (gate)
            {
                if (launchDirectory != requestDirectory) return;
                Complete();
            }
        });
    }
    internal static void Capture(string stage, XmlDocument document,
        IReadOnlyDictionary<XmlNode, LoadableXmlAsset>? provenance = null)
    {
        lock (gate)
        {
            if (!armed || !captured.Add(stage)) return;
            // At most two actual stage snapshots; never export every repeated
            // load/hot-reload merely because startup requested diagnostics once.
            if (captured.Count > 2) return;
            string label = captured.Count.ToString("D2", CultureInfo.InvariantCulture) + "-" + SafeName(stage);
            try
            {
                WriteText(label + "-manifest.txt", writer =>
                {
                    writer.WriteLine("Wake-Up explicit XML diagnostics; UTC " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    writer.WriteLine("Stage: " + stage);
                    writer.WriteLine("This is the actual observed document at this boundary. Combined input precedes patches; processed output includes changes up to the named stage.");
                    writer.WriteLine("Origins do not identify every mod that subsequently patched a node. Missing origin means shared/unknown/generated work.");
                    writer.WriteLine("Ordered active loadout:");
                    foreach (var mod in LoadedModManager.RunningModsListForReading)
                        writer.WriteLine(mod.loadOrder + "\t" + Escape(mod.PackageId) + "\t" + Escape(mod.Name));
                    writer.WriteLine("Storage limit: three exports / 256 MiB total; each XML file is limited to 96 MiB. Status files identify incomplete output.");
                });
                WriteXml(label + ".xml", document);
                WriteText(label + "-origins.tsv", writer =>
                {
                    writer.WriteLine("element_index\telement\tdefName\tpackage\tsource_folder\tsource_name");
                    int index = 0;
                    if (document.DocumentElement == null) return;
                    foreach (XmlNode node in document.DocumentElement.ChildNodes)
                    {
                        if (node.NodeType != XmlNodeType.Element) continue;
                        LoadableXmlAsset? asset = null;
                        provenance?.TryGetValue(node, out asset);
                        writer.WriteLine((index++).ToString(CultureInfo.InvariantCulture) + "\t" + Escape(node.Name) + "\t" + Escape(node["defName"]?.InnerText)
                            + "\t" + Escape(asset?.mod?.PackageId ?? "shared/unknown") + "\t" + Escape(asset?.fullFolderPath) + "\t" + Escape(asset?.name));
                    }
                });
                if (!sourcesWritten && provenance != null)
                {
                    sourcesWritten = true;
                    // Original loaded source trees, not fresh reads of possibly
                    // changed files. Deduplication lasts only for this call.
                    Write("sources.xml", stream =>
                    {
                        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
                        writer.WriteStartElement("LoadedSources");
                        foreach (var source in provenance.Values.Distinct())
                        {
                            writer.WriteStartElement("Source");
                            writer.WriteAttributeString("package", source.mod?.PackageId ?? "shared/unknown");
                            writer.WriteAttributeString("folder", source.fullFolderPath ?? "");
                            writer.WriteAttributeString("name", source.name ?? "");
                            writer.WriteAttributeString("available", (source.xmlDoc?.DocumentElement != null).ToString());
                            source.xmlDoc?.DocumentElement?.WriteTo(writer);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    });
                }
                WriteText(label + "-status.txt", writer => writer.WriteLine("Stage document and available origins exported. A source tree may be unavailable if its native asset did not retain it."));
                Status = "XML stage exported: " + stage + ". " + launchDirectory;
            }
            catch (Exception e)
            {
                Status = "XML export incomplete at " + stage + ": " + e.Message + ". Existing complete files: " + launchDirectory;
                try { WriteText(label + "-status.txt", writer => writer.WriteLine(Status)); } catch { }
            }
            if (captured.Count >= 2) armed = false;
        }
    }
    internal static void Complete()
    {
        lock (gate)
        {
            if (!armed) return;
            armed = false;
            Status = "Requested XML export ended after " + captured.Count + "/2 observed stages. Missing stages were unsupported, skipped or interrupted; no complete export is claimed. " + launchDirectory;
            try { WriteText("completion-status.txt", writer => writer.WriteLine(Status)); } catch { }
        }
    }
    private static string SafeName(string value)
        => new(value.Take(60).Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-').ToArray());
    private static string Escape(string? value) => (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
    private static void WriteXml(string name, XmlDocument document)
        => Write(name, stream =>
        {
            using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, CloseOutput = false });
            document.WriteTo(writer);
        });
    private static void WriteText(string name, Action<TextWriter> action)
        => Write(name, stream => { using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true); action(writer); });
    private static void Write(string name, Action<Stream> action)
    {
        // Reserve a little room for an explicit incomplete receipt.
        long limit = Math.Min(MaximumFileBytes, MaximumBytes - written - (name.EndsWith("-status.txt", StringComparison.Ordinal) ? 0 : 16384));
        if (limit <= 0) throw new IOException("256 MiB export limit reached");
        Prune(limit);
        string path = Path.Combine(launchDirectory, name), pending = path + ".partial";
        try
        {
            using (var file = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bounded = new BoundedWriteStream(file, limit)) action(bounded);
            if (File.Exists(path)) { written -= new FileInfo(path).Length; File.Delete(path); }
            File.Move(pending, path);
            written += new FileInfo(path).Length;
        }
        catch { if (File.Exists(pending)) File.Delete(pending); throw; }
    }
    private static void Prune(long reservation)
    {
        var folders = new DirectoryInfo(ExportDirectory).GetDirectories("export-*")
            .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0).OrderBy(d => d.Name, StringComparer.Ordinal).ToList();
        long size = folders.Sum(d => d.GetFiles().Sum(f => f.Length));
        // Only this service's flat export directories are eligible. Refuse to
        // traverse user-added directories or links during retention cleanup.
        while (folders.Count + (launchDirectory.Length == 0 ? 1 : 0) > 3 || size + reservation > MaximumBytes)
        {
            var oldest = folders.FirstOrDefault(d => !string.Equals(d.FullName, launchDirectory, StringComparison.OrdinalIgnoreCase));
            if (oldest == null) break;
            if (oldest.GetDirectories().Length != 0 || oldest.GetFiles().Any(f => (f.Attributes & FileAttributes.ReparsePoint) != 0))
                throw new IOException("Export retention found an unexpected nested directory or link; inspect " + oldest.FullName);
            long removed = oldest.GetFiles().Sum(f => f.Length);
            foreach (var file in oldest.GetFiles()) file.Delete();
            oldest.Delete(); folders.Remove(oldest); size -= removed;
        }
    }
    private sealed class BoundedWriteStream : Stream
    {
        private readonly Stream target;
        private readonly long limit;
        private long count;
        internal BoundedWriteStream(Stream target, long limit) { this.target = target; this.limit = limit; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => count;
        public override long Position { get => count; set => throw new NotSupportedException(); }
        public override void Flush() => target.Flush();
        public override void Write(byte[] buffer, int offset, int length)
        {
            if (length > limit - count) throw new IOException("XML export storage limit reached; partial file discarded");
            target.Write(buffer, offset, length); count += length;
        }
        public override int Read(byte[] buffer, int offset, int length) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
