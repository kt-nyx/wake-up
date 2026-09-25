// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace WakeUp;

// A matching file/MVID is not enough: Prepatcher can edit a loaded callback
// without changing its MVID. Compare its actual IL, locals and exception regions
// with the already hash-qualified disk image. Never load a second assembly.
internal static class SupplierBodyIdentity
{
    private static readonly Dictionary<MethodBase, bool> checkedBodies = new();
    private static readonly HashSet<Assembly> reported = new();
    private static string instructionMismatch = "";
    internal static bool Matches(MethodBase method)
    {
        if (checkedBodies.TryGetValue(method, out bool value)) return value;
        try
        {
            using var stream = File.OpenRead(LoaderSupplierPolicy.SourceImage(method.Module.Assembly));
            using var module = ModuleDefinition.ReadModule(stream);
            // Cecil/Prepatcher may renumber every metadata row without changing
            // behavior. Resolve the named signature, never a runtime token in a
            // different image's metadata table.
            var original = module.GetType(method.DeclaringType!.FullName.Replace('+', '/'))?.Methods
                .SingleOrDefault(m => MemberMatches(m, method));
            var actual = method.GetMethodBody();
            value = original?.HasBody == true && actual != null && original.Name == method.Name
                && original.DeclaringType.FullName.Replace('/', '+') == method.DeclaringType?.FullName
                && module.Mvid == method.Module.ModuleVersionId
                && original.Body.InitLocals == actual.InitLocals && original.Body.MaxStackSize == actual.MaxStackSize
                && original.Body.Variables.Select(v => (v.IsPinned ? "pinned:" : "") + CecilName(v.VariableType))
                    .SequenceEqual(actual.LocalVariables.Select(v => (v.IsPinned ? "pinned:" : "") + RuntimeName(v.LocalType)))
                && InstructionsMatch(original, actual.GetILAsByteArray(), method)
                && RegionsMatch(original, actual);
            if (!value && reported.Add(method.Module.Assembly))
                Verse.Log.Message("[Wake-Up] Supplier callback body differs: " + method.DeclaringType?.FullName + "." + method.Name
                    + "; disk=" + original?.FullName + "; loadedToken=" + method.MetadataToken
                    + "; initLocals=" + original?.Body.InitLocals + "/" + actual?.InitLocals
                    + "; maxStack=" + original?.Body.MaxStackSize + "/" + actual?.MaxStackSize
                    + "; sourceOwner=" + CType(module.GetType(method.DeclaringType!.FullName.Replace('+', '/')))
                    + "; loadedOwner=" + RType(method.DeclaringType!)
                    + "; sourceReturn=" + string.Join(",", module.GetType(method.DeclaringType!.FullName.Replace('+', '/')).Methods.Where(m => m.Name == method.Name).Select(m => CType(m.ReturnType)))
                    + "; loadedReturn=" + RType(method is MethodInfo info ? info.ReturnType : typeof(void))
                    + "; sourceParameters=" + string.Join("/", module.GetType(method.DeclaringType!.FullName.Replace('+', '/')).Methods.Where(m => m.Name == method.Name).Select(m => string.Join(",", m.Parameters.Select(p => CType(p.ParameterType)))))
                    + "; loadedParameters=" + string.Join(",", method.GetParameters().Select(p => RModified(p.ParameterType, p.GetRequiredCustomModifiers(), p.GetOptionalCustomModifiers())))
                    + "; operand=" + instructionMismatch
                    + "; rawIL=" + (original?.HasBody == true && actual != null && OriginalIl(stream, original.RVA).SequenceEqual(actual.GetILAsByteArray())));
        }
        catch (Exception error)
        {
            value = false;
            if (reported.Add(method.Module.Assembly)) Verse.Log.Message("[Wake-Up] Supplier callback identity unavailable: " + error);
        }
        checkedBodies[method] = value;
        return value;
    }
    internal static bool InstructionsMatch(MethodDefinition original, byte[] actual, MethodBase method)
    {
        instructionMismatch = "";
        // Preserve instruction/branch encoding. Only replace token bytes after
        // resolving both operands to the same named member, type or string.
        if (actual.Length != original.Body.CodeSize) return false;
        foreach (var instruction in original.Body.Instructions)
        {
            int at = instruction.Offset;
            ushort code = unchecked((ushort)instruction.OpCode.Value);
            if (instruction.OpCode.Size == 2) { if (actual[at++] != 0xfe) return false; }
            if (actual[at++] != (byte)code) return false;
            int length = instruction.Next?.Offset ?? original.Body.CodeSize;
            length -= at;
            switch (instruction.OpCode.OperandType)
            {
                case Mono.Cecil.Cil.OperandType.InlineString:
                    if (length != 4 || method.Module.ResolveString(BitConverter.ToInt32(actual, at)) != (string)instruction.Operand) return false;
                    break;
                case Mono.Cecil.Cil.OperandType.InlineField:
                case Mono.Cecil.Cil.OperandType.InlineMethod:
                case Mono.Cecil.Cil.OperandType.InlineType:
                case Mono.Cecil.Cil.OperandType.InlineTok:
                    if (length != 4) return false;
                    int token = BitConverter.ToInt32(actual, at), table = (int)((uint)token >> 24);
                    Type[]? types = method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null;
                    Type[]? methods = method.IsGenericMethod ? method.GetGenericArguments() : null;
                    MemberInfo member = table == 1 || table == 2 || table == 0x1b
                        ? method.Module.ResolveType(token, types, methods) : method.Module.ResolveMember(token, types, methods);
                    if (!MemberMatches((MemberReference)instruction.Operand, member))
                    {
                        var sourceType = instruction.Operand as TypeReference ?? ((MemberReference)instruction.Operand).DeclaringType;
                        var loadedType = member as Type ?? member.DeclaringType!;
                        instructionMismatch = instruction + " -> " + member + "; sourceType=" + CType(sourceType)
                            + "; loadedType=" + RType(loadedType);
                        if (instruction.Operand is FieldReference field && member is FieldInfo runtimeField)
                            instructionMismatch += "; sourceFieldType=" + CType(field.FieldType, (field.DeclaringType as GenericInstanceType)?.GenericArguments.ToArray())
                                + "; loadedFieldType=" + RModified(runtimeField.FieldType, runtimeField.GetRequiredCustomModifiers(), runtimeField.GetOptionalCustomModifiers());
                        return false;
                    }
                    break;
                case Mono.Cecil.Cil.OperandType.InlineSig: return false;
                default:
                    // Cecil's writer retains this method's non-token operand bytes.
                    // Compare numeric values and branch offsets without metadata.
                    // Index the existing IL buffer directly instead of constructing
                    // and advancing a slice iterator for every instruction.
                    byte[] expected = OperandBytes(instruction, at);
                    if (expected.Length != length) return false;
                    for (int i = 0; i < length; i++)
                        if (expected[i] != actual[at + i]) return false;
                    break;
            }
        }
        return true;
    }
    private static byte[] OperandBytes(Mono.Cecil.Cil.Instruction instruction, int afterOpcode)
    {
        object value = instruction.Operand;
        switch (instruction.OpCode.OperandType)
        {
            case Mono.Cecil.Cil.OperandType.InlineNone: return Array.Empty<byte>();
            case Mono.Cecil.Cil.OperandType.ShortInlineBrTarget: return new[] { unchecked((byte)(((Mono.Cecil.Cil.Instruction)value).Offset - afterOpcode - 1)) };
            case Mono.Cecil.Cil.OperandType.InlineBrTarget: return BitConverter.GetBytes(((Mono.Cecil.Cil.Instruction)value).Offset - afterOpcode - 4);
            case Mono.Cecil.Cil.OperandType.InlineSwitch:
                var targets = (Mono.Cecil.Cil.Instruction[])value;
                return BitConverter.GetBytes(targets.Length).Concat(targets.SelectMany(t => BitConverter.GetBytes(t.Offset - afterOpcode - 4 - targets.Length * 4))).ToArray();
            case Mono.Cecil.Cil.OperandType.ShortInlineI: return new[] { unchecked((byte)Convert.ToSByte(value)) };
            case Mono.Cecil.Cil.OperandType.InlineI: return BitConverter.GetBytes((int)value);
            case Mono.Cecil.Cil.OperandType.InlineI8: return BitConverter.GetBytes((long)value);
            case Mono.Cecil.Cil.OperandType.ShortInlineR: return BitConverter.GetBytes((float)value);
            case Mono.Cecil.Cil.OperandType.InlineR: return BitConverter.GetBytes((double)value);
            case Mono.Cecil.Cil.OperandType.ShortInlineVar:
            case Mono.Cecil.Cil.OperandType.InlineVar:
            case Mono.Cecil.Cil.OperandType.ShortInlineArg:
            case Mono.Cecil.Cil.OperandType.InlineArg:
                int index = value is Mono.Cecil.Cil.VariableDefinition variable ? variable.Index
                    : ((ParameterDefinition)value).Index + (((ParameterDefinition)value).Method.HasThis ? 1 : 0);
                return instruction.OpCode.OperandType == Mono.Cecil.Cil.OperandType.ShortInlineVar || instruction.OpCode.OperandType == Mono.Cecil.Cil.OperandType.ShortInlineArg
                    ? new[] { (byte)index } : BitConverter.GetBytes((ushort)index);
            default: throw new InvalidDataException("Unsupported supplier operand");
        }
    }
    private static bool MemberMatches(MemberReference original, MemberInfo actual)
    {
        if (original is TypeReference type && actual is Type runtimeType) return CType(type) == RType(runtimeType);
        if (original.Name != actual.Name || CType(original.DeclaringType) != RType(actual.DeclaringType!)) return false;
        if (original is FieldReference field && actual is FieldInfo runtimeField) return CType(field.FieldType, (field.DeclaringType as GenericInstanceType)?.GenericArguments.ToArray())
            == RModified(runtimeField.FieldType, runtimeField.GetRequiredCustomModifiers(), runtimeField.GetOptionalCustomModifiers());
        if (original is MethodReference method && actual is MethodBase runtimeMethod)
        {
            if (method.HasThis == runtimeMethod.IsStatic || method.Parameters.Count != runtimeMethod.GetParameters().Length) return false;
            var typeArguments = (method.DeclaringType as GenericInstanceType)?.GenericArguments.ToArray();
            var methodArguments = (method as GenericInstanceMethod)?.GenericArguments.ToArray();
            if (actual is MethodInfo info && CType(method.ReturnType, typeArguments, methodArguments) != RModified(info.ReturnType, info.ReturnParameter.GetRequiredCustomModifiers(), info.ReturnParameter.GetOptionalCustomModifiers())) return false;
            if (!method.Parameters.Select(p => CType(p.ParameterType, typeArguments, methodArguments)).SequenceEqual(runtimeMethod.GetParameters().Select(p => RModified(p.ParameterType, p.GetRequiredCustomModifiers(), p.GetOptionalCustomModifiers())))) return false;
            var args = method is GenericInstanceMethod generic ? generic.GenericArguments.Select(CType)
                : method.GenericParameters.Select(CType);
            return args.SequenceEqual(runtimeMethod.IsGenericMethod ? runtimeMethod.GetGenericArguments().Select(RType) : Array.Empty<string>());
        }
        return false;
    }
    private static string CType(TypeReference type) => CType(type, null, null);
    private static string CType(TypeReference type, TypeReference[]? typeArguments, TypeReference[]? methodArguments = null)
    {
        if (type is RequiredModifierType required) return CType(required.ElementType, typeArguments, methodArguments) + " modreq(" + CType(required.ModifierType) + ")";
        if (type is OptionalModifierType optional) return CType(optional.ElementType, typeArguments, methodArguments) + " modopt(" + CType(optional.ModifierType) + ")";
        if (type is GenericParameter parameter)
        {
            var arguments = parameter.Type == GenericParameterType.Method ? methodArguments : typeArguments;
            if (arguments != null && parameter.Position < arguments.Length) return CType(arguments[parameter.Position]);
            return (parameter.Type == GenericParameterType.Method ? "!!" : "!") + parameter.Position;
        }
        if (type is ByReferenceType reference) return CType(reference.ElementType, typeArguments, methodArguments) + "&";
        if (type is PointerType pointer) return CType(pointer.ElementType, typeArguments, methodArguments) + "*";
        if (type is ArrayType array) return CType(array.ElementType, typeArguments, methodArguments) + "[" + new string(',', array.Rank - 1) + "]";
        if (type is GenericInstanceType generic) return CTypeBase(generic.ElementType) + "<" + string.Join(",", generic.GenericArguments.Select(t => CType(t, typeArguments, methodArguments))) + ">";
        return CTypeBase(type) + (type.HasGenericParameters ? "<" + string.Join(",", type.GenericParameters.Select(CType)) + ">" : "");
    }
    private static string CTypeBase(TypeReference type)
    {
        var reference = type.Scope as AssemblyNameReference;
        // The exact hash-qualified supplier references the Steam game version;
        // Mono binds that unsigned reference to the active GOG game. Only this
        // native game binding is normalized, never another mod/assembly identity.
        string assembly = reference?.Name == "Assembly-CSharp" && reference.PublicKeyToken.Length == 0 && string.IsNullOrEmpty(reference.Culture)
            ? "active-rimworld-game" : reference?.FullName ?? type.Module.Assembly.Name.FullName;
        // The qualified suppliers reference 2.4.1; current Prepatcher supplies
        // 2.4.2. Admit only this observed unsigned binding, to the actual Harmony
        // assembly used by Wake-Up. Unrelated versions retain exact identities.
        if (reference?.Name == "0Harmony" && reference.PublicKeyToken.Length == 0 && string.IsNullOrEmpty(reference.Culture)
            && (reference.Version == new Version(2, 4, 1, 0) || reference.Version == new Version(2, 4, 2, 0))
            && typeof(HarmonyLib.AccessTools).Assembly.GetName().FullName == "0Harmony, Version=2.4.2.0, Culture=neutral, PublicKeyToken=null")
            assembly = "active-harmony-2.4.2";
        return assembly + ":" + type.FullName.Replace('/', '+');
    }
    private static string RType(Type type)
    {
        if (type.IsGenericParameter) return (type.DeclaringMethod != null ? "!!" : "!") + type.GenericParameterPosition;
        if (type.IsByRef) return RType(type.GetElementType()!) + "&";
        if (type.IsPointer) return RType(type.GetElementType()!) + "*";
        if (type.IsArray) return RType(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        string assembly = type.Assembly == typeof(Verse.LoadedModManager).Assembly ? "active-rimworld-game" : type.Assembly.GetName().FullName;
        if (type.Assembly == typeof(HarmonyLib.AccessTools).Assembly
            && assembly == "0Harmony, Version=2.4.2.0, Culture=neutral, PublicKeyToken=null") assembly = "active-harmony-2.4.2";
        return assembly + ":" + (type.IsGenericType ? type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(RType)) + ">" : type.FullName);
    }
    private static string RModified(Type type, Type[] required, Type[] optional) => RType(type)
        + string.Concat(required.Select(t => " modreq(" + RType(t) + ")")) + string.Concat(optional.Select(t => " modopt(" + RType(t) + ")"));
    private static string CecilName(TypeReference type)
        => type is PinnedType pinned ? CecilName(pinned.ElementType)
            : type is ArrayType array ? CecilName(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]"
            : type is GenericInstanceType generic ? generic.ElementType.FullName.Replace('/', '+') + "<" + string.Join(",", generic.GenericArguments.Select(CecilName)) + ">"
            : type.FullName.Replace('/', '+');
    private static string RuntimeName(Type type)
        => type.IsArray ? RuntimeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]"
            : type.IsGenericType ? type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(RuntimeName)) + ">"
            : type.FullName ?? type.Name;
    private static bool RegionsMatch(MethodDefinition original, MethodBody actual)
    {
        var expected = original.Body.ExceptionHandlers;
        var clauses = actual.ExceptionHandlingClauses;
        if (expected.Count != clauses.Count) return false;
        for (int i = 0; i < expected.Count; i++)
        {
            var x = expected[i]; var y = clauses[i];
            int endTry = x.TryEnd?.Offset ?? original.Body.CodeSize, endHandler = x.HandlerEnd?.Offset ?? original.Body.CodeSize;
            if ((int)x.HandlerType != (int)y.Flags || x.TryStart.Offset != y.TryOffset || endTry - x.TryStart.Offset != y.TryLength
                || x.HandlerStart.Offset != y.HandlerOffset || endHandler - x.HandlerStart.Offset != y.HandlerLength) return false;
            if (y.Flags == ExceptionHandlingClauseOptions.Filter && x.FilterStart.Offset != y.FilterOffset) return false;
            if (y.Flags == ExceptionHandlingClauseOptions.Clause && x.CatchType.FullName.Replace('/', '+') != y.CatchType.FullName) return false;
        }
        return true;
    }
    internal static byte[] OriginalIl(Stream stream, int rva)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        stream.Position = 0x3c;
        int pe = reader.ReadInt32(); stream.Position = pe;
        if (reader.ReadUInt32() != 0x00004550) throw new InvalidDataException("Not a PE image");
        stream.Position = pe + 6; int sections = reader.ReadUInt16();
        stream.Position = pe + 20; int optional = reader.ReadUInt16();
        long offset = -1;
        for (int i = 0; i < sections; i++)
        {
            stream.Position = pe + 24 + optional + i * 40 + 8;
            uint virtualSize = reader.ReadUInt32(), address = reader.ReadUInt32(), rawSize = reader.ReadUInt32(), raw = reader.ReadUInt32();
            if (rva >= address && rva - address < Math.Max(virtualSize, rawSize)) { offset = raw + rva - address; break; }
        }
        if (offset < 0) throw new InvalidDataException("Missing method section");
        stream.Position = offset;
        byte first = reader.ReadByte(); int size;
        if ((first & 3) == 2) size = first >> 2;
        else if ((first & 3) == 3)
        {
            stream.Position = offset; int header = reader.ReadUInt16() >> 12;
            stream.Position = offset + 4; size = reader.ReadInt32();
            stream.Position = offset + header * 4;
        }
        else throw new InvalidDataException("Unknown method header");
        if (size < 0 || size > 1024 * 1024) throw new InvalidDataException("Invalid method size");
        var bytes = reader.ReadBytes(size);
        if (bytes.Length != size) throw new EndOfStreamException();
        return bytes;
    }
}
