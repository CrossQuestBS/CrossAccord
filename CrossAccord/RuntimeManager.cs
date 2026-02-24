using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrossAccord.Common.Interfaces;

namespace CrossAccord;

public class RuntimeManager
{
    private readonly Dictionary<MethodInfo, IAccordPatcher> Patchers = new ();

    public static RuntimeManager Instance { get; } = new();
    
    private RuntimeManager()
    {
        var interfacePatcher = typeof(IAccordPatcher);

        var generatedAssembly = Assembly.Load("CrossAccord.Generated, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
        
        var types = generatedAssembly.GetTypes()
            .Where(p => interfacePatcher.IsAssignableFrom(p));

        foreach (var patcherType  in types)
        {
            var methodInfo = patcherType.GetMethod("get_Instance");
            object[] array = [];
            if (methodInfo == null) continue;
            var instance = (IAccordPatcher)methodInfo.Invoke(null, array);
            var methodInfo2 = patcherType.GetMethod("get_OriginalMethod");
            if (methodInfo2 == null) continue;
            var originalMethod = methodInfo2.Invoke(null, array);
            Patchers.TryAdd((MethodInfo)originalMethod, instance);
        }
    }
    
    public void RegisterPatcher(IAccordPatcher patcher, MethodInfo methodInfo)
    {
        Patchers.TryAdd(methodInfo, patcher);
    }
    
    public void Patch(IAccordPatch patch)
    {
        foreach (var (methodInfo, accordPatcher) in Patchers)
        {
            if (patch.Method.HasSameMetadataDefinitionAs(methodInfo))
            {
                accordPatcher.Patch(patch);
            }
        }
    }
    
    public void Unpatch(IAccordPatch patch)
    {
        foreach (var (methodInfo, accordPatcher) in Patchers)
        {
            if (patch.Method.HasSameMetadataDefinitionAs(methodInfo))
            {
                accordPatcher.Unpatch(patch);
            }
        }
    }
}