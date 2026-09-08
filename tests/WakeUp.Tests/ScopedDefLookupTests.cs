// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class ScopedDefLookupTests
{
    [TestCase("Defs/ThingDef[defName='Beer']")]
    [TestCase("/Defs/ThingDef[defName=\"Beer\"]/statBases/MarketValue")]
    [TestCase("Defs/ThingDef[defName = 'Beer']/statBases/*[position()=1]")]
    [TestCase("Defs/ThingDef[defName='Beer']/@Name")]
    [TestCase("Defs/ThingDef[defName='Beer']/missing")]
    public void UniqueLiteralLookupReturnsTheOriginalNodes(string xpath)
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document);
        Assert.That(lookup.SelectNodes(document, xpath).Cast<XmlNode>(), Is.EqualTo(document.SelectNodes(xpath)!.Cast<XmlNode>()));
        Assert.That(lookup.SelectSingleNode(document, xpath), Is.SameAs(document.SelectSingleNode(xpath)));
        Assert.That(lookup.Hits, Is.EqualTo(2));
        Assert.That(lookup.Rebuilds, Is.EqualTo(1));
    }

    [Test]
    public void DuplicateDefsAndMultipleSuffixResultsRetainTheNativeListAndOrder()
    {
        XmlDocument document = Sample();
        XmlNode original = document.DocumentElement!.FirstChild!;
        document.DocumentElement.AppendChild(original.CloneNode(true));
        using var lookup = new ScopedDefLookup(document);
        const string duplicate = "Defs/ThingDef[defName='Beer']";
        XmlNodeList result = lookup.SelectNodes(document, duplicate);
        Assert.That(result.GetType(), Is.EqualTo(document.SelectNodes(duplicate)!.GetType()));
        Assert.That(result.Cast<XmlNode>(), Is.EqualTo(document.SelectNodes(duplicate)!.Cast<XmlNode>()));
        Assert.That(lookup.SelectSingleNode(document, duplicate), Is.SameAs(original));
        document.DocumentElement.RemoveChild(document.DocumentElement.LastChild!);
        const string multiple = "Defs/ThingDef[defName='Beer']/statBases/*";
        Assert.That(lookup.SelectNodes(document, multiple).Cast<XmlNode>(), Is.EqualTo(document.SelectNodes(multiple)!.Cast<XmlNode>()));
        Assert.That(lookup.Hits, Is.Zero);
        Assert.That(lookup.Fallbacks, Is.EqualTo(3));
    }

    [Test]
    public void RootMembershipAndNestedDefNameTextMutationsInvalidateButStatEditsDoNot()
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document);
        const string beer = "Defs/ThingDef[defName='Beer']";
        XmlNode first = lookup.SelectSingleNode(document, beer)!;
        first["statBases"]!["MarketValue"]!.InnerText = "99";
        first["statBases"]!.AppendChild(document.CreateElement("Mass"));
        Assert.That(lookup.SelectSingleNode(document, beer), Is.SameAs(first));
        Assert.That(lookup.Rebuilds, Is.EqualTo(1), "Unrelated patch writes must not rebuild the name index.");

        XmlNode name = first["defName"]!;
        name.InnerXml = "<part>Wine</part>";
        const string wine = "Defs/ThingDef[defName='Wine']";
        Assert.That(lookup.SelectSingleNode(document, wine), Is.SameAs(first));
        Assert.That(lookup.SelectSingleNode(document, beer), Is.Null);
        name.FirstChild!.FirstChild!.Value = "Ale";
        const string ale = "Defs/ThingDef[defName='Ale']";
        Assert.That(lookup.SelectSingleNode(document, ale), Is.SameAs(first));
        Assert.That(lookup.SelectSingleNode(document, wine), Is.Null);

        XmlNode duplicate = document.DocumentElement!.AppendChild(first.CloneNode(true))!;
        Assert.That(lookup.SelectNodes(document, ale).Count, Is.EqualTo(2));
        document.DocumentElement.RemoveChild(first);
        Assert.That(lookup.SelectSingleNode(document, ale), Is.SameAs(duplicate), "Removal must use the old parent after disconnection.");
        document.DocumentElement.RemoveChild(duplicate);
        Assert.That(lookup.SelectSingleNode(document, ale), Is.Null);
        Assert.That(lookup.Invalidations, Is.GreaterThanOrEqualTo(5));
    }

    [Test]
    public void QueryDuringChangingCallbackCannotLeaveAnIndexOfTheOldValue()
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document);
        const string beer = "Defs/ThingDef[defName='Beer']";
        lookup.SelectSingleNode(document, beer);
        document.NodeChanging += (_, _) => lookup.SelectSingleNode(document, beer);
        document.DocumentElement!.FirstChild!["defName"]!.FirstChild!.Value = "Wine";
        Assert.That(lookup.SelectSingleNode(document, beer), Is.Null);
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[defName='Wine']"), Is.Not.Null);
    }

    [Test]
    public void CallbacksOnBothSidesOfTheLookupSeeDuplicateInsertionAndNestedMutation()
    {
        XmlDocument document = Sample();
        XmlNode duplicate = document.DocumentElement!.FirstChild!.CloneNode(true);
        const string beer = "Defs/ThingDef[defName='Beer']";
        ScopedDefLookup? lookup = null;
        int afterCount = -1;
        // This callback runs before the lookup's after-event callback.
        document.NodeInserted += (_, args) =>
        {
            if (ReferenceEquals(args.Node, duplicate))
                afterCount = lookup!.SelectNodes(document, beer).Count;
        };
        using (lookup = new ScopedDefLookup(document))
        {
            lookup.SelectSingleNode(document, beer);
            // This callback runs after the lookup's before-event callback.
            document.NodeInserting += (_, args) =>
            {
                if (!ReferenceEquals(args.Node, duplicate))
                    return;
                Assert.That(lookup.SelectNodes(document, beer).Count, Is.EqualTo(1));
                document.DocumentElement.FirstChild!["statBases"]!["MarketValue"]!.FirstChild!.Value = "99";
            };
            document.DocumentElement.AppendChild(duplicate);
            Assert.That(afterCount, Is.EqualTo(2));
            document.DocumentElement.RemoveChild(duplicate);
            long hits = lookup.Hits;
            Assert.That(lookup.SelectSingleNode(document, beer), Is.SameAs(document.DocumentElement.FirstChild));
            Assert.That(lookup.Hits, Is.EqualTo(hits + 1), "Balanced nested events must restore indexing.");
        }
    }

    [Test]
    public void AbortedMutationKeepsSubsequentQueriesSafelyOnTheNativePath()
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document);
        const string beer = "Defs/ThingDef[defName='Beer']";
        lookup.SelectSingleNode(document, beer);
        XmlNodeChangedEventHandler abort = (_, _) => throw new InvalidOperationException("Abort this edit.");
        document.NodeChanging += abort;
        Assert.Throws<InvalidOperationException>(new Action(() => { document.DocumentElement!.FirstChild!["defName"]!.FirstChild!.Value = "Wine"; }));
        document.NodeChanging -= abort;
        document.DocumentElement!.FirstChild!["defName"]!.FirstChild!.Value = "Wine";
        long hits = lookup.Hits;
        Assert.That(lookup.SelectSingleNode(document, beer), Is.Null);
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[defName='Wine']"), Is.Not.Null);
        Assert.That(lookup.Hits, Is.EqualTo(hits));
    }

    [TestCase("<ThingDef><defName>Beer</defName></ThingDef>", "&extra;")]
    [TestCase("<defName>Beer</defName>", "<ThingDef>&extra;</ThingDef>")]
    public void ExpandedEntityChildrenUseTheNativeQuery(string entity, string secondDef)
    {
        var document = new XmlDocument();
        document.LoadXml("<!DOCTYPE Defs [<!ENTITY extra \"" + entity + "\">]><Defs>"
            + "<ThingDef><defName>Beer</defName></ThingDef>" + secondDef + "</Defs>");
        using var lookup = new ScopedDefLookup(document);
        const string beer = "Defs/ThingDef[defName='Beer']";
        Assert.That(lookup.SelectNodes(document, beer).Count, Is.EqualTo(2));
        Assert.That(lookup.Hits, Is.Zero);
    }

    [TestCase("Defs/ThingDef[defName='Beer'] | Defs/ThingDef")]
    [TestCase("Defs/ThingDef[defName='Beer']/../ThingDef")]
    [TestCase("Defs/ThingDef[defName='Beer']/following-sibling::*")]
    [TestCase("Defs/ThingDef[defName='Beer'][1]")]
    [TestCase("Defs/ThingDef[contains(defName,'Beer')]")]
    [TestCase("Defs/ThingDef[@Name='Drink']/../ThingDef")]
    [TestCase("Defs/ThingDef[@Name='Drink'][1]")]
    public void UnrecognizedOrEscapingExpressionsUseOriginalXPath(string xpath)
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document);
        Assert.That(lookup.SelectNodes(document, xpath).Cast<XmlNode>(), Is.EqualTo(document.SelectNodes(xpath)!.Cast<XmlNode>()));
        Assert.That(lookup.Hits, Is.Zero);
    }

    [Test]
    public void InvalidExpressionKeepsNativeExceptionAndLimitsFallBackWithoutDroppingContent()
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document, maximumEntries: 1);
        const string xpath = "Defs/ThingDef[defName='Beer']";
        Assert.That(lookup.SelectSingleNode(document, xpath), Is.SameAs(document.SelectSingleNode(xpath)));
        Assert.That(lookup.Hits, Is.Zero);
        Assert.That(lookup.PeakEntries, Is.LessThanOrEqualTo(1));
        Assert.Throws<XPathException>((Action)(() => lookup.SelectNodes(document, "Defs/ThingDef[defName='Beer']/[")));
        lookup.Dispose();
        Assert.That(lookup.SelectSingleNode(document, xpath), Is.SameAs(document.SelectSingleNode(xpath)));
    }

    [Test]
    public void SameDefWithRepeatedNameChildrenIsOneMatchAndOtherDefTypesWork()
    {
        var document = new XmlDocument();
        document.LoadXml("<Defs><RecipeDef><defName>A</defName><defName>A</defName><label>recipe</label></RecipeDef></Defs>");
        using var lookup = new ScopedDefLookup(document);
        const string xpath = "Defs/RecipeDef[defName='A']/label";
        Assert.That(lookup.SelectNodes(document, xpath).Cast<XmlNode>(), Is.EqualTo(document.SelectNodes(xpath)!.Cast<XmlNode>()));
        Assert.That(lookup.Hits, Is.EqualTo(1));
        Assert.That(lookup.SelectSingleNode(document.DocumentElement!, "RecipeDef"), Is.SameAs(document.DocumentElement!.FirstChild));
    }

    [Test]
    public void CustomElementsUseTheOriginalQuery()
    {
        var document = new XmlDocument();
        document.LoadXml("<Defs />");
        var custom = new CustomElement(document);
        custom.InnerXml = "<defName>Beer</defName>";
        document.DocumentElement!.AppendChild(custom);
        using var lookup = new ScopedDefLookup(document);
        const string xpath = "Defs/ThingDef[defName='Beer']";
        Assert.That(lookup.SelectSingleNode(document, xpath), Is.SameAs(document.SelectSingleNode(xpath)));
        Assert.That(lookup.Hits, Is.Zero);
    }

    private sealed class CustomElement : XmlElement
    {
        internal CustomElement(XmlDocument document) : base(string.Empty, "ThingDef", string.Empty, document) { }
    }

    [TestCase("Defs/ThingDef[@Name='Drink']")]
    [TestCase("/Defs/ThingDef[@Name = \"Drink\"]/statBases/MarketValue")]
    [TestCase("Defs/ThingDef[@Name='Drink']/missing")]
    public void LiteralAttributeLookupPreservesNativeNodeIdentity(string xpath)
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document);
        Assert.That(lookup.SelectNodes(document, xpath).Cast<XmlNode>(), Is.EqualTo(document.SelectNodes(xpath)!.Cast<XmlNode>()));
        Assert.That(lookup.SelectSingleNode(document, xpath), Is.SameAs(document.SelectSingleNode(xpath)));
        Assert.That(lookup.AttributeHits, Is.EqualTo(2));
        Assert.That(lookup.Rebuilds, Is.EqualTo(1));
    }

    [Test]
    public void AttributeDuplicatesMissingNamesAndMultipleResultsStayNative()
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document);
        const string drink = "Defs/ThingDef[@Name='Drink']";
        XmlNode first = document.DocumentElement!.FirstChild!;
        XmlNode duplicate = document.DocumentElement.AppendChild(first.CloneNode(true))!;
        Assert.That(lookup.SelectNodes(document, drink).Cast<XmlNode>(), Is.EqualTo(document.SelectNodes(drink)!.Cast<XmlNode>()));
        Assert.That(lookup.SelectSingleNode(document, drink), Is.SameAs(first));
        document.DocumentElement.RemoveChild(duplicate);
        const string many = drink + "/statBases/*";
        Assert.That(lookup.SelectNodes(document, many).Cast<XmlNode>(), Is.EqualTo(document.SelectNodes(many)!.Cast<XmlNode>()));
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[@Name='Absent']"), Is.Null);
        Assert.That(lookup.AttributeHits, Is.Zero);
        Assert.That(lookup.Fallbacks, Is.EqualTo(4));
    }

    [Test]
    public void AttributeValuesTextAndMembershipInvalidateOnlyTheAttributeIndex()
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document);
        XmlElement first = (XmlElement)document.DocumentElement!.FirstChild!;
        const string def = "Defs/ThingDef[defName='Beer']";
        const string drink = "Defs/ThingDef[@Name='Drink']";
        Assert.That(lookup.SelectSingleNode(document, def), Is.SameAs(first));
        Assert.That(lookup.SelectSingleNode(document, drink), Is.SameAs(first));
        XmlAttribute name = first.GetAttributeNode("Name")!;
        name.Value = "Food";
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[@Name='Food']"), Is.SameAs(first));
        Assert.That(lookup.SelectSingleNode(document, drink), Is.Null);
        name.FirstChild!.Value = "Meal";
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[@Name='Meal']"), Is.SameAs(first));
        name.RemoveAll();
        name.AppendChild(document.CreateTextNode("Soup"));
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[@Name='Soup']"), Is.SameAs(first));
        first.RemoveAttributeNode(name);
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[@Name='Soup']"), Is.Null);
        first.SetAttribute("OtherName", "Soup");
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[@Name='Soup']"), Is.Null);
        first.SetAttribute("Name", "Drink");
        Assert.That(lookup.SelectSingleNode(document, drink), Is.SameAs(first));
        long rebuilds = lookup.Rebuilds;
        Assert.That(lookup.SelectSingleNode(document, def), Is.SameAs(first));
        Assert.That(lookup.Rebuilds, Is.EqualTo(rebuilds), "Attribute edits must not invalidate the defName map.");
        first["defName"]!.InnerText = "Wine";
        Assert.That(lookup.SelectSingleNode(document, drink), Is.SameAs(first));
        Assert.That(lookup.Rebuilds, Is.EqualTo(rebuilds), "defName edits must not invalidate the attribute map.");
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[defName='Wine']"), Is.SameAs(first));
    }

    [Test]
    public void AttributeTextCallbacksSeeNativeValuesWhileMutationIsPending()
    {
        XmlDocument document = Sample();
        const string drink = "Defs/ThingDef[@Name='Drink']";
        const string food = "Defs/ThingDef[@Name='Food']";
        ScopedDefLookup? lookup = null;
        int afterCount = -1;
        XmlAttribute name = ((XmlElement)document.DocumentElement!.FirstChild!).GetAttributeNode("Name")!;
        document.NodeChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Node, name.FirstChild))
                afterCount = lookup!.SelectNodes(document, food).Count;
        };
        using (lookup = new ScopedDefLookup(document))
        {
            lookup.SelectSingleNode(document, drink);
            document.NodeChanging += (_, args) =>
            {
                if (!ReferenceEquals(args.Node, name.FirstChild))
                    return;
                long hits = lookup.Hits;
                Assert.That(lookup.SelectNodes(document, drink).Count, Is.EqualTo(1));
                Assert.That(lookup.Hits, Is.EqualTo(hits));
            };
            name.FirstChild!.Value = "Food";
            Assert.That(afterCount, Is.EqualTo(1));
            Assert.That(lookup.SelectSingleNode(document, drink), Is.Null);
            Assert.That(lookup.SelectSingleNode(document, food), Is.SameAs(document.DocumentElement.FirstChild));
        }
    }

    [Test]
    public void SameTypeAndLiteralKeepAttributeAndDefinitionKeysIndependent()
    {
        var document = new XmlDocument();
        document.LoadXml("<Defs><ThingDef Name='Shared'><defName>A</defName></ThingDef><ThingDef Name='B'><defName>Shared</defName></ThingDef></Defs>");
        using var lookup = new ScopedDefLookup(document);
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[@Name='Shared']"), Is.SameAs(document.DocumentElement!.FirstChild));
        Assert.That(lookup.SelectSingleNode(document, "Defs/ThingDef[defName='Shared']"), Is.SameAs(document.DocumentElement.LastChild));
        Assert.That(lookup.Rebuilds, Is.EqualTo(2));
    }

    [Test]
    public void CustomAttributeAndTextNodesRetainNativeQueries()
    {
        foreach (bool customAttribute in new[] { true, false })
        {
            XmlDocument document = Sample();
            XmlElement first = (XmlElement)document.DocumentElement!.FirstChild!;
            XmlAttribute name = customAttribute ? new CustomAttribute(document) : document.CreateAttribute("Name");
            name.AppendChild(customAttribute ? document.CreateTextNode("Drink") : new CustomText(document));
            first.SetAttributeNode(name);
            using var lookup = new ScopedDefLookup(document);
            const string xpath = "Defs/ThingDef[@Name='Drink']";
            Assert.That(lookup.SelectSingleNode(document, xpath), Is.SameAs(document.SelectSingleNode(xpath)));
            Assert.That(lookup.AttributeHits, Is.Zero);
        }
    }

    [Test]
    public void AttributeDuplicatesBecomeUniqueAfterValueChangesRemovalAndMovingOwner()
    {
        XmlDocument document = Sample();
        XmlElement first = (XmlElement)document.DocumentElement!.FirstChild!;
        XmlElement second = (XmlElement)document.DocumentElement.LastChild!;
        second.SetAttribute("Name", "Drink");
        using var lookup = new ScopedDefLookup(document);
        const string drink = "Defs/ThingDef[@Name='Drink']";
        Assert.That(lookup.SelectNodes(document, drink).Count, Is.EqualTo(2));
        second.GetAttributeNode("Name")!.Value = "Food";
        Assert.That(lookup.SelectSingleNode(document, drink), Is.SameAs(first));
        second.SetAttribute("Name", "Drink");
        Assert.That(lookup.SelectNodes(document, drink).Count, Is.EqualTo(2));
        second.RemoveAttribute("Name");
        Assert.That(lookup.SelectSingleNode(document, drink), Is.SameAs(first));
        XmlAttribute moved = first.RemoveAttributeNode(first.GetAttributeNode("Name")!)!;
        Assert.That(lookup.SelectSingleNode(document, drink), Is.Null);
        second.SetAttributeNode(moved);
        Assert.That(lookup.SelectSingleNode(document, drink), Is.SameAs(second));
        Assert.That(lookup.AttributeHits, Is.EqualTo(3));
    }

    [Test]
    public void EmptyAttributeDiffersFromMissingAndNamespacedAttribute()
    {
        var document = new XmlDocument();
        document.LoadXml("<Defs xmlns:n='urn:test'><ThingDef/><ThingDef Name=''/><ThingDef n:Name=''/></Defs>");
        using var lookup = new ScopedDefLookup(document);
        const string empty = "Defs/ThingDef[@Name='']";
        Assert.That(lookup.SelectSingleNode(document, empty), Is.SameAs(document.DocumentElement!.ChildNodes[1]));
        ((XmlElement)document.DocumentElement.ChildNodes[1]!).RemoveAttribute("Name");
        Assert.That(lookup.SelectSingleNode(document, empty), Is.Null);
        Assert.That(lookup.AttributeHits, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EntryBudgetIsSharedAcrossBothKeyKinds(bool attributeFirst)
    {
        XmlDocument document = Sample();
        using var lookup = new ScopedDefLookup(document, maximumEntries: 2);
        string[] paths = { "Defs/ThingDef[defName='Beer']", "Defs/ThingDef[@Name='Drink']" };
        if (attributeFirst)
            Array.Reverse(paths);
        foreach (string xpath in paths)
            Assert.That(lookup.SelectSingleNode(document, xpath), Is.SameAs(document.SelectSingleNode(xpath)));
        Assert.That(lookup.Hits, Is.EqualTo(1));
        Assert.That(lookup.Fallbacks, Is.EqualTo(1));
        Assert.That(lookup.PeakEntries, Is.LessThanOrEqualTo(2));
    }

    private sealed class CustomAttribute : XmlAttribute
    {
        internal CustomAttribute(XmlDocument document) : base(string.Empty, "Name", string.Empty, document) { }
    }

    private sealed class CustomText : XmlText
    {
        internal CustomText(XmlDocument document) : base("Drink", document) { }
    }

    private static XmlDocument Sample()
    {
        var document = new XmlDocument();
        document.LoadXml("<Defs><ThingDef Name='Drink'><defName>Beer</defName><statBases><MarketValue>2</MarketValue><Nutrition>1</Nutrition></statBases></ThingDef><ThingDef><defName>Other</defName></ThingDef></Defs>");
        return document;
    }
}
