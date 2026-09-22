// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

// Only the pinned ReloadAll clone may use this continuation contract. Arbitrary
// enumerator consumers still suspend. Its successful continuation normalizes a
// string and inserts it into the holder dictionary/trie; duplicate paths log and
// therefore must suspend before yielding, even with no patches on ReloadAll.
internal sealed class PngConsumerContract
{
    private sealed class Shape
    {
        internal FieldInfo HolderTrie = null!, StringTrie = null!, Comparer = null!, Root = null!, Children = null!, Terminal = null!;
        internal Type StringType = null!, TrieType = null!, NodeType = null!, DictionaryType = null!;
        internal PropertyInfo DictionaryComparer = null!;
        internal PublishedPatchGuard.PublicationSet Publications = null!;
    }
    private static Shape? shape;
    private static bool initialized;
    private readonly ModContentHolder<Texture2D> holder;
    private readonly Dictionary<string, Texture2D> content;
    private readonly object stringTrie, trie, root;
    private PngConsumerContract(ModContentHolder<Texture2D> holder, Dictionary<string, Texture2D> content,
        object stringTrie, object trie, object root)
    { this.holder = holder; this.content = content; this.stringTrie = stringTrie; this.trie = trie; this.root = root; }

    // Call before queue admission, after PngRuntime's existing exact game/Harmony
    // identity checks. Only this GOG body's transitive calls were inspected.
    internal static PngConsumerContract? Capture(ModContentHolder<Texture2D> holder)
    {
        try
        {
            if (!initialized) { initialized = true; shape = CaptureShape(); }
            var s = shape;
            if (s == null || !s.Publications.Unchanged || holder == null) return null;
            var content = holder.contentList;
            if (content == null || content.GetType() != typeof(Dictionary<string, Texture2D>)
                || !ReferenceEquals(content.Comparer, EqualityComparer<string>.Default)) return null;
            object? strings = s.HolderTrie.GetValue(holder);
            if (strings == null || strings.GetType() != s.StringType) return null;
            object? trie = s.StringTrie.GetValue(strings);
            if (trie == null || trie.GetType() != s.TrieType
                || !ReferenceEquals(s.Comparer.GetValue(trie), EqualityComparer<char>.Default)) return null;
            object? root = s.Root.GetValue(trie);
            if (root == null || root.GetType() != s.NodeType) return null;
            return new PngConsumerContract(holder, content, strings, trie, root);
        }
        catch { return null; }
    }

    internal bool Allows(string key)
    {
        try
        {
            var s = shape;
            if (s == null || !s.Publications.Unchanged || !ReferenceEquals(holder.contentList, content)
                || !ReferenceEquals(content.Comparer, EqualityComparer<string>.Default)
                || !ReferenceEquals(s.HolderTrie.GetValue(holder), stringTrie)
                || !ReferenceEquals(s.StringTrie.GetValue(stringTrie), trie)
                || !ReferenceEquals(s.Root.GetValue(trie), root)
                || !ReferenceEquals(s.Comparer.GetValue(trie), EqualityComparer<char>.Default)) return false;
            // Match the inspected GOG/Steam holder exactly, without calling the supplier
            // ContentPath helper while deciding whether callbacks are safe.
            string path = key.Replace('\\', '/');
            if (path.StartsWith("Textures/")) path = path.Substring("Textures/".Length);
            if (path.EndsWith(Path.GetExtension(path))) path = path.Substring(0, path.Length - Path.GetExtension(path).Length);
            if (content.ContainsKey(path)) return false; // Native duplicate warning.
            object node = root;
            foreach (char character in path)
            {
                if (node.GetType() != s.NodeType) return false;
                object? children = s.Children.GetValue(node);
                // Refuse interface dispatch to a substituted dictionary/comparer.
                if (children == null || children.GetType() != s.DictionaryType
                    || !ReferenceEquals(s.DictionaryComparer.GetValue(children, null), EqualityComparer<char>.Default)) return false;
                var dictionary = (IDictionary)children;
                if (!dictionary.Contains(character)) return true; // Remaining nodes use the guarded constructor/default comparer.
                node = dictionary[character]!;
                if (node == null) return false;
            }
            // Also avoid TrieSet's duplicate exception when holder/trie disagree.
            return node.GetType() == s.NodeType && s.Terminal.GetValue(node) is bool terminal && !terminal;
        }
        catch { return false; }
    }

    private static Shape? CaptureShape()
    {
        if (!GameBuildContract.Current.HasReviewedTextureConsumers) return null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var s = new Shape { StringType = typeof(KTrie.StringTrieSet), TrieType = typeof(KTrie.TrieSet<char>) };
        s.NodeType = s.TrieType.GetNestedType("TrieNode", flags)!;
        if (s.NodeType.ContainsGenericParameters) s.NodeType = s.NodeType.MakeGenericType(typeof(char));
        s.DictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(char), s.NodeType);
        s.DictionaryComparer = s.DictionaryType.GetProperty("Comparer")!;
        s.HolderTrie = AccessTools.Field(typeof(ModContentHolder<Texture2D>), "contentListTrie");
        s.StringTrie = AccessTools.Field(s.StringType, "_trie");
        s.Comparer = AccessTools.Field(s.TrieType, "_comparer");
        s.Root = AccessTools.Field(s.TrieType, "_root");
        s.Children = AccessTools.Field(s.NodeType, "<Children>k__BackingField");
        s.Terminal = AccessTools.Field(s.NodeType, "<IsTerminal>k__BackingField");
        if (s.DictionaryComparer == null || s.HolderTrie == null || s.StringTrie == null || s.Comparer == null
            || s.Root == null || s.Children == null || s.Terminal == null) return null;
        var targets = new List<MethodBase> {
            AccessTools.Method(typeof(GenFilePaths), "ContentPath").MakeGenericMethod(typeof(Texture2D)),
            AccessTools.Method(s.StringType, "Add", new[] { typeof(string) }),
            AccessTools.Method(s.TrieType, "Add", new[] { typeof(IEnumerable<char>) }),
            AccessTools.Method(s.TrieType, "AddItem", new[] { s.NodeType, typeof(char) }),
            AccessTools.PropertyGetter(s.TrieType, "Count"), AccessTools.PropertySetter(s.TrieType, "Count"),
            AccessTools.Constructor(s.NodeType, new[] { typeof(char), typeof(IEqualityComparer<char>) }),
            AccessTools.PropertyGetter(s.NodeType, "Children"), AccessTools.PropertyGetter(s.NodeType, "IsTerminal"),
            AccessTools.PropertySetter(s.NodeType, "IsTerminal"), AccessTools.PropertySetter(s.NodeType, "Item"),
            AccessTools.PropertySetter(s.NodeType, "Parent")
        };
        var guards = new List<PublishedPatchGuard>();
        foreach (var target in targets)
        {
            if (target == null || !PublishedPatchGuard.TryCreate(target, PngRuntime.Owner, out var guard, true)) return null;
            guards.Add(guard!);
        }
        return PublishedPatchGuard.TryCapturePublications(guards, out var publications)
            ? SetPublications(s, publications!) : null;
    }
    private static Shape SetPublications(Shape s, PublishedPatchGuard.PublicationSet publications)
    { s.Publications = publications; return s; }
}
