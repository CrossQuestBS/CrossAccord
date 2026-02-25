using System;
using System.IO;
using System.Linq;
using CrossAccord.Builder;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CrossAccord.Unity;

public class UnityGenerator : IPreprocessBuildWithReport
{

    public int callbackOrder => 1;
    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("Running OnPreprocessBuild");
        Debug.Log(Application.dataPath);
        var PluginsFolder = Path.Combine(Application.dataPath, "Plugins");


        var files = Directory.GetFiles(PluginsFolder, "*.dll", SearchOption.AllDirectories).Where(
            fileName => Path.GetExtension(fileName) == ".dll"
        ).ToList();

        var assemblyParentPath = Directory.GetParent(files.First(it => it.Contains("CrossAccord.dll"))).FullName;
        
        var patchers = AssemblyGenerator.GetAllPatchers(files.ToArray());
        
        SharedState.PatcherInfos = patchers;

        foreach (var patcher in patchers)
        {
            Debug.Log(patcher.MethodFullName);
            Debug.Log(patcher.GeneratedCode.GetText());
        }

        AssemblyGenerator.GeneratePatcherAssembly(patchers, files.ToArray(), assemblyParentPath);
    }
}