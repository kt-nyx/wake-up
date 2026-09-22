// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System.Collections.Generic;
using System.IO;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
namespace WakeUp.Tests;
[TestFixture]
public class BackgroundMapContractTests
{
    [Test]
    public void NativeMapContractsMatchFrozenBodies()
    {
        var lines = new List<string>();
        int index = 0;
        foreach (var target in BackgroundLoadingMapContract.Targets)
        {
            Assert.That(SemanticMethodIdentity.TryHash(target, out string hash, out string reason, out string canonical), Is.True, BackgroundLoadingContract.Key(target) + ": " + reason);
            lines.Add(hash + " " + BackgroundLoadingContract.Key(target));
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "background-map-original-" + index + ".txt"), canonical);
            string gameCanonical = Regex.Replace(canonical, @"(?:System\.Private\.CoreLib|System\.Linq):System\.[A-Za-z0-9_`+.]+",
                match => FrameworkOwners.TryGetValue(match.Value, out string? owner) ? owner : match.Value);
            string gameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(gameCanonical)));
            Assert.That(gameHash, Is.EqualTo(BackgroundLoadingMapContract.Expected[index++]), BackgroundLoadingContract.Key(target));
        }
        File.WriteAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "background-map-contracts.txt"), lines);
        Assert.That(index, Is.EqualTo(BackgroundLoadingMapContract.Expected.Length));
    }

    // Exact resolved framework type bindings verified against all 42 native
    // Unity Mono captures from the pinned original GOG assembly. Only this
    // modern offline host needs the mapping; production hashing is unchanged.
    private static readonly Dictionary<string, string> FrameworkOwners = BuildFrameworkOwners();
    private static Dictionary<string, string> BuildFrameworkOwners()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string type in new[] {
            "Int32", "Collections.Generic.IEnumerable`1", "Void", "Boolean", "Action", "Object", "IntPtr", "String",
            "Action`1", "Exception", "Collections.Generic.List`1", "Predicate`1", "Text.StringBuilder",
            "Collections.Generic.List`1+Enumerator", "Char", "Collections.Generic.IList`1", "IDisposable", "Nullable`1",
            "Collections.Generic.IEnumerator`1", "Collections.IEnumerable", "Collections.IEnumerator", "Environment",
            "NotSupportedException", "Collections.Generic.KeyValuePair`2", "Collections.Generic.IReadOnlyDictionary`2", "Func`2" })
            result.Add("System.Private.CoreLib:System." + type, "mscorlib:System." + type);
        result.Add("System.Linq:System.Linq.Enumerable", "System.Core:System.Linq.Enumerable");
        return result;
    }

}
