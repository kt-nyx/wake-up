// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class PreparedQualityStorageTests
{
    private string root=null!;
    [SetUp] public void Setup()=>root=Path.Combine(TestContext.CurrentContext.WorkDirectory,"c08-quality-"+Guid.NewGuid().ToString("N"));
    [TearDown] public void Cleanup(){if(Directory.Exists(root))Directory.Delete(root,true);}
    private static PreparationRecord Record(string logical,byte value,int bytes=84)=>new() {
        Provider="provider",Logical=logical,Physical="source/"+logical,SourceDigest="source-digest",Active="active-order",
        Runtime=PreparationContract.RuntimeGog,Helper="helper-v3",Role="mask",RoleIdentity="resolved-pair",
        Options=new PreparationOptions{Preset=2},Output=new PreparationPixels{Width=4,Height=4,Mips=3,TextureFormat=4,
            GraphicsFormat=4,Filter=2,Aniso=2,Pixels=Enumerable.Repeat(value,bytes).ToArray()} };
    [Test] public void AtomicGroupReopensWithCompleteRepresentationsAndIndependentProviderSlots()
    {
        var a=Record("Textures/color.png",10);var b=Record("Textures/mask.png",20);
        using(var session=new PreparationStorageSession(root))Assert.That(session.PublishGroup(new[]{a,b}),Is.True);
        using(var session=new PreparationStorageSession(root))
        {
            Assert.That(session.Read(a.Provider,a.Logical)!.Output.Pixels,Is.EqualTo(a.Output.Pixels));
            Assert.That(session.Read(b.Provider,b.Logical)!.Output.Pixels,Is.EqualTo(b.Output.Pixels));
            Assert.That(session.Read("other-provider",a.Logical),Is.Null);
            a.Options.MipBias=1;
            Assert.That(session.Read(a.Provider,a.Logical)!.Options.Identity,Is.Not.EqualTo(a.Options.Identity));
        }
    }
    [Test] public void QuotaRefusalKeepsEveryPreviousGroupMember()
    {
        var a=Record("color",1);var b=Record("mask",2);
        using(var session=new PreparationStorageSession(root,1))
        {
            Assert.That(session.PublishGroup(new[]{a,b}),Is.True);
            Assert.That(session.PublishGroup(new[]{Record("color",3,700000),Record("mask",4,700000)}),Is.False);
        }
        using(var session=new PreparationStorageSession(root,1))
        {Assert.That(session.Read("provider","color")!.Output.Pixels,Is.EqualTo(a.Output.Pixels));
         Assert.That(session.Read("provider","mask")!.Output.Pixels,Is.EqualTo(b.Output.Pixels));}
    }
    [Test] public void CancelledGroupAndDuplicateSlotsDoNotReplacePreviousOutput()
    {
        var a=Record("color",1);var b=Record("mask",2);
        using var session=new PreparationStorageSession(root);
        Assert.That(session.PublishGroup(new[]{a,b}),Is.True);
        using var stop=new CancellationTokenSource();stop.Cancel();
        Assert.Throws<OperationCanceledException>(new Action(()=>session.PublishGroup(new[]{Record("color",7),Record("mask",8)},stop.Token)));
        Assert.That(session.PublishGroup(new[]{Record("color",7),Record("color",8)}),Is.False);
        Assert.That(session.Read("provider","color")!.Output.Pixels,Is.EqualTo(a.Output.Pixels));
        Assert.That(session.Read("provider","mask")!.Output.Pixels,Is.EqualTo(b.Output.Pixels));
    }

    private string Category => Path.Combine(root,"WakeUp","PreparedTextures","v1");
    private OwnedCacheStore Open(out GroupedTextureBlocks blocks,SharedCacheBudget budget,bool batched=false,string action="normal")
    {
        CacheLaunchPolicy? selected=null;
        var group=new GroupedTextureBlocks(Path.Combine(Category,"groups"),batched); blocks=group;
        return new OwnedCacheStore(Category,GroupedTextureBlocks.MaximumEntry,budget.MaximumBytes,
            CacheLaunchPolicy.Latch(ref selected,action,()=>{}),()=>group.StoredBytes,group.Clear,group.Initialize,budget);
    }
    private long ActualBytes => Directory.GetFiles(Path.Combine(root,"WakeUp"),"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length);
    private static string Key(char c)=>new(c,64);
    private static void AssertGroup(OwnedCacheStore owner,GroupedTextureBlocks groups,PreparationRecord[] records)
    {
        foreach(var record in records)
        {
            var actual=PreparationStorage.Read(owner,groups,record.Provider,record.Logical);
            Assert.That(actual,Is.Not.Null,record.Logical);
            Assert.That(actual!.Options.Identity,Is.EqualTo(record.Options.Identity));
            Assert.That(actual.Output.Pixels,Is.EqualTo(record.Output.Pixels));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReopenedOrdinaryPublicationAndSharedQuotaPreserveEveryExplicitSelection(bool batched)
    {
        var records=new[]{Record("color",11,1500),Record("mask",22,1500)};
        using(var budget=new SharedCacheBudget(root,12000))
        using(var owner=Open(out var groups,budget))
        {
            Assert.That(PreparationStorage.PublishGroup(owner,groups,records,false,CancellationToken.None),Is.True);
            Assert.That(groups.Publish(owner,Key('A'),new byte[500],ownerKey:Key('B')),Is.True);
            groups.Complete(owner);
        }
        using(var budget=new SharedCacheBudget(root,12000))
        using(var owner=Open(out var groups,budget,batched))
        {
            // Do not read the quality records first. This is an ordinary loader
            // publishing native data before the selected sources are visited.
            Assert.That(groups.Publish(owner,Key('C'),new byte[1200],ownerKey:Key('D')),Is.True);
            Assert.That(groups.Publish(owner,Key('E'),new byte[9000],ownerKey:Key('F')),Is.False);
            Assert.That(groups.LastReason,Is.EqualTo("group-quota-explicit-selections-retained"));
            AssertGroup(owner,groups,records);
            Assert.That(groups.SelectedQualityCount,Is.EqualTo(2));
            Assert.That(budget.StoredBytes,Is.EqualTo(ActualBytes));
            Assert.That(budget.StoredBytes,Is.LessThanOrEqualTo(12000));
            groups.Complete(owner);
        }
        using(var budget=new SharedCacheBudget(root,12000))
        using(var owner=Open(out var groups,budget))
        {
            AssertGroup(owner,groups,records);
            Assert.That(owner.ReadExternal(()=>groups.Read(Key('C'))),Is.EqualTo(new byte[1200]));
            Assert.That(owner.ReadExternal(()=>groups.Read(Key('E'))),Is.Null);
        }
    }

    // Change only authenticated usage dates in this private test fixture, so
    // normal completion observes actual old persisted entries after reopening.
    private void AgeIndex()
    {
        string path=Path.Combine(Category,"groups","index"); byte[] bytes=File.ReadAllBytes(path);
        using(var reader=new BinaryReader(new MemoryStream(bytes),Encoding.UTF8))
        {
            reader.ReadInt32(); Assert.That(reader.ReadInt32(),Is.EqualTo(3)); reader.ReadBytes(16);
            int count=reader.ReadInt32(); reader.ReadBytes(32);
            for(int i=0;i<count;i++)
            {
                reader.ReadBytes(64);reader.ReadInt32();reader.ReadBytes(32);int blocks=reader.ReadInt32();reader.ReadBytes(64);
                Buffer.BlockCopy(BitConverter.GetBytes(DateTime.UtcNow.AddDays(-45).Ticks),0,bytes,checked((int)reader.BaseStream.Position),8);
                reader.ReadInt64();reader.ReadBytes(blocks*77);
            }
            Assert.That(reader.BaseStream.Position,Is.EqualTo(bytes.Length-32));
        }
        using(var hash=SHA256.Create())Buffer.BlockCopy(hash.ComputeHash(bytes,0,bytes.Length-32),0,bytes,bytes.Length-32,32);
        File.WriteAllBytes(path,bytes);
    }
    [Test] public void AgeMaintenanceKeepsUnreadQualityGroupButStillExpiresOrdinaryNativeEntries()
    {
        var records=new[]{Record("color",31),Record("mask",32)};
        using(var budget=new SharedCacheBudget(root,12000))
        using(var owner=Open(out var groups,budget))
        {
            Assert.That(PreparationStorage.PublishGroup(owner,groups,records,false,CancellationToken.None),Is.True);
            Assert.That(groups.Publish(owner,Key('A'),new byte[100],ownerKey:Key('B')),Is.True);groups.Complete(owner);
        }
        AgeIndex();
        using(var budget=new SharedCacheBudget(root,12000))
        using(var owner=Open(out var groups,budget))
        {
            groups.Complete(owner); // no quality read may make the entries young
            AssertGroup(owner,groups,records);
            Assert.That(owner.ReadExternal(()=>groups.Read(Key('A'))),Is.Null);
            Assert.That(groups.SelectedQualityRecordBytes,Is.EqualTo(records.Sum(r=>(long)PreparationContract.Encode(r).Length)));
        }
        using var reopened=new PreparationStorageSession(root);
        Assert.That(reopened.SelectedQualityCount,Is.EqualTo(2));
        foreach(var record in records)Assert.That(reopened.Read(record.Provider,record.Logical)!.Options.Identity,Is.EqualTo(record.Options.Identity));
    }

    [Test] public void ExplicitClearStillRemovesChoicesAndTheWindowStatesThatEffectBeforeItsAction()
    {
        var record=Record("color",1);
        using(var session=new PreparationStorageSession(root))Assert.That(session.Publish(record),Is.True);
        CacheLaunchPolicy? selected=null;
        PreparedTextureStore.Maintain(root,CacheLaunchPolicy.Latch(ref selected,"clear",()=>{}));
        using(var session=new PreparationStorageSession(root))
        {Assert.That(session.Read(record.Provider,record.Logical),Is.Null);Assert.That(session.SelectedQualityCount,Is.Zero);}
        var instructions=PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(TexturePreparationWindow),"DoWindowContents"));
        int explanation=instructions.FindIndex(i=>Equals(i.operand,PreparationStorageSession.ClearDescription));
        int action=instructions.FindIndex(i=>Equals(i.operand,PreparationStorageSession.ClearActionLabel));
        Assert.That(explanation,Is.GreaterThanOrEqualTo(0));Assert.That(action,Is.GreaterThan(explanation));
        Assert.That(PreparationStorageSession.ClearDescription,Does.Contain("saved quality choices").And.Contain("next launch").And.Contain("authored DDS remain intact"));
        Assert.That(instructions.Any(i=>Equals(i.operand,PreparationStorageSession.ClearCompleted)),Is.True);
    }

    private Dictionary<string,(byte[] Bytes,long WriteTicks)> SnapshotFiles()=>Directory.GetFiles(root,"*",SearchOption.AllDirectories)
        // Existing launch ownership files are intentionally held with no read
        // sharing. Compare persisted cache data, not the lock handles we own.
        .Where(p=>Path.GetFileName(p)!=".owner"&&Path.GetFileName(p)!=".budget-owner")
        .ToDictionary(p=>p,p=>(File.ReadAllBytes(p),File.GetLastWriteTimeUtc(p).Ticks));
    [Test] public void PreviewReadsExactSavedChoiceWithoutCreatingOrCleaningFiles()
    {
        var missing=PreparationStorageSession.Inspect(root,new[]{("provider","color")});
        Assert.That(missing.Single().Reason,Is.EqualTo("selection-missing"));Assert.That(Directory.Exists(root),Is.False);
        var record=Record("color",9);record.Options.MipBias=record.Output.Bias=0.5f;record.Options.Anisotropy=record.Output.Aniso=4;
        using(var session=new PreparationStorageSession(root))Assert.That(session.Publish(record),Is.True);
        string pending=Path.Combine(Category,"groups",Guid.NewGuid().ToString("N")+".pending");File.WriteAllBytes(pending,new byte[]{1,2,3});
        var before=SnapshotFiles();
        var preview=PreparationStorageSession.Inspect(root,new[]{("provider","color"),("provider","missing")});
        Assert.That(preview[0].Reason,Is.EqualTo("completed-selection"));
        Assert.That(preview[0].Choice!.Options.Identity,Is.EqualTo(record.Options.Identity));
        Assert.That(preview[0].PixelBytes,Is.EqualTo(record.Output.Pixels.Length));Assert.That(preview[0].Choice!.Output.Pixels,Is.Empty);
        Assert.That(preview[1].Reason,Is.EqualTo("selection-missing"));
        var after=SnapshotFiles();Assert.That(after.Keys,Is.EquivalentTo(before.Keys));
        foreach(string path in before.Keys)
        {Assert.That(after[path].Bytes,Is.EqualTo(before[path].Bytes));Assert.That(after[path].WriteTicks,Is.EqualTo(before[path].WriteTicks));}
        using(var session=new PreparationStorageSession(root))
        {
            var locked=PreparationStorageSession.Inspect(root,new[]{("provider","color")}).Single();
            Assert.That(locked.Choice,Is.Null);Assert.That(locked.Reason,Does.StartWith("selection-locked-or-unavailable:"));
        }
    }
    [Test] public void PreviewRejectsCorruptCompletedPayloadWithoutDeletingTheSelection()
    {
        using(var session=new PreparationStorageSession(root))Assert.That(session.Publish(Record("color",9)),Is.True);
        string group=Directory.GetFiles(Path.Combine(Category,"groups"),"*.group").Single();
        byte[] damaged=File.ReadAllBytes(group);damaged[113]^=1;File.WriteAllBytes(group,damaged);
        var before=SnapshotFiles();var preview=PreparationStorageSession.Inspect(root,new[]{("provider","color")}).Single();
        Assert.That(preview.Choice,Is.Null);Assert.That(preview.Reason,Is.EqualTo("group-block-digest"));
        foreach(var file in before)Assert.That(File.ReadAllBytes(file.Key),Is.EqualTo(file.Value.Bytes));
    }
    [Test] public void PreviewBorrowsIdleLaunchOwnershipAndRefusesAnAttachedWriter()
    {
        using(var session=new PreparationStorageSession(root))Assert.That(session.Publish(Record("color",5)),Is.True);
        using var budget=new SharedCacheBudget(root,12000);
        var current=AccessTools.Field(typeof(SharedCacheBudget),"current"); object? previous=current.GetValue(null);
        try
        {
            current.SetValue(null,budget);
            using(var owner=Open(out _,budget))
            {
                var refused=PreparationStorageSession.Inspect(root,new[]{("provider","color")}).Single();
                Assert.That(refused.Choice,Is.Null);Assert.That(refused.Reason,Does.Contain("shared-cache-category-owned"));
            }
            var before=SnapshotFiles();
            var saved=PreparationStorageSession.Inspect(root,new[]{("provider","color")}).Single();
            Assert.That(saved.Choice,Is.Not.Null);Assert.That(saved.Description,Does.Contain("half dimensions").And.Contain("trilinear"));
            foreach(var file in before)
            {
                Assert.That(File.ReadAllBytes(file.Key),Is.EqualTo(file.Value.Bytes));
                Assert.That(File.GetLastWriteTimeUtc(file.Key).Ticks,Is.EqualTo(file.Value.WriteTicks));
            }
        }
        finally {current.SetValue(null,previous);}
    }
    [Test] public void InGameScanReadsSavedChoicesAndDrawsThemSeparatelyFromPendingControls()
    {
        var scan=PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(TextureQualityWindow),"Scan"));
        Assert.That(scan.Any(i=>i.operand is System.Reflection.MethodInfo m && m.DeclaringType==typeof(PreparationStorageSession) && m.Name=="Inspect"),Is.True);
        var draw=PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(TextureQualityWindow),"DoWindowContents"));
        Assert.That(draw.Any(i=>i.operand is System.Reflection.FieldInfo f && f.Name=="SavedChoice"),Is.True);
        var choice=Record("color",1);choice.Options.Preset=3;choice.Options.Filter=0;choice.Options.MipBias=-0.5f;choice.Options.Anisotropy=8;
        Assert.That(PreparationChoiceSnapshot.Describe(choice),Does.Contain("quarter dimensions").And.Contain("point")
            .And.Contain("mip bias -0.5").And.Contain("anisotropy 8"));
    }
}
