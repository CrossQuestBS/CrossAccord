using System;
using System.IO;
using System.Linq;
using CrossAccord.Builder;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CrossAccord.Unity;

public class UnityPatcher : IPostBuildPlayerScriptDLLs
{
    public int callbackOrder => 1;

    public void OnPostBuildPlayerScriptDLLs(BuildReport report)
    {
        var assemblyPath = report.GetFiles();
        var assemblies = assemblyPath.Select(it => it.path).Where(it => it.EndsWith(".dll")).ToArray();

        var assemblyParentPath = Directory.GetParent(assemblies.First()).FullName;
        
        AssemblyPatcher.PatchAll(SharedState.PatcherInfos, Path.Join(assemblyParentPath, "CrossAccord.Generated.dll"), assemblyParentPath);
    }
}