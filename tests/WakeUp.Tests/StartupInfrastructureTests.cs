// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class StartupInfrastructureTests
{
    [DataContract]
    private sealed class Receipt
    {
        [DataMember(Name = "event")] public string Event = "";
        [DataMember(Name = "reason")] public string Reason = "";
        [DataMember(Name = "count")] public int Count = 0;
    }

    [Test]
    public void ReceiptRoundTripsControlCharactersWithoutAddingLines()
    {
        string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "receipt-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "events.jsonl");
        string reason = "C:\\mods\\\"name\"\tline\r\n" + new string(new[] { '\0', '\b', '\f', '\u001f' }) + " café";
        try
        {
            JsonLineLog.WriteReceipt(path, "refused", reason, "\"count\":2");
            string[] lines = File.ReadAllLines(path);
            Assert.That(lines, Has.Length.EqualTo(1));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(lines[0]));
            var receipt = (Receipt)new DataContractJsonSerializer(typeof(Receipt)).ReadObject(stream);
            Assert.That(receipt.Event, Is.EqualTo("refused"));
            Assert.That(receipt.Reason, Is.EqualTo(reason));
            Assert.That(receipt.Count, Is.EqualTo(2));
            JsonLineLog.WriteEvent(directory, "ignored");
            JsonLineLog.WriteEvent(null, "ignored");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static class BrokenFeature
    {
        static BrokenFeature() => throw new InvalidOperationException("unsupported feature metadata");
        internal static void Initialize()
        {
        }
    }

    [Test]
    public void StaticInitializationFailureDoesNotStopTheNextFeature()
    {
        var failures = new List<Exception>();
        bool nextFeatureRan = false;
        StartupFeatureRunner.Run(() => BrokenFeature.Initialize(), failures.Add);
        StartupFeatureRunner.Run(() => nextFeatureRan = true, failures.Add);
        Assert.That(failures, Has.Count.EqualTo(1));
        Assert.That(failures[0], Is.TypeOf<TypeInitializationException>());
        Assert.That(nextFeatureRan, Is.True);
    }
}
