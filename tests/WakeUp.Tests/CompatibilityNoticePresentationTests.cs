// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class CompatibilityNoticePresentationTests
{
    private string directory = null!, file = null!;
    [SetUp] public void Setup()
    { directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "notice-copy-" + Guid.NewGuid().ToString("N")); file = Path.Combine(directory, "notices.xml"); }
    [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    private OperationRegistry Open() => new(file, _ => { });
    private static CompatibilityCard Card(string id, string code = "required-contract", string provider = "Wake-Up", string details = "foreign-contract-IL_Harmony_PatchOperationAdd", string displayProvider = "")
        => CompatibilityNoticePresentation.Describe(new CompatibilityNotice(id + "|" + code + "|" + provider, details, true, displayProvider));

    [TestCase("YaOpt", "Yet Another Optimizer (YaOpt)")]
    [TestCase("Faster Game Loading", "Faster Game Loading")]
    public void KnownSearchCooperationNamesModAndUsesInformation(string provider, string name)
    {
        var c = Card("definitions/PatchOperationAdd", provider: provider);
        Assert.That(c.Kind, Is.EqualTo(NoticeKind.Information)); Assert.That(c.Mods, Is.EqualTo(name));
        Assert.That(c.Area, Is.EqualTo("Definition and template searches"));
        Assert.That(c.Change, Does.Contain("only the overlapping")); Assert.That(c.Action, Does.Contain("No action needed"));
        Assert.That(c.Text, Does.Not.Contain("PatchOperation").And.Not.Contain("Harmony").And.Not.Contain("contract"));
    }
    [Test] public void PrettyLabelDoesNotChangeSemanticCategory()
    {
        var c = Card("definitions/PatchOperationAdd", provider: "YaOpt", displayProvider: "Yet Another Optimizer (YaOpt) + Another named mod");
        Assert.That(c.Kind, Is.EqualTo(NoticeKind.Information)); Assert.That(c.Mods, Does.Contain("Another named mod"));
    }
    [Test] public void UnknownModIsNotGuessedFromUnsupportedCode()
    {
        var c = Card("type-name", details: "unqualified-game-version");
        Assert.That(c.Mods, Is.Empty); Assert.That(c.Text, Does.Not.Contain("another mod").And.Not.Contain("which mod"));
        var foreign = Card("type-name", "foreign-method");
        Assert.That(foreign.Text, Does.Contain("could not identify which mod"));
        Assert.That(foreign.Text, Does.Not.Contain("IL_Harmony"));
    }
    [TestCase("giddy", "Giddy-Up")]
    [TestCase("character", "Character Editor")]
    [TestCase("repaint", "Loading Progress")]
    [TestCase("gagarin", "Missile Girl / Gagarin")]
    [TestCase("extended-query", "XML Extensions")]
    public void IntegrationNamesAffectedModWithoutBlamingIt(string id, string name)
    { var c = Card(id, details: "unqualified-version"); Assert.That(c.Mods, Is.EqualTo(name)); Assert.That(c.Change, Does.Not.Contain(name + " changes")); }
    [Test] public void ElevenXmlWorkersShareOneCardAndAllAcknowledgementKeys()
    {
        var r = Open();
        foreach (string name in new[] { "Add", "AddModExtension", "Insert", "Remove", "Replace", "SetName", "AttributeAdd", "AttributeRemove", "AttributeSet", "Test", "Conditional" })
        { string id = "definitions/PatchOperation" + name; r.Request(id, name, true); r.Set(id, OperationState.Unavailable, "foreign-worker", provider: "YaOpt"); }
        var cards = CompatibilityNoticePresentation.Group(r.Pending());
        Assert.That(cards, Has.Length.EqualTo(1)); Assert.That(cards[0].Keys, Has.Length.EqualTo(11));
        Assert.That(r.Acknowledge(cards[0].Keys), Is.True); Assert.That(Open().Pending(), Is.Empty);
    }
    [Test] public void ImageProducerFiveOperationsShareOneNoActionCard()
    {
        var notices = new[] { "texture-loader", "texture-cache", "prepared", "psd", "quality" }
            .Select(id => new CompatibilityNotice(id + "|supplier-image-producer|Image Opt", "native decoder hook-xx", true));
        var c = CompatibilityNoticePresentation.Group(notices).Single();
        Assert.That(c.Keys, Has.Length.EqualTo(5)); Assert.That(c.Kind, Is.EqualTo(NoticeKind.Information));
        Assert.That(c.Continues, Does.Contain("Image Opt continues to load your images"));
        Assert.That(c.Action, Does.Contain("keep both mods enabled").And.Not.Contain("native loader"));
    }
    [TestCase("Loading Progress", "Loading Progress provides", "Information")]
    [TestCase("RimThemes", "RimThemes provides", "Information")]
    [TestCase("RimThemes + Loading Progress", "both provide", "ActionNeeded")]
    public void DisplayNoticeUsesActualScreenOwner(string provider, string change, string kind)
    {
        var c = Card("display", "supplier-display", provider);
        Assert.That(c.Change, Does.Contain(change)); Assert.That(c.Kind.ToString(), Is.EqualTo(kind));
        if (provider == "Loading Progress") Assert.That(c.Text, Does.Not.Contain("RimThemes"));
    }
    [Test] public void UnusedLoadingProgressTrackingIsOneInformationalCard()
    {
        var cards = CompatibilityNoticePresentation.Group(new[] { "observation", "invocations" }
            .Select(id => new CompatibilityNotice(id + "|display-dependency|Loading Progress", "internal tracking", true)));
        Assert.That(cards, Has.Length.EqualTo(1)); Assert.That(cards[0].Area, Is.EqualTo("Loading screen"));
        Assert.That(cards[0].Kind, Is.EqualTo(NoticeKind.Information));
    }
    [Test] public void ExactUserSettingsAndOptionalActionsArePreserved()
    {
        var lp = Card("background", "supplier-deferred-completion", "Loading Progress");
        Assert.That(lp.Area, Does.Contain("Loading saves")); Assert.That(lp.Kind, Is.EqualTo(NoticeKind.OptionalChoice));
        Assert.That(lp.Action, Does.Contain("Patch in-game renderer regeneration to keep the loading window responsive").And.Contain("keep your current settings"));
        var image = Card("giddy", "image-original-destruction", "Image Opt");
        Assert.That(image.Action, Does.Contain("destroyOriginalTexture")); Assert.That(image.Mods, Does.Contain("Giddy-Up").And.Contain("ImageOptCompat"));
        Assert.That(image.Text, Does.Not.Contain("readback"));
        Assert.That(Card("summary", "supplier-dlc-panel", "No Modlist on Loading").Action,
            Does.Contain("Also hide the DLC panel kept by No Modlist on Loading (restart required)"));
    }
    [Test] public void AdvisoryDoesNotPretendWakeUpDisabledTheSupplier()
    {
        var c = Card("advisory", "source-skip-without-patch-replay", "DefLoadCache");
        Assert.That(c.Kind, Is.EqualTo(NoticeKind.Advisory)); Assert.That(c.Action, Does.Contain("Skip reading mod files on repeat launches"));
        Assert.That(c.Continues, Does.Contain("has not changed")); Assert.That(c.Text, Does.Not.Contain("No action needed"));
    }
    [Test] public void OwnOptionConflictAndPendingImageAreNotGenericFailures()
    {
        var own = Card("processed-xml", details: "foreign-xml-hook WakeUp.StreamingXmlRuntime");
        Assert.That(own.Mods, Is.EqualTo("Wake-Up")); Assert.That(own.Continues, Does.Contain("two Wake-Up options"));
        Assert.That(own.Text, Does.Not.Contain("could not identify"));
        Assert.That(Card("giddy", "image-source-pending", "Image Opt").Continues, Does.Contain("does not switch off all"));
    }
    [Test] public void LegacyPendingTextIsReformattedFromStableKeyWithoutReplayingDiagnostics()
    {
        Directory.CreateDirectory(directory);
        new XElement("compatibility", new XAttribute("version", "1"),
            new XElement("pending", new XAttribute("key", "definitions/PatchOperationAdd|required-contract|YaOpt"), "RAW_OLD_Harmony_MethodHash"),
            new XElement("pending", new XAttribute("key", "unknown|old-code|Mod|Variant"), "RAW_OLD_Exception")).Save(file);
        var r = Open(); var pending = r.Pending();
        Assert.That(pending[0].Text, Does.Contain("Yet Another Optimizer").And.Contain("earlier launch").And.Not.Contain("RAW_OLD"));
        Assert.That(pending[1].Provider, Is.EqualTo("Mod|Variant")); Assert.That(pending[1].Text, Does.Not.Contain("RAW_OLD"));
        Assert.That(r.Acknowledge(pending.Select(n => n.Key)), Is.True); Assert.That(Open().Pending(), Is.Empty);
    }
    [Test] public void OldAcknowledgementSurvivesNewCopyAndDisplayAttribution()
    {
        var r = Open(); r.Request("reflection/x", "Old raw label", true); r.Set("reflection/x", OperationState.Unavailable, "old");
        Assert.That(r.Acknowledge(r.Pending().Select(n => n.Key)), Is.True);
        r = Open(); r.Request("reflection/x", "New friendly label", true); r.Set("reflection/x", OperationState.Unavailable, "new", displayProvider: "Known mod");
        Assert.That(r.Pending(), Is.Empty);
    }
    [Test] public void SameDecisionRefreshesPendingFromEarlierLaunchAndLaterAttribution()
    {
        var r = Open(); r.Request("reflection/x", "Lookup", true); r.Set("reflection/x", OperationState.Unavailable, "same");
        r = Open(); Assert.That(r.Pending()[0].Current, Is.False);
        r.Request("reflection/x", "Lookup", true); r.Set("reflection/x", OperationState.Unavailable, "same");
        Assert.That(r.Pending()[0].Current, Is.True);
        r.Set("reflection/x", OperationState.Unavailable, "same", displayProvider: "Known mod");
        Assert.That(r.Pending()[0].Text, Does.Contain("Known mod")); Assert.That(r.Pending()[0].Key, Is.EqualTo("reflection/x|required-contract|Wake-Up"));
        Assert.That(Open().Pending()[0].Text, Does.Contain("Known mod"));
    }
    [Test] public void RecoveryDistinguishesEarlierThisLaunchFromEarlierLaunch()
    {
        var r = Open(); r.Request("giddy", "Giddy", true); r.Set("giddy", OperationState.Unavailable, "same");
        r.Set("giddy", OperationState.Available, "ready");
        Assert.That(r.Pending()[0].Text, Does.Contain("Earlier in this launch").And.Contain("available on this launch").And.Not.Contain("On an earlier launch"));
        r = Open(); r.Request("giddy", "Giddy", true); r.Set("giddy", OperationState.NotApplicable, "absent");
        Assert.That(r.Pending()[0].Text, Does.Contain("On an earlier launch").And.Contain("does not apply on this launch").And.Contain("No action is required for this old decision"));
    }
    [Test] public void TallCardsRequireEveryFragmentButCanBeReadByScrolling()
    {
        var progress = new NoticeReadProgress();
        Assert.That(progress.Observe(0, 300, 900), Is.False);
        Assert.That(progress.Observe(600, 900, 900), Is.False, "Skipping the middle must not count.");
        Assert.That(progress.Observe(300, 600, 900), Is.True);
    }
    [Test] public void DifferentCauseAndHistoricalStatusStaySeparateWhenGrouping()
    {
        var cards = CompatibilityNoticePresentation.Group(new[] {
            new CompatibilityNotice("definitions/a|required-contract|YaOpt", "same", true),
            new CompatibilityNotice("definitions/b|required-contract|YaOpt", "same"),
            new CompatibilityNotice("definitions/c|setup-interrupted|YaOpt", "same", true) });
        Assert.That(cards, Has.Length.EqualTo(3));
    }
    [Test] public void WriteReviewExamples()
    {
        var examples = new[] { Card("definitions/PatchOperationAdd", provider: "YaOpt"), Card("texture-cache", "supplier-image-producer", "Image Opt"),
            Card("background", "supplier-deferred-completion", "Loading Progress"), Card("giddy", "image-original-destruction", "Image Opt"),
            Card("advisory", "source-skip-without-patch-replay", "DefLoadCache"), Card("type-name", "foreign-method"),
            CompatibilityNoticePresentation.Describe(new CompatibilityNotice("display|supplier-display|RimThemes", "RAW_OLD_NOTICE")) };
        string? path = Environment.GetEnvironmentVariable("WAKE_UP_NOTICE_EXAMPLES");
        if (path != null) File.WriteAllText(path, string.Join("\n\n---\n\n", examples.Select(c => c.Text)));
        Assert.That(examples.All(c => c.Text.Length > 0), Is.True);
    }
}

