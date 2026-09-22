// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class LoadingAttributeIndexTests
{
    [Test]
    public void ActualAttributeTypesAndBothInheritanceValuesHaveIndependentBooleanEntries()
    {
        var index = new LoadingReflectionIndex();
        MemberInfo[] members = { typeof(Derived), typeof(Derived).GetMethod("Act")!, typeof(Base).GetField("Value")! };
        foreach (MemberInfo member in members)
        foreach (Type attribute in new[] { typeof(FirstAttribute), typeof(SecondAttribute), typeof(ObsoleteAttribute) })
        foreach (bool inherit in new[] { false, true })
        for (int i = 0; i < 2; i++)
            Assert.That(index.IsDefined(member, attribute, inherit, 1), Is.EqualTo(Attribute.IsDefined(member, attribute, inherit)));
        Assert.That(index.AttributeHits, Is.EqualTo(18));
        Assert.That(index.AttributeMisses, Is.EqualTo(18));
        Assert.That(index.Count, Is.EqualTo(18));
        Assert.That(index.IsDefined(typeof(Derived), typeof(FirstAttribute), false, 1), Is.False);
        Assert.That(index.IsDefined(typeof(Derived), typeof(FirstAttribute), true, 1), Is.True);
    }

    [Test]
    public void InvalidArgumentsAndCustomMetadataRepeatTheNativeOperation()
    {
        var index = new LoadingReflectionIndex();
        var custom = new CustomMember();
        for (int i = 0; i < 2; i++)
        {
            Assert.That(index.IsDefined(custom, typeof(FirstAttribute), true, 1), Is.True);
            Assert.Throws<ArgumentNullException>((Action)(() => index.IsDefined(null!, typeof(FirstAttribute), true, 1)));
            Assert.Throws<ArgumentNullException>((Action)(() => index.IsDefined(typeof(Base), null!, true, 1)));
            Assert.Throws<ArgumentException>((Action)(() => index.IsDefined(typeof(Base), typeof(string), true, 1)));
            var delegated = new TypeDelegator(typeof(FirstAttribute));
            Assert.That(index.IsDefined(typeof(Base), delegated, true, 1), Is.EqualTo(Attribute.IsDefined(typeof(Base), delegated, true)));
        }
        Assert.That(custom.Calls, Is.EqualTo(2));
        Assert.That(index.Count, Is.Zero);
        Assert.That(index.AttributeHits, Is.Zero);
    }

    [Test]
    public void GenerationAndScopeClearReleaseAttributeMetadata()
    {
        var index = new LoadingReflectionIndex();
        index.IsDefined(typeof(Base), typeof(FirstAttribute), true, 1);
        index.IsDefined(typeof(Base), typeof(FirstAttribute), true, 2);
        Assert.That(index.AttributeHits, Is.Zero);
        Assert.That(index.Count, Is.EqualTo(1));
        index.Clear();
        Assert.That(index.Count, Is.Zero);
        index.IsDefined(typeof(Base), typeof(FirstAttribute), true, 2);
        Assert.That(index.AttributeHits, Is.Zero);
    }

    [Test]
    public void OnlyBooleansAreCachedAndAttributeInstancesRetainConstructionAndMutation()
    {
        var index = new LoadingReflectionIndex();
        FirstAttribute.Constructions = 0;
        for (int i = 0; i < 2; i++) Assert.That(index.IsDefined(typeof(Base), typeof(FirstAttribute), true, 1), Is.True);
        Assert.That(FirstAttribute.Constructions, Is.Zero);
        var first = (FirstAttribute)typeof(Base).GetCustomAttributes(typeof(FirstAttribute), true).Single();
        first.Value = "changed";
        var second = (FirstAttribute)typeof(Base).GetCustomAttributes(typeof(FirstAttribute), true).Single();
        Assert.That(FirstAttribute.Constructions, Is.EqualTo(2));
        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(second.Value, Is.EqualTo("native"));
    }

    [Test]
    public void MissingPrepatchAndUninitializedBridgeStayNative()
    {
        LoadingAttributeRuntime.Initialize();
        Assert.That(LoadingAttributeRuntime.Status, Is.EqualTo("native-missing-or-changed-prepatch"));
        var custom = new CustomMember();
        for (int i = 0; i < 2; i++) Assert.That(LoadingAttributeRuntime.IsDefined(custom, typeof(FirstAttribute), true), Is.True);
        Assert.That(custom.Calls, Is.EqualTo(2));
    }

    [First] public class Base
    {
        [Second] public int Value;
        [First] public virtual void Act() { }
    }
    public sealed class Derived : Base { public override void Act() { } }
    [AttributeUsage(AttributeTargets.All, Inherited = true)]
    public sealed class FirstAttribute : Attribute
    {
        public static int Constructions;
        public string Value = "native";
        public FirstAttribute() { Constructions++; }
    }
    public sealed class SecondAttribute : Attribute { }
    private sealed class CustomMember : MemberInfo
    {
        internal int Calls;
        public override Type DeclaringType => typeof(Base);
        public override Type ReflectedType => typeof(Base);
        public override MemberTypes MemberType => MemberTypes.Field;
        public override string Name => "custom";
        public override bool IsDefined(Type attributeType, bool inherit) { Calls++; return true; }
        public override object[] GetCustomAttributes(bool inherit) => throw new InvalidOperationException();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => throw new InvalidOperationException();
    }
}
