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
}