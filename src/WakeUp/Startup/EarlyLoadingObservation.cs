// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Xml;
using Verse;

namespace WakeUp;

// Prepatcher places this boundary before the FIRST Mod constructor, including
// Wake-Up's own constructor. It never instantiates settings/mods ahead of order.
public static class EarlyLoadingObservation
{
    private static bool initialized;
    internal static string Report { get; private set; } = "Early loading observation was not selected.";

    public static void ExecuteDeferredAction(Action action)
    {
        if (LoadingObservationRuntime.Current == null) { action(); return; }
        LoadingInvocationObservation.ExecuteAction(action);
    }

    public static object CreateMod(Type type, object[] arguments)
    {
        Initialize();
        LoadingSession.Token? token = null;
        try { token = LoadingObservationRuntime.Begin("Mod constructors", type.FullName ?? type.Name,
            (arguments.FirstOrDefault() as ModContentPack)?.PackageId ?? "shared/unknown"); } catch { }
        Exception? failure = null;
        try { return Activator.CreateInstance(type, arguments); }
        catch (Exception exception) { failure = exception; throw; }
        finally { LoadingObservationRuntime.End(token, failure); }
    }

    private static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            bool selected, display;
            if (UserStartupSelection.HasExplicitSelection(args)) { selected = LoadingTimingRuntime.Selected(args); display = LoadingDisplayRuntime.Selected(args); }
            else
            {
                var owner = LoadedModManager.RunningModsListForReading.Single(m => m.assemblies.loadedAssemblies.Contains(typeof(WakeUpMod).Assembly));
                string file = Path.Combine(GenFilePaths.ConfigFolderPath, "Mod_" + new DirectoryInfo(owner.RootDir).Name + "_WakeUpMod.xml");
                if (!File.Exists(file) || new FileInfo(file).Length > 1024 * 1024) return;
                using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                var document = new XmlDocument { XmlResolver = null }; document.Load(reader);
                selected = Select(document);
                var node = document.SelectSingleNode("/SettingsBlock/ModSettings[@Class='WakeUp.WakeUpSettings']");
                display = !string.Equals(node?.SelectSingleNode("enabled")?.InnerText, "false", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(node?.SelectSingleNode("loadingDisplay")?.InnerText, "true", StringComparison.OrdinalIgnoreCase);
            }
            LoadingObservationRuntime.Configure(display, selected, GenFilePaths.SaveDataFolderPath);
        }
        catch { /* Inert observation cannot interrupt construction or selection. */ }
    }

    internal static bool Select(XmlDocument settings)
    {
        var nodes = settings.SelectNodes("/SettingsBlock/ModSettings[@Class='WakeUp.WakeUpSettings']");
        if (nodes == null || nodes.Count != 1) return false;
        var node = nodes[0]!;
        string? enabled = node.SelectSingleNode("enabled")?.InnerText;
        return (enabled == null || string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            && string.Equals(node.SelectSingleNode("loadingTimings")?.InnerText, "true", StringComparison.OrdinalIgnoreCase);
    }

    internal static void Finish()
    {
        Report = "Early mod constructors belong to the same loading session as subsequent XML, content and callback work.";
    }
}
