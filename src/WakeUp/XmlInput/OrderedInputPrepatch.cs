// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace WakeUp;

// This is part of the coordinated parsed-XML rewrite, after its original-body
// check. Selection stays native IL: folder precedence, enumeration and dictionary
// winners are not reconstructed by a second filesystem implementation.
internal static class OrderedInputPrepatch
{
    internal const string SelectorName = "WakeUpSelectXmlFiles";

    internal static MethodDefinition Inject(ModuleDefinition module, MethodDefinition files, Instruction selectedCall)
    {
        if (files.Body.ExceptionHandlers.Count != 0 || files.DeclaringType.Methods.Any(m => m.Name == SelectorName))
            throw new InvalidOperationException("unsupported native XML selection body");
        var selectedStore = selectedCall.Next;
        var original = files.Body.Instructions.TakeWhile(i => i != selectedStore).ToArray();
        var selectedType = files.Body.Variables.Single(v => v.VariableType.FullName == "System.Collections.Generic.List`1<System.IO.FileInfo>").VariableType;
        var selector = new MethodDefinition(SelectorName, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, selectedType);
        foreach (var parameter in files.Parameters)
        {
            var copy = new ParameterDefinition(parameter.Name, parameter.Attributes, parameter.ParameterType);
            if (parameter.HasConstant) copy.Constant = parameter.Constant;
            selector.Parameters.Add(copy);
        }
        selector.Body.InitLocals = files.Body.InitLocals;
        selector.Body.MaxStackSize = files.Body.MaxStackSize;
        var variables = new Dictionary<VariableDefinition, VariableDefinition>();
        foreach (var variable in files.Body.Variables)
        {
            var copy = new VariableDefinition(variable.VariableType);
            variables.Add(variable, copy); selector.Body.Variables.Add(copy);
        }
        var instructions = original.ToDictionary(i => i, _ => Instruction.Create(OpCodes.Nop));
        var emptyConstructor = new MethodReference(".ctor", module.TypeSystem.Void, selectedType) { HasThis = true };
        foreach (var instruction in original)
        {
            var copy = instructions[instruction];
            copy.OpCode = instruction.OpCode;
            copy.Operand = instruction.Operand switch {
                Instruction target => instructions[target],
                Instruction[] targets => targets.Select(t => instructions[t]).ToArray(),
                VariableDefinition variable => variables[variable],
                ParameterDefinition parameter => selector.Parameters[parameter.Index],
                _ => instruction.Operand
            };
            // The original no-files result is an asset array. The factored
            // selector returns an empty file list; the caller retains that
            // original shared empty asset array before entering any consumer.
            if (instruction.OpCode == OpCodes.Ldsfld && instruction.Operand is FieldReference field && field.Name == "EmptyXmlAssetsArray")
            { copy.OpCode = OpCodes.Newobj; copy.Operand = emptyConstructor; }
            selector.Body.Instructions.Add(copy);
        }
        selector.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

        // Keep the native display-class prologue used by its worker remainder.
        // The selector clone also retains its own original prologue and locals.
        var selectionStart = original.Single(i => i.OpCode == OpCodes.Ldarg_2);
        var emptyField = original.Single(i => i.OpCode == OpCodes.Ldsfld && i.Operand is FieldReference field && field.Name == "EmptyXmlAssetsArray").Operand;
        var countMethod = files.Body.Instructions.SkipWhile(i => i != selectedStore).Select(i => i.Operand).OfType<MethodReference>()
            .First(m => m.Name == "get_Count" && m.DeclaringType.FullName == selectedType.FullName);
        var bridge = ParsedXmlPrepatch.Bridge(module, "OrderedInputRuntime", "SelectFiles", selectedType,
            files.Parameters.Select(p => p.ParameterType).ToArray());
        var il = files.Body.GetILProcessor();
        foreach (var instruction in new[] {
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldarg_2),
            Instruction.Create(OpCodes.Call, bridge), Instruction.Create(OpCodes.Dup), Instruction.Create(OpCodes.Callvirt, countMethod),
            Instruction.Create(OpCodes.Brtrue, selectedStore), Instruction.Create(OpCodes.Pop),
            Instruction.Create(OpCodes.Ldsfld, (FieldReference)emptyField), Instruction.Create(OpCodes.Ret)
        }) il.InsertBefore(selectionStart, instruction);
        foreach (var instruction in original.SkipWhile(i => i != selectionStart)) il.Remove(instruction);
        files.DeclaringType.Methods.Add(selector);
        return selector;
    }
}
