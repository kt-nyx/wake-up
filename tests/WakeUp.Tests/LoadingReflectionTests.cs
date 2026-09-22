// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NUnit.Framework;
using Verse;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class LoadingReflectionIndexTests
{
    private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    private const BindingFlags All = Public | BindingFlags.NonPublic;

    [Test]
    public void FullTypeNameFlagsAndMemberKindPreserveResolution()
    {
        var index = new LoadingReflectionIndex();
        Assert.That(index.Field(typeof(Generic<int>), "Value", All, 1), Is.EqualTo(typeof(Generic<int>).GetField("Value", All)));
        Assert.That(index.Field(typeof(Generic<string>), "Value", All, 1)!.FieldType, Is.EqualTo(typeof(string)));
        Assert.That(index.Field(typeof(Generic<int>), "hidden", Public, 1), Is.Null);
        Assert.That(index.Field(typeof(Generic<int>), "hidden", All, 1), Is.Not.Null);
        Assert.That(index.Field(typeof(Generic<int>), "value", All, 1), Is.Null);
        Assert.That(index.Field(typeof(Generic<int>), "value", All | BindingFlags.IgnoreCase, 1), Is.Not.Null);
        Assert.That(index.Property(typeof(List<int>), "Count", Public, 1), Is.EqualTo(typeof(List<int>).GetProperty("Count")));
        Assert.That(index.Method(typeof(Generic<int>), "Read", All, 1), Is.EqualTo(typeof(Generic<int>).GetMethod("Read", All)));
        long before = index.Hits;
        index.Field(typeof(Generic<int>), "Value", All, 1);
        index.Property(typeof(List<int>), "Count", Public, 1);
        index.Method(typeof(Generic<int>), "Read", All, 1);
        Assert.That(index.Hits - before, Is.EqualTo(3));
    }

    [Test]
    public void NativeAmbiguityAndArgumentErrorsAreRepeatedInsteadOfCached()
    {
        var index = new LoadingReflectionIndex();
        for (int i = 0; i < 2; i++)
        {
            Assert.Throws<AmbiguousMatchException>((Action)(() => index.Method(typeof(Generic<int>), "Overloaded", All, 1)));
            Assert.Throws<ArgumentNullException>((Action)(() => index.Field(typeof(Generic<int>), null!, All, 1)));
        }
        Assert.That(index.Count, Is.Zero);
    }

    [Test]
    public void FieldArraysRetainNativeOrderAndFreshOwnership()
    {
        var index = new LoadingReflectionIndex();
        FieldInfo[] expected = typeof(Generic<int>).GetFields(All);
        FieldInfo[] first = index.Fields(typeof(Generic<int>), All, 1);
        first[0] = null!;
        FieldInfo[] second = index.Fields(typeof(Generic<int>), All, 1);
        Assert.That(second, Is.EqualTo(expected));
        second[0] = null!;
        Assert.That(index.Fields(typeof(Generic<int>), All, 1), Is.EqualTo(expected));
    }

    [Test]
    public void GenerationChangesReleaseMetadataAndDynamicTypesRemainNative()
    {
        var index = new LoadingReflectionIndex();
        index.Field(typeof(Generic<int>), "Value", All, 1);
        index.Field(typeof(Generic<int>), "Value", All, 2);
        Assert.That(index.Hits, Is.Zero);
        Assert.That(index.Count, Is.EqualTo(1));
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("LoadingReflectionDynamic"), AssemblyBuilderAccess.Run);
        TypeBuilder builder = assembly.DefineDynamicModule("main").DefineType("Added", TypeAttributes.Public);
        builder.DefineField("Value", typeof(int), FieldAttributes.Public);
        Type added = builder.CreateType()!;
        Assert.That(index.Field(added, "Value", Public, 3), Is.EqualTo(added.GetField("Value")));
        Assert.That(index.Field(added, "Value", Public, 3), Is.EqualTo(added.GetField("Value")));
        Assert.That(index.Hits, Is.Zero);
        index.Clear();
        Assert.That(index.Count, Is.Zero);
    }

    [Test]
    public void PlainTypeDelegatorCannotHideAnObservableCustomResolver()
    {
        var index = new LoadingReflectionIndex();
        var custom = new CountingType(typeof(Generic<int>));
        var delegated = new TypeDelegator(custom);
        Assert.That(index.Field(delegated, "Value", All, 1), Is.Not.Null);
        Assert.That(index.Field(delegated, "Value", All, 1), Is.Not.Null);
        Assert.That(custom.Reads, Is.EqualTo(2));
        Assert.That(index.Count, Is.Zero);
    }

    private sealed class CountingType : TypeDelegator
    {
        internal int Reads;
        internal CountingType(Type type) : base(type) { }
        public override FieldInfo? GetField(string name, BindingFlags bindingAttr)
        {
            Reads++;
            return base.GetField(name, bindingAttr);
        }
    }

    [Test]
    public void MemberReuseLeavesNativeAttributeConstructionEffects()
    {
        var index = new LoadingReflectionIndex();
        FieldInfo field = typeof(Generic<int>).GetField("Value")!;
        CountingAttribute.Constructions = 0;
        for (int i = 0; i < 2; i++)
            Assert.That(GenAttribute.HasAttribute<CountingAttribute>(index.Field(typeof(Generic<int>), "Value", Public, 1)!), Is.True);
        Assert.That(CountingAttribute.Constructions, Is.Zero, "Native IsDefined has no constructor effects.");
        var first = (CountingAttribute)field.GetCustomAttributes(typeof(CountingAttribute), true).Single();
        var second = (CountingAttribute)field.GetCustomAttributes(typeof(CountingAttribute), true).Single();
        Assert.That(CountingAttribute.Constructions, Is.EqualTo(2));
        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(index.FieldHits, Is.EqualTo(1));
    }

    [Test]
    public void MetadataReusePreservesGetterAndMethodEffects()
    {
        var index = new LoadingReflectionIndex();
        Generic<int>.Calls = 0;
        for (int i = 0; i < 2; i++)
        {
            index.Method(typeof(Generic<int>), "Read", Public, 1)!.Invoke(null, null);
            index.Property(typeof(Generic<int>), "Next", Public, 1)!.GetValue(null, null);
        }
        Assert.That(Generic<int>.Calls, Is.EqualTo(4));
        Assert.That(index.Hits, Is.EqualTo(2));
    }

    public sealed class Generic<T>
    {
        [Counting] public T? Value;
#pragma warning disable CS0169
        private int hidden;
#pragma warning restore CS0169
        public static int Calls;
        public static int Next => ++Calls;
        public static int Read() => ++Calls;
        public static void Overloaded() { }
        public static void Overloaded(int value) { }
    }
    public sealed class CountingAttribute : Attribute
    {
        public static int Constructions;
        public CountingAttribute() { Constructions++; }
    }
}
