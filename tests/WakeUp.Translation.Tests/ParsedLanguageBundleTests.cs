// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class ParsedLanguageBundleTests
{
    private static void Throws(Action action) => Assert.Throws<InvalidDataException>(action);
    [Test]
    public void BundlePreservesSeparateFullKeysAndRejectsMalformedBoundaries()
    {
        var source = new Dictionary<string, byte[]> { [new string('A', 64)] = new byte[] { 1, 2 }, [new string('B', 64)] = new byte[] { 3 } };
        byte[] bytes = ParsedLanguageBundle.Encode(source);
        var result = ParsedLanguageBundle.Decode(bytes);
        Assert.That(result.Keys, Is.EqualTo(source.Keys));
        Assert.That(result.Values.ToArray(), Is.EqualTo(source.Values.ToArray()));
        result[new string('A',64)][0] = 9;
        Assert.That(source[new string('A',64)][0], Is.EqualTo(1));
        foreach (int length in new[] { 0, 7, 8, 70, bytes.Length - 1 })
            Throws(() => ParsedLanguageBundle.Decode(bytes.Take(length).ToArray()));
        Throws(() => ParsedLanguageBundle.Decode(bytes.Concat(new byte[] { 0 }).ToArray()));
        Array.Copy(bytes, 8, bytes, 78, 64); // Duplicate the first full key in entry two.
        Throws(() => ParsedLanguageBundle.Decode(bytes));
    }
}
