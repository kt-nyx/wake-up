// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;

namespace WakeUp;

internal static class SemanticMethodIdentity
{
    private static readonly IReadOnlyDictionary<ushort, OpCode> Codes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null))
        .ToDictionary(code => unchecked((ushort)code.Value));

    // Derived from the pinned ORIGINAL GOG assembly (SHA in GameBuildContract),
    // using resolved Cecil operands, exact opcodes/branch offsets, locals and EH.
    // Steam Windows rev590 and Linux rev600 have identical original instructions,
    // operands, locals and exception regions for this constructor.
    // This value is not learned from an optimized or Prepatcher-modified body.
    internal const string ExpectedXmlAssetConstructor =
        "8672989FE7B5FF5D424A18EDBF1F6890A590E20C6C993737552A05A3A90491D5";

    internal static bool TryHash(MethodBase method, out string hash, out string reason) =>
        TryHash(method, out hash, out reason, out _);

    internal static bool TryHash(MethodBase method, out string hash, out string reason, out string canonicalText)
    {
        canonicalText = string.Empty;
        hash = string.Empty;
        reason = "semantic-il-unavailable";
        try
        {
            MethodBody? body = method.GetMethodBody();
            byte[]? il = body?.GetILAsByteArray();
            if (body is null || il is null)
                return false;

            var canonical = new StringBuilder(il.Length * 3);
            // Harmony sets NoInlining when it detours a method. It changes the
            // execution strategy, not loader instructions or synchronization.
            // Keep every other implementation flag in the semantic contract.
            canonical.Append("header:").Append((int)method.Attributes).Append(':')
                .Append((int)(method.GetMethodImplementationFlags() & ~MethodImplAttributes.NoInlining)).Append(':')
                .Append(body.InitLocals ? "1" : "0").Append(':').Append(body.MaxStackSize).Append(';');
            foreach (LocalVariableInfo local in body.LocalVariables)
                canonical.Append("local:").Append(local.LocalIndex).Append(':')
                    .Append(local.IsPinned ? "1" : "0").Append(':').Append(TypeName(local.LocalType)).Append(';');
            int index = 0;
            while (index < il.Length)
            {
                int instructionOffset = index;
                ushort value = il[index++];
                if (value == 0xfe)
                {
                    Require(il, index, 1);
                    value = unchecked((ushort)(0xfe00 | il[index++]));
                }
                if (!Codes.TryGetValue(value, out OpCode code))
                {
                    reason = "semantic-il-opcode-" + instructionOffset.ToString("x", CultureInfo.InvariantCulture);
                    return false;
                }

                canonical.Append(value.ToString("x4", CultureInfo.InvariantCulture)).Append(':');
                switch (code.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                        {
                            Require(il, index, 1);
                            int delta = unchecked((sbyte)il[index++]);
                            canonical.Append(index + delta);
                            break;
                        }
                    case OperandType.InlineBrTarget:
                        {
                            int delta = ReadInt32(il, ref index);
                            canonical.Append(index + delta);
                            break;
                        }
                    case OperandType.InlineSwitch:
                        {
                            int count = ReadInt32(il, ref index);
                            if (count < 0 || count > (il.Length - index) / 4)
                                throw new InvalidOperationException("Invalid switch table.");
                            int tableEnd = checked(index + count * 4);
                            for (int item = 0; item < count; item++)
                            {
                                int delta = ReadInt32(il, ref index);
                                canonical.Append(tableEnd + delta).Append(',');
                            }
                            break;
                        }
                    case OperandType.ShortInlineI:
                        Require(il, index, 1);
                        canonical.Append(il[index++].ToString("x2", CultureInfo.InvariantCulture));
                        break;
                    case OperandType.InlineI:
                        canonical.Append(ReadInt32(il, ref index).ToString(CultureInfo.InvariantCulture));
                        break;
                    case OperandType.InlineI8:
                        canonical.Append(ReadInt64(il, ref index).ToString(CultureInfo.InvariantCulture));
                        break;
                    case OperandType.ShortInlineR:
                        canonical.Append(ReadBytes(il, ref index, 4));
                        break;
                    case OperandType.InlineR:
                        canonical.Append(ReadBytes(il, ref index, 8));
                        break;
                    case OperandType.ShortInlineVar:
                        Require(il, index, 1);
                        canonical.Append(il[index++].ToString(CultureInfo.InvariantCulture));
                        break;
                    case OperandType.InlineVar:
                        canonical.Append(ReadUInt16(il, ref index).ToString(CultureInfo.InvariantCulture));
                        break;
                    case OperandType.InlineString:
                        {
                            int token = ReadInt32(il, ref index);
                            string text = method.Module.ResolveString(token);
                            canonical.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));
                            break;
                        }
                    case OperandType.InlineField:
                    case OperandType.InlineMethod:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                        {
                            int token = ReadInt32(il, ref index);
                            Type[]? typeArguments = method.DeclaringType?.GetGenericArguments();
                            Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
                            int table = (int)((uint)token >> 24);
                            // Mono's ResolveMember rejects some valid open generic
                            // type tokens. Resolve their declared metadata kind directly.
                            MemberInfo member = table == 0x01 || table == 0x02 || table == 0x1b
                                ? method.Module.ResolveType(token, typeArguments, methodArguments)
                                : method.Module.ResolveMember(token, typeArguments, methodArguments);
                            canonical.Append(Member(member));
                            break;
                        }
                    case OperandType.InlineSig:
                        reason = "semantic-il-inline-signature";
                        return false;
                    default:
                        reason = "semantic-il-operand-" + code.OperandType.ToString().ToLowerInvariant();
                        return false;
                }
                canonical.Append(';');
            }

            foreach (ExceptionHandlingClause clause in body.ExceptionHandlingClauses)
            {
                int filterOffset = clause.Flags == ExceptionHandlingClauseOptions.Filter ? clause.FilterOffset : -1;
                Type? catchType = clause.Flags == ExceptionHandlingClauseOptions.Clause ? clause.CatchType : null;
                canonical.Append("eh:").Append((int)clause.Flags).Append(':')
                    .Append(clause.TryOffset).Append(':').Append(clause.TryLength).Append(':')
                    .Append(clause.HandlerOffset).Append(':').Append(clause.HandlerLength).Append(':')
                    .Append(filterOffset).Append(':')
                    .Append(catchType is null ? "-" : TypeName(catchType)).Append(';');
            }

            canonicalText = canonical.ToString();
            using SHA256 algorithm = SHA256.Create();
            hash = BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonicalText)))
                .Replace("-", string.Empty);
            reason = "semantic-il-exact";
            return true;
        }
        catch (Exception exception)
        {
            reason = "semantic-il-exception-" + exception.GetType().Name.ToLowerInvariant();
            return false;
        }
    }

    private static string Member(MemberInfo member)
    {
        if (member is Type type)
            return "T|" + TypeName(type);
        if (member is FieldInfo field)
            return "F|" + TypeName(field.DeclaringType) + "|" + field.Name + "|" + ModifiedTypeName(field.FieldType, field.GetRequiredCustomModifiers(), field.GetOptionalCustomModifiers()) + "|" + (field.IsStatic ? "S" : "I");
        if (member is MethodBase method)
        {
            string result = method is MethodInfo info
                ? ModifiedTypeName(info.ReturnType, info.ReturnParameter.GetRequiredCustomModifiers(), info.ReturnParameter.GetOptionalCustomModifiers())
                : "void";
            string generic = method.IsGenericMethod
                ? string.Join(",", method.GetGenericArguments().Select(TypeName))
                : string.Empty;
            string parameters = string.Join(",", method.GetParameters().Select(parameter => ModifiedTypeName(parameter.ParameterType, parameter.GetRequiredCustomModifiers(), parameter.GetOptionalCustomModifiers())));
            return "M|" + TypeName(method.DeclaringType) + "|" + method.Name + "|" + result + "|"
                + (method.IsStatic ? "S" : "I") + "|" + generic + "|" + parameters;
        }
        throw new InvalidOperationException("Unsupported metadata member.");
    }

    private static string ModifiedTypeName(Type type, Type[] required, Type[] optional)
    {
        string result = TypeName(type);
        foreach (Type modifier in required)
            result += " modreq(" + TypeName(modifier) + ")";
        foreach (Type modifier in optional)
            result += " modopt(" + TypeName(modifier) + ")";
        return result;
    }

    private static string TypeName(Type? type)
    {
        if (type is null)
            return "<global>";
        if (type.IsGenericParameter)
            return (type.DeclaringMethod is null ? "!" : "!!") + type.GenericParameterPosition;
        if (type.IsByRef)
            return TypeName(type.GetElementType()) + "&";
        if (type.IsPointer)
            return TypeName(type.GetElementType()) + "*";
        if (type.IsArray)
            return TypeName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            return AssemblyName(definition) + ":" + definition.FullName + "<"
                + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
        }
        return AssemblyName(type) + ":" + (type.FullName ?? type.Name);
    }

    private static string AssemblyName(Type type) => type.Assembly.GetName().Name ?? string.Empty;

    private static int ReadInt32(byte[] source, ref int index)
    {
        Require(source, index, 4);
        int value = BitConverter.ToInt32(source, index);
        index += 4;
        return value;
    }

    private static long ReadInt64(byte[] source, ref int index)
    {
        Require(source, index, 8);
        long value = BitConverter.ToInt64(source, index);
        index += 8;
        return value;
    }

    private static ushort ReadUInt16(byte[] source, ref int index)
    {
        Require(source, index, 2);
        ushort value = BitConverter.ToUInt16(source, index);
        index += 2;
        return value;
    }

    private static string ReadBytes(byte[] source, ref int index, int count)
    {
        Require(source, index, count);
        string value = BitConverter.ToString(source, index, count).Replace("-", string.Empty);
        index += count;
        return value;
    }

    private static void Require(byte[] source, int index, int count)
    {
        if (index < 0 || count < 0 || index > source.Length - count)
            throw new InvalidOperationException("Truncated IL.");
    }
}
