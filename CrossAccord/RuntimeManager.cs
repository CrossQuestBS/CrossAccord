using System.Collections.Generic;
using System.Reflection;
using CrossAccord.Common.Interfaces;

namespace CrossAccord;

public static class RuntimeManager
{
    private static readonly Dictionary<MethodInfo, IAccordPatcher> Patchers = new ();
    
    public static void RegisterPatcher(IAccordPatcher patcher, MethodInfo methodInfo)
    {
        Patchers.TryAdd(methodInfo, patcher);
    }
    
    public static void Patch(IAccordPatch patch)
    {
        foreach (var (methodInfo, accordPatcher) in Patchers)
        {
            if (patch.Method.HasSameMetadataDefinitionAs(methodInfo))
            {
                accordPatcher.Patch(patch);
            }
        }
    }
    
    public static void Unpatch(IAccordPatch patch)
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