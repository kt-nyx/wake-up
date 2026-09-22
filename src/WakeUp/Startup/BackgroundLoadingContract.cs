// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace WakeUp;

internal static class BackgroundLoadingContract
{
    internal static readonly MethodInfo[] Targets = FindTargets();
    // Hashes describe only the functions this feature relies on, not a whole
    // game version. Collected from the frozen, unmodified development reference.
    internal static readonly string[] Expected = new[] {
        "F8507DB072D6B6612B60DB9E75F5BA4CF1B728B1549E32C49257A2BBB032ED7F", // Verse.Game+<>c:Boolean <LoadGame>b__81_0(Verse.Map)
        "6E24EF393EBFDF60D9E475526C86649FA9D9530339AA60E66D53A5E525A68E02", // Verse.Game+<>c:Void <LoadGame>b__81_1()
        "20EEDB1474642522DFD40FD24F01B7A54E63A9F7657A65088B725575E918F9B6", // Verse.Game:Void LoadGame()
        "8AABF4C12FB167C16ABEC1324471541D536E103402D9BB1F41ED05729981AC0F", // Verse.GameAndMapInitExceptionHandlers:Void ErrorWhileLoadingGame(System.Exception)
        "D2E592F70DAE9C18B5E9D091D4D360D7796A1EE8686CE682F48D87CFA3E1B2F5", // Verse.GameDataSaveLoader+<>c__DisplayClass30_0:Void <LoadGame>g__PreLoadAct|0()
        "37144AE8FB26685BF39BA5F2681A3E930B788BAC9B1371BA848D4CBADF9629C3", // Verse.GameDataSaveLoader:Void LoadGame(System.String)
        "516CEAD97E9F7ED1F21DDEA230C1C41FFCACE023756EFE5AB54DA2C9A0E7C5F4", // Verse.GenScene+<>c:Void <GoToMainMenu>b__6_0()
        "61DBBEA34F84D4BADB73D5852FC8850DE68F58DDC99946112A6F8AD3E5F322B7", // Verse.GenScene:Void GoToMainMenu()
        "0B9C72F3E6307C0817087C2D373EA99BC30E1396916D1AC18429067B4058E96A", // Verse.LongEventHandler+<>c:Void <UpdateCurrentAsynchronousEvent>b__28_0()
        "480D06ABB3673F853C9972117725B5B64328F07B0D2AA38B509945EAE98B7651", // Verse.LongEventHandler+QueuedLongEvent:Boolean get_ShouldWaitUntilDisplayed()
        "16BD863CA04974F1768CAC30749E882E859B09C07267F5B07F910FB123E196A6", // Verse.LongEventHandler+QueuedLongEvent:Boolean get_UseStandardWindow()
        "9E0E5E80C8C34D3825B70A15CF542B8468A2D0253C241D3AAE49C0E9976F0421", // Verse.LongEventHandler:Boolean get_AnyEventNowOrWaiting()
        "853DDFAC551F7FC620517DACA84D653B883CFE11B157A41AB4F28182BF78CEEB", // Verse.LongEventHandler:Boolean get_AnyEventWhichDoesntUseStandardWindowNowOrWaiting()
        "61299A84AF956BE47C3FAB4DE8FAD0773EBC1890E778A966A180EE72FF30C270", // Verse.LongEventHandler:Boolean get_ShouldWaitForEvent()
        "64AD37C4630562BE12656B7371E984872869DDD7B4CEA4ED79C03C4B18D5D03D", // Verse.LongEventHandler:Void ClearQueuedEvents()
        "D66124A98DE410C2F59734EA7D76D304D4D16EBD9DA978200E992B5FC9BFA526", // Verse.LongEventHandler:Void ExecuteToExecuteWhenFinished()
        "255907211728E3C37B58485BBC25482F1E78F2F2ED55BDD2CC930913009F01A5", // Verse.LongEventHandler:Void ExecuteWhenFinished(System.Action)
        "330F15627699A3DEAA1C9D2A97694D116BFF9E45CCF5293686AF67E4C0A2A280", // Verse.LongEventHandler:Void ForceExecuteToExecuteWhenFinished()
        "43145D89CF55CBD93858867B52D713F58585A60838318743A0EF9CBFE867DEFA", // Verse.LongEventHandler:Void LongEventsUpdate(Boolean ByRef)
        "54FE5C4EBA54F20A103F94D11E8F4597C790826E5D7CBAAD1660C023E51180A6", // Verse.LongEventHandler:Void QueueLongEvent(System.Action, System.String, Boolean, System.Action`1[System.Exception], Boolean, Boolean, System.Action)
        "89C6E90C8710CC397C4617B0047577755E68F4348EC8CABF0685DFEB6CDA29FC", // Verse.LongEventHandler:Void QueueLongEvent(System.Action, System.String, System.String, Boolean, System.Action`1[System.Exception], Boolean, Boolean)
        "67311333B0324C0054058FFE4ADEAB79288BFB570347443AE7FD99A93FF5A503", // Verse.LongEventHandler:Void QueueLongEvent(System.Collections.IEnumerable, System.String, System.Action`1[System.Exception], Boolean, Boolean)
        "BB36F0D2558C0527BEEDFF64DC1EB26E676AC9CE5C1531CC6F3FE62B49467A5B", // Verse.LongEventHandler:Void RunEventFromAnotherThread(System.Action)
        "074C69DB5382FB3A8AFCB2CC6BF10A655E50D897583F338071D09E3E7D58CCAE", // Verse.LongEventHandler:Void UpdateCurrentAsynchronousEvent()
        "071A80892FEA43A7B7D4423A95343A5DFCE3038A69D6EA4D5BE729140476CD4C", // Verse.LongEventHandler:Void UpdateCurrentEnumeratorEvent()
        "C27DAE9920E91523B2CE557EF1D8870440057C1290F451F472F25D28D8878740", // Verse.LongEventHandler:Void UpdateCurrentSynchronousEvent(Boolean ByRef)
        "ABBCA94F88F01324BA70B4BE966488F5615EBCD5750C1B338AA6E42C267F970E", // Verse.Prefs:Boolean get_RunInBackground()
        "7D0CD2FF2A33CA7B15C536E79A11CE5E411884FD3A4116D88CD2FCEF46235DC0", // Verse.PrefsData:Void Apply()
        "ECCE46E949FE09713E3E45889B5A0CE953B843A8FB5B94E51E1052BA931D1DB9", // Verse.Root+<>c:Void <Start>b__10_1()
        "6430B0AF9175BDBA1A84F12DB454FB8F1912A41ADBF067AADB1789CBD2B0C285", // Verse.Root:Void <Start>b__10_0()
        "78EB170870B02C7886CEBBE52BC52909BE7E386F1D1E69A14592FDCF07469268", // Verse.Root:Void Start()
        "8EB1F1E23AAC877807CCD18D5376CF0418070B48BF094A13231AD83C16C791CF", // Verse.Root:Void Update()
        "C3D3EF37B66C022C6741E7C5823206B6C24F7D5C7D9A6191174B58EFD4C68CDD", // Verse.Root_Play+<>c:Void <Start>b__1_1()
        "AC53D3FA7DD2AF973984D09AFDEBB61956B25DFADFA70D78FFFDE7077B1596BB", // Verse.Root_Play+<>c:Void <Start>b__1_2()
        "DFF66BF49CB1FBA82380C40A6732710221AC3EE340F867CBE36B952E79507505", // Verse.Root_Play+<>c:Void <Start>b__1_3()
        "5A19FC70B7441D61FEF1875A5E3C24A98F81C9E6B74DB206CE09AC2C015D5150", // Verse.Root_Play+<>c__DisplayClass1_0:Void <Start>b__0()
        "E8E46ED3033D8A9019AF2C7A1D7253DB2D313E6E3D7EF115923B791613984444", // Verse.Root_Play:Void Start()
        "151FFBA337BD58FBFDA740D0EE116C3FBFEC9FD16667C0F36BB2D5364DFF8327", // Verse.Root_Play:Void Update()
        "FE6A51C5E3722F7B684F9E3C78570AE819347D4D16727E2564A5A7E0FEA3511B", // Verse.SavedGameLoaderNow:Void LoadGameFromSaveFileNow(System.String)
    };

    private static MethodInfo[] FindTargets()
    {
        var methods = new List<MethodInfo> { BackgroundLoadingRuntime.Load,
            BackgroundLoadingRuntime.PlayStart, BackgroundLoadingRuntime.PlayUpdate, BackgroundLoadingRuntime.Apply,
            AccessTools.Method(typeof(Root), "Start"), AccessTools.Method(typeof(Root), "Update"),
            AccessTools.Method(typeof(GenScene), "GoToMainMenu"),
            AccessTools.PropertyGetter(typeof(Prefs), "RunInBackground"),
            AccessTools.Method(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow"),
            AccessTools.Method(typeof(Game), "LoadGame"),
            AccessTools.Method(typeof(GameAndMapInitExceptionHandlers), "ErrorWhileLoadingGame") };
        foreach (string name in new[] { "QueueLongEvent", "LongEventsUpdate", "ClearQueuedEvents", "ExecuteWhenFinished",
            "ForceExecuteToExecuteWhenFinished", "ExecuteToExecuteWhenFinished", "UpdateCurrentAsynchronousEvent",
            "UpdateCurrentSynchronousEvent", "UpdateCurrentEnumeratorEvent", "RunEventFromAnotherThread",
            "get_AnyEventNowOrWaiting", "get_AnyEventWhichDoesntUseStandardWindowNowOrWaiting", "get_ShouldWaitForEvent" })
            methods.AddRange(typeof(LongEventHandler).GetMethods(AccessTools.allDeclared).Where(m => m.Name == name));
        Type queued = typeof(LongEventHandler).GetNestedType("QueuedLongEvent", BindingFlags.NonPublic)!;
        foreach (string name in new[] { "ShouldWaitUntilDisplayed", "UseStandardWindow" })
            methods.Add(AccessTools.PropertyGetter(queued, name));
        // Include compiler-generated actions: hashing the queueing caller alone
        // would miss a changed loader, pause-on-load callback or scene cleanup.
        AddClosures(methods, typeof(GameDataSaveLoader), "<LoadGame>");
        AddClosures(methods, typeof(Root_Play), "<Start>");
        AddClosures(methods, typeof(Root), "<Start>");
        AddClosures(methods, typeof(Game), "<LoadGame>");
        AddClosures(methods, typeof(GenScene), "<GoToMainMenu>");
        AddClosures(methods, typeof(LongEventHandler), "<UpdateCurrentAsynchronousEvent>");
        return methods.Distinct().OrderBy(Key, StringComparer.Ordinal).ToArray();
    }
    private static void AddClosures(List<MethodInfo> methods, Type type, string name)
    {
        methods.AddRange(type.GetMethods(AccessTools.allDeclared).Where(m => m.Name.StartsWith(name, StringComparison.Ordinal)));
        foreach (Type nested in type.GetNestedTypes(AccessTools.all)) AddClosures(methods, nested, name);
    }
    internal static string Key(MethodInfo method) => method.DeclaringType!.FullName + ":" + method;
    internal static void Validate()
    {
        if (Targets.Length != Expected.Length) throw new InvalidOperationException("Native save-loading functions changed.");
        for (int i = 0; i < Targets.Length; i++)
            if (!SemanticMethodIdentity.TryHash(Targets[i], out string hash, out _) || hash != Expected[i])
                throw new InvalidOperationException("Native save-loading function changed: " + Key(Targets[i]));
    }
}
