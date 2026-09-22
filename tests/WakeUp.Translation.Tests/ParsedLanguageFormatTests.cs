// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Verse;
using WakeUp;
using Injection = Verse.DefInjectionPackage.DefInjection;

namespace WakeUp.Tests;

[TestFixture]
public sealed class ParsedLanguageFormatTests
{
    private static void Throws(Action action) => Assert.Throws<InvalidDataException>(action);
    [Test]
    public void NativeSourcePayloadsValidateAllRowsBeforeReturningThem()
    {
        var rows = new List<DirectXmlLoaderSimple.XmlKeyValuePair> {
            new() { key = "same", value = "first", lineNumber = 2 },
            new() { key = "same", value = "TODO", lineNumber = 3 }
        };
        byte[] payload = ParsedLanguageFormat.EncodeKeyedRows(rows);
        var ready = ParsedLanguageFormat.DecodeKeyedRows(payload);
        Assert.That(ready.Select(r => r.value), Is.EqualTo(new[] { "first", "TODO" }));
        Assert.That(ready.Select(r => r.lineNumber), Is.EqualTo(new[] { 2, 3 }));
        Throws(() => ParsedLanguageFormat.DecodeKeyedRows(payload.Take(payload.Length - 1).ToArray()));
        rows[1] = new() { key = "same", value = "bad line", lineNumber = -1 };
        Throws(() => ParsedLanguageFormat.DecodeKeyedRows(ParsedLanguageFormat.EncodeKeyedRows(rows)));
        Throws(() => ParsedLanguageFormat.DecodeLines(ParsedLanguageFormat.EncodeLines(new List<string> { "valid", null! })));
        var lines = new List<string> { "one", "", "two" };
        Assert.That(ParsedLanguageFormat.DecodeLines(ParsedLanguageFormat.EncodeLines(lines)), Is.EqualTo(lines).And.Not.SameAs(lines));
    }

    [Test]
    public void KeyedRoundTripPreservesNativeFieldsOrderAndFreshOwnership()
    {
        var source = new Dictionary<string, LoadedLanguage.KeyedReplacement>
        {
            ["z"] = new LoadedLanguage.KeyedReplacement
            {
                key = "z", value = "Été 日本語\n", fileSource = "Keys.xml", fileSourceLine = 17,
                fileSourceFullPath = "Languages/French/Keyed/Keys.xml", isPlaceholder = true
            },
            ["a"] = new LoadedLanguage.KeyedReplacement { key = "a", value = "" }
        };
        byte[] bytes = ParsedLanguageFormat.EncodeKeyed(source);
        var first = ParsedLanguageFormat.DecodeKeyed(bytes);
        var second = ParsedLanguageFormat.DecodeKeyed(bytes);
        Assert.That(first.Keys, Is.EqualTo(new[] { "z", "a" }));
        Assert.That(first["z"].key, Is.EqualTo("z"));
        Assert.That(first["z"].value, Is.EqualTo(source["z"].value));
        Assert.That(first["z"].fileSource, Is.EqualTo("Keys.xml"));
        Assert.That(first["z"].fileSourceLine, Is.EqualTo(17));
        Assert.That(first["z"].fileSourceFullPath, Is.EqualTo(source["z"].fileSourceFullPath));
        Assert.That(first["z"].isPlaceholder, Is.True);
        Assert.That(first["a"].value, Is.Empty);
        Assert.That(first["a"].fileSource, Is.Null);
        first["z"].value = "changed";
        Assert.That(second["z"].value, Is.EqualTo(source["z"].value));
        Assert.That(second["z"], Is.Not.SameAs(source["z"]));
    }

    [Test]
    public void InjectionRoundTripPreservesParseStateAndDetachedMutableLists()
    {
        var source = new DefInjectionPackage(typeof(ThingDef)) { usedOldRepSyntax = true };
        source.injections.Add("Z.list", new Injection
        {
            path = "Z.list", nonBackCompatiblePath = "Old.list", fileSource = "Defs.xml", isPlaceholder = true,
            fullListInjection = new List<string> { "un", "", null! },
            fullListInjectionComments = new List<Pair<int, string>> { new Pair<int, string>(1, "commentaire") }
        });
        source.injections.Add("A.label", new Injection { path = "A.label", injection = "étiquette" });
        source.injections.Add("B.list", new Injection
        {
            path = "B.list", fullListInjection = new List<string>(), fullListInjectionComments = new List<Pair<int, string>>()
        });
        byte[] bytes = ParsedLanguageFormat.EncodeInjection(source);
        var first = ParsedLanguageFormat.DecodeInjection(bytes, typeof(ThingDef));
        var second = ParsedLanguageFormat.DecodeInjection(bytes, typeof(ThingDef));
        Assert.That(first.defType, Is.EqualTo(typeof(ThingDef)));
        Assert.That(first.usedOldRepSyntax, Is.True);
        Assert.That(first.injections.Keys, Is.EqualTo(source.injections.Keys));
        var list = first.injections["Z.list"];
        Assert.That(list.path, Is.EqualTo("Z.list"));
        Assert.That(list.nonBackCompatiblePath, Is.EqualTo("Old.list"));
        Assert.That(list.fileSource, Is.EqualTo("Defs.xml"));
        Assert.That(list.isPlaceholder, Is.True);
        Assert.That(list.injection, Is.Null);
        Assert.That(list.fullListInjection, Is.EqualTo(source.injections["Z.list"].fullListInjection));
        Assert.That(list.fullListInjectionComments[0].First, Is.EqualTo(1));
        Assert.That(list.fullListInjectionComments[0].Second, Is.EqualTo("commentaire"));
        Assert.That(first.injections["A.label"].fullListInjection, Is.Null);
        Assert.That(first.injections["A.label"].fullListInjectionComments, Is.Null);
        Assert.That(first.injections["B.list"].fullListInjection, Is.Empty);
        Assert.That(first.injections["B.list"].fullListInjectionComments, Is.Empty);
        list.injected = true; list.normalizedPath = "runtime"; list.fullListInjection[0] = "changed";
        list.fullListInjectionComments.Clear();
        var untouched = second.injections["Z.list"];
        Assert.That(untouched.injected, Is.False);
        Assert.That(untouched.normalizedPath, Is.Null);
        Assert.That(untouched.suggestedPath, Is.Null);
        Assert.That(untouched.replacedString, Is.Null);
        Assert.That(untouched.replacedList, Is.Null);
        Assert.That(untouched.fullListInjection[0], Is.EqualTo("un"));
        Assert.That(untouched.fullListInjectionComments, Has.Count.EqualTo(1));
        Assert.That(first.loadErrors, Is.Empty);
        Assert.That(first.loadSyntaxSuggestions, Is.Empty);
        Assert.That(source.injections["Z.list"].fullListInjection[0], Is.EqualTo("un"));
    }

    [Test]
    public void StringsRoundTripPreservesNullEmptyAndFreshLists()
    {
        var source = new Dictionary<string, List<string>>
        {
            ["z"] = new List<string> { "猫", "", null! }, ["a"] = new List<string>(), ["null"] = null!
        };
        byte[] bytes = ParsedLanguageFormat.EncodeStrings(source);
        var first = ParsedLanguageFormat.DecodeStrings(bytes);
        var second = ParsedLanguageFormat.DecodeStrings(bytes);
        Assert.That(first.Keys, Is.EqualTo(source.Keys));
        Assert.That(first["z"], Is.EqualTo(source["z"]));
        Assert.That(first["a"], Is.Empty);
        Assert.That(first["null"], Is.Null);
        first["z"].Clear();
        Assert.That(second["z"], Is.EqualTo(source["z"]));
        Assert.That(second["z"], Is.Not.SameAs(source["z"]));
    }

    [TestCase("injected")]
    [TestCase("normalized")]
    [TestCase("suggested")]
    [TestCase("replacedString")]
    [TestCase("replacedList")]
    [TestCase("error")]
    [TestCase("suggestion")]
    public void CaptureRejectsApplicationStateAndDiagnostics(string state)
    {
        var package = new DefInjectionPackage(typeof(ThingDef));
        var value = new Injection { path = "A.label", injection = "test" };
        package.injections.Add(value.path, value);
        switch (state)
        {
            case "injected": value.injected = true; break;
            case "normalized": value.normalizedPath = ""; break;
            case "suggested": value.suggestedPath = ""; break;
            case "replacedString": value.replacedString = ""; break;
            case "replacedList": value.replacedList = Array.Empty<string>(); break;
            case "error": package.loadErrors.Add("duplicate"); break;
            case "suggestion": package.loadSyntaxSuggestions.Add("rename"); break;
        }
        Throws(() => ParsedLanguageFormat.EncodeInjection(package));
    }

    [Test]
    public void DecodeRejectsTruncationTrailingDataInvalidCountsAndCrossKindPayloads()
    {
        byte[] bytes = ParsedLanguageFormat.EncodeStrings(new Dictionary<string, List<string>> { ["a"] = new List<string> { "b" } });
        for (int i = 0; i < bytes.Length; i++)
        {
            byte[] truncated = bytes.Take(i).ToArray();
            Throws(() => ParsedLanguageFormat.DecodeStrings(truncated));
        }
        Throws(() => ParsedLanguageFormat.DecodeStrings(bytes.Concat(new byte[] { 0 }).ToArray()));
        Throws(() => ParsedLanguageFormat.DecodeKeyed(bytes));
        byte[] invalidCount = (byte[])bytes.Clone();
        Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, invalidCount, 5, 4);
        Throws(() => ParsedLanguageFormat.DecodeStrings(invalidCount));
        byte[] invalidStringLength = (byte[])bytes.Clone();
        Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, invalidStringLength, 13, 4);
        Throws(() => ParsedLanguageFormat.DecodeStrings(invalidStringLength));
        foreach (int token in new[] { -3, 0, int.MaxValue })
        {
            byte[] invalidReference = (byte[])bytes.Clone();
            Array.Copy(BitConverter.GetBytes(token), 0, invalidReference, 9, 4);
            Throws(() => ParsedLanguageFormat.DecodeStrings(invalidReference));
        }
        Throws(() => ParsedLanguageFormat.DecodeStrings(new byte[ParsedLanguageFormat.MaximumPayload + 1]));
    }

    [Test]
    public void CaptureRejectsUnsupportedComparerAndOversizedStrings()
    {
        Throws(() => ParsedLanguageFormat.EncodeStrings(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)));
        Throws(() => ParsedLanguageFormat.EncodeStrings(new Dictionary<string, List<string>>
        { ["large"] = new List<string> { new string('x', 4 * 1024 * 1024 + 1) } }));
    }

    [Test]
    public void StringSharingDoesNotImposeTheRecordCountLimitOnDistinctText()
    {
        var source = new Dictionary<string, List<string>>
        {
            ["a"] = Enumerable.Range(0, 140000).Select(i => "a" + i).ToList(),
            ["b"] = Enumerable.Range(0, 140000).Select(i => "b" + i).ToList()
        };
        var restored = ParsedLanguageFormat.DecodeStrings(ParsedLanguageFormat.EncodeStrings(source));
        Assert.That(restored["a"], Is.EqualTo(source["a"]));
        Assert.That(restored["b"], Is.EqualTo(source["b"]));
    }
}

