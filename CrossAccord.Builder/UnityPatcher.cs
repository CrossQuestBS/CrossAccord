using IPA.BuildProcess.Interfaces;

namespace CrossAccord.Builder;

public class UnityPatcher : IPostLinkerBuild
{
    public int executeOrder => 1;
    public void Execute(List<string> files)
    {
        var assemblies = files.Where(it => it.EndsWith(".dll")).ToArray();

        var assemblyParentPath = Directory.GetParent(assemblies.First())!.FullName;
        
        AssemblyPatcher.PatchAll(SharedState.PatcherInfos, Path.Join(assemblyParentPath, "CrossAccord.Generated.dll"), assemblyParentPath);
    }
}