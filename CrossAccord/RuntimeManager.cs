using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrossAccord.Common.Interfaces;

namespace CrossAccord;

public class RuntimeManager
{
    private readonly Dictionary<MemberInfo, IAccordPatcher> Patchers = new ();

    public static RuntimeManager Instance { get; } = new();
    
    private RuntimeManager()
    {
        var interfacePatcher = typeof(IAccordPatcher);

        var generatedAssembly = Assembly.Load("CrossAccord.Generated, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
        
        var types = generatedAssembly.GetTypes()
            .Where(p => interfacePatcher.IsAssignableFrom(p));

        foreach (var patcherType in types)
        {
            var getInstance = patcherType.GetMethod("get_Instance", (global::System.Reflection.BindingFlags)~0);
            object[] array = [];
            
            if (getInstance == null) 
                continue;
            
            var patcherInstance = (IAccordPatcher)getInstance.Invoke(null, array);
            var getOriginalMemberInfo = patcherType.GetMethod("get_OriginalMemberInfo", (global::System.Reflection.BindingFlags)~0);
            
            if (getOriginalMemberInfo == null) 
                continue;
            
            var originalMethod = getOriginalMemberInfo.Invoke(null, array);
            
            if (originalMethod is not null && patcherInstance is not null)
                Patchers.TryAdd((MemberInfo)originalMethod, patcherInstance);
        }
        
    }
    
    public void RegisterPatcher(IAccordPatcher patcher, MethodInfo methodInfo)
    {
        Patchers.TryAdd(methodInfo, patcher);
    }
    
    public void Patch(IAccordPatch patch)
    {
        foreach (var (memberInfo, accordPatcher) in Patchers)
        {
            if (patch.MemberMethod is null)
                throw new NullReferenceException($"{patch.GetType().FullName} has empty Method!");
            
            if (patch.MemberMethod.HasSameMetadataDefinitionAs(memberInfo))
            {
                accordPatcher.Patch(patch);
            }
        }
    }
    
    public void Unpatch(IAccordPatch patch)
    {
        foreach (var (memberInfo, accordPatcher) in Patchers)
        {
            if (patch.MemberMethod is null)
                throw new NullReferenceException($"{patch.GetType().FullName} has empty Method!");
            
            if (patch.MemberMethod.HasSameMetadataDefinitionAs(memberInfo))
            {
                accordPatcher.Unpatch(patch);
            }
        }
    }
}