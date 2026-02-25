using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CrossAccord.Builder;

public static class AssemblyPatcher
{
    public static void AddPatcher(MethodDefinition patcherInstance, MethodDefinition prefix, MethodDefinition postfix, MethodDefinition originalMethod)
    {
        var ilProcessor = originalMethod.Body.GetILProcessor();

        Dictionary<string, Instruction> nopInstructions = new();

        var startInstruction = originalMethod.Body.Instructions[0];
        var lastInstruction = originalMethod.Body.Instructions.Last();

        var nopStart = ilProcessor.Create(OpCodes.Nop);
        
        nopInstructions["StartOriginal"] = nopStart;
        
        ilProcessor.InsertBefore(startInstruction, nopStart);
        
        var nopEnd = ilProcessor.Create(OpCodes.Nop);
        nopInstructions["EndOriginal"] = nopEnd;
        ilProcessor.InsertAfter(lastInstruction, nopEnd);
        
        VariableDefinition? returnValue = null;
        if (originalMethod.ReturnType.MetadataType != MetadataType.Void)
        {
            returnValue = new VariableDefinition(originalMethod.ReturnType);
            originalMethod.Body.Variables.Add(returnValue);
        }
        
        
        var parameters = originalMethod.Parameters.Count;

        var methodInstanceRef = originalMethod.Module.ImportReference(patcherInstance);
        
        ilProcessor.InsertBefore(nopInstructions["StartOriginal"], ilProcessor.Create(OpCodes.Call, methodInstanceRef));
        
        if (originalMethod.HasThis)
        {
            ilProcessor.InsertBefore(nopInstructions["StartOriginal"], ilProcessor.Create(OpCodes.Ldarg, 0));
        }
        
        for (int i = 0; i < parameters; i++)
        {
            ilProcessor.InsertBefore(nopInstructions["StartOriginal"],
                originalMethod.Parameters[i].ParameterType.IsByReference
                    ? ilProcessor.Create(OpCodes.Ldarg, i + (originalMethod.HasThis ? 1 : 0))
                    : ilProcessor.Create(OpCodes.Ldarga, i + (originalMethod.HasThis ? 1 : 0)));
        }

        if (returnValue != null)
        {
            ilProcessor.InsertBefore(nopInstructions["StartOriginal"], ilProcessor.Create(OpCodes.Ldloca, returnValue));
        }
        
        var prefixRef = originalMethod.Module.ImportReference(prefix);

        ilProcessor.InsertBefore(nopInstructions["StartOriginal"], ilProcessor.Create(OpCodes.Callvirt, prefixRef));
        
        ilProcessor.InsertBefore(nopInstructions["StartOriginal"], ilProcessor.Create(OpCodes.Brfalse, nopInstructions["EndOriginal"]));
        
        if (originalMethod.ReturnType.MetadataType != MetadataType.Void)
        {
            ilProcessor.InsertBefore(nopInstructions["EndOriginal"], ilProcessor.Create(OpCodes.Stloc, returnValue));
        }
        
        if (lastInstruction.OpCode == OpCodes.Ret)
        {
            ilProcessor.Remove(lastInstruction);
        }

        var nopPostfixEnd = ilProcessor.Create(OpCodes.Nop);
        nopInstructions["PostfixEnd"] = nopPostfixEnd;
        ilProcessor.InsertAfter(nopEnd, nopPostfixEnd);
        // Handle calling original
        
        ilProcessor.InsertBefore(nopInstructions["PostfixEnd"], ilProcessor.Create(OpCodes.Call, methodInstanceRef));


        if (originalMethod.HasThis)
        {
            
            ilProcessor.InsertBefore(nopInstructions["PostfixEnd"], ilProcessor.Create(OpCodes.Ldarg, 0));
        }
        
        for (int i = 0; i < parameters; i++)
        {
            ilProcessor.InsertBefore(nopInstructions["PostfixEnd"],
                originalMethod.Parameters[i].ParameterType.IsByReference
                    ? ilProcessor.Create(OpCodes.Ldarg, i + (originalMethod.HasThis ? 1 : 0))
                    : ilProcessor.Create(OpCodes.Ldarga, i + (originalMethod.HasThis ? 1 : 0)));
        }

        if (returnValue != null)
        {
            ilProcessor.InsertBefore(nopInstructions["PostfixEnd"], ilProcessor.Create(OpCodes.Ldloca, returnValue));
        }

        var postfixRef = originalMethod.Module.ImportReference(postfix);

        ilProcessor.InsertBefore(nopInstructions["PostfixEnd"], ilProcessor.Create(OpCodes.Callvirt, postfixRef));

        if (originalMethod.ReturnType.MetadataType != MetadataType.Void)
        {
            ilProcessor.InsertBefore(nopInstructions["PostfixEnd"], ilProcessor.Create(OpCodes.Ldloc, returnValue));
        }
        
        ilProcessor.InsertAfter(nopInstructions["PostfixEnd"], ilProcessor.Create(OpCodes.Ret));
    }


    public static MethodDefinition? FindOriginalMethod(PatcherInfo info, AssemblyDefinition assemblyDefinition)
    {
        var method = assemblyDefinition.MainModule.Types.SelectMany(it => it.Methods).First(it => it.FullName == info.MethodFullName);

        if (method is null)
            return null;


        return method;
    }

    public static TypeDefinition? GetGeneratedPatcher(PatcherInfo info, AssemblyDefinition generatedAssembly)
    {
        return generatedAssembly.MainModule.Types.First(it => it.Name.EndsWith(info.Guid.ToClassSafeString()));
    }

    public static void PatchAssembly(PatcherInfo[] patcherList, AssemblyDefinition assemblyToPatch,
        AssemblyDefinition generatedAssembly)
    {
        foreach (var patcherInfo in patcherList)
        {
            var patcherType = GetGeneratedPatcher(patcherInfo, generatedAssembly);
            
            if (patcherType is null)
                continue;

            var instance = patcherType.Methods.First(it => it.Name == "get_Instance");
            var prefix = patcherType.Methods.First(it => it.Name == "Prefix");
            var postfix = patcherType.Methods.First(it => it.Name == "Postfix");

            var originalMethod = FindOriginalMethod(patcherInfo, assemblyToPatch);
            
            if (originalMethod is null)
                continue;

            AddPatcher(instance, prefix, postfix, originalMethod);
        }
    }

    public static void PatchAll(PatcherInfo[] patchers, string generatedPath, string assemblyParentPath)
    {
        var patcherGroupedByAssemblyPath = patchers.GroupBy(it => it.AssemblyName);

        foreach (var grouping in patcherGroupedByAssemblyPath)
        {
            AssemblyHelper.InitializeResolver(assemblyParentPath, Array.Empty<string>());

            using var assembly = AssemblyHelper.ReadAssemblyInMemory(Path.Join(assemblyParentPath, grouping.Key));
            using var generatedAssembly = AssemblyHelper.ReadAssemblyInMemory(generatedPath);
            
            PatchAssembly(grouping.ToArray(), assembly, generatedAssembly);
            assembly.Write(Path.Join(assemblyParentPath, grouping.Key));
        }
    }
    

    /*private void ApplyPostFix(MethodDefinition method, MethodDefinition patchMethod)
    {
        var ilProcessor = method.Body.GetILProcessor();
        var reference = method.Module.ImportReference(patchMethod);

        ilProcessor.Remove(method.Body.Instructions.Last());

        for (var i = 0; i < patchMethod.Parameters.Count; i++)
        {
            ilProcessor.Emit(OpCodes.Ldarg, i);
        }

        ilProcessor.Emit(OpCodes.Call, reference);
        ilProcessor.Emit(OpCodes.Ret);
    }

    private VariableDefinition CreateLocalResultVariable(MethodDefinition method, ILProcessor ilProcessor,
        Instruction instruction)
    {
        VariableDefinition localResultVariable = new VariableDefinition(method.ReturnType);
        method.Body.Variables.Add(localResultVariable);
        if (!method.ReturnType.IsByReference) return localResultVariable;

        Instruction[] instructions =
        {
            ilProcessor.Create(OpCodes.Ldc_I4_1),
            ilProcessor.Create(OpCodes.Newarr, method.ReturnType.GetElementType()),
            ilProcessor.Create(OpCodes.Ldc_I4_0),
            ilProcessor.Create(OpCodes.Ldelem_Ref, method.ReturnType.GetElementType()),
            ilProcessor.Create(OpCodes.Stloc, localResultVariable),
        };

        foreach (var newInstruction in instructions)
        {
            ilProcessor.InsertBefore(instruction, newInstruction);
        }

        return localResultVariable;
    }

    private void InsertPrefixPatchStack(MethodDefinition patchMethod, ILProcessor ilProcessor,
        Instruction instruction, bool hasReturnParameter, VariableDefinition? localResultVariable)
    {
        var parameters = patchMethod.Parameters.ToArray().Where(it => !it.Name.Contains("__result")).ToArray();

        for (var i = 0; i < parameters.Length; i++)
        {
            ilProcessor.InsertBefore(instruction,
                ilProcessor.Create(OpCodes.Ldarg, i));
        }

        if (hasReturnParameter)
            ilProcessor.InsertBefore(instruction,
                ilProcessor.Create(OpCodes.Ldloca, localResultVariable));
    }


    private static void HandlePrefixReturn(ILProcessor ilProcessor, Instruction instruction,
        bool hasReturnParameter,
        VariableDefinition? localResultVariable)
    {
        ilProcessor.InsertBefore(instruction, ilProcessor.Create(OpCodes.Brtrue, instruction));

        if (hasReturnParameter)
        {
            ilProcessor.InsertBefore(instruction, ilProcessor.Create(OpCodes.Ldloc, localResultVariable));
        }

        ilProcessor.InsertBefore(instruction, ilProcessor.Create(OpCodes.Ret));
    }

    private void ApplyPrefix(MethodDefinition method, MethodDefinition patchMethod)
    {
        var ilProcessor = method.Body.GetILProcessor();
        var reference = method.Module.ImportReference(patchMethod);

        var instruction = method.Body.Instructions[0];

        var hasReturnParameter =
            patchMethod.Parameters.Any(it => it.Name == "__result" && it.ParameterType.IsByReference);

        var localResultVariable =
            hasReturnParameter ? CreateLocalResultVariable(method, ilProcessor, instruction) : null;

        var hasBoolReturn = patchMethod.ReturnType.Name.ToLower().StartsWith("bool");

        InsertPrefixPatchStack(patchMethod, ilProcessor, instruction, hasReturnParameter, localResultVariable);

        ilProcessor.InsertBefore(instruction, ilProcessor.Create(OpCodes.Call, reference));

        if (hasBoolReturn)
            HandlePrefixReturn(ilProcessor, instruction, hasReturnParameter, localResultVariable);
    }*/
}