using IPA.BuildProcess.Interfaces;

namespace CrossAccord.Builder;

public class UnityGenerator : IPreLinkerBuild
{
    
    public int executeOrder => 1;
    public void Execute(List<string> files)
    {
        var libs = files.First(it => it.Contains("CrossAccord.dll"));
        var mods = files.First(it => it.Contains("/Mods/"));

        var libFiles = Directory.GetFiles(Directory.GetParent(libs).Parent.FullName, "*.dll", SearchOption.AllDirectories).Where(
            fileName => Path.GetExtension(fileName) == ".dll"
        ).ToList();
        
        var modFiles = Directory.GetFiles(Directory.GetParent(mods).FullName, "*.dll", SearchOption.AllDirectories).Where(
            fileName => Path.GetExtension(fileName) == ".dll"
        ).ToList();

        var assemblyParentPath = Directory.GetParent(libFiles.First(it => it.Contains("CrossAccord.dll"))).FullName;

        var allFiles = new List<string>();
        
        allFiles.AddRange(libFiles);
        allFiles.AddRange(modFiles);
        
        var patchers = AssemblyGenerator.GetAllPatchers(allFiles.ToArray());
        
        SharedState.PatcherInfos = patchers;

        AssemblyGenerator.GeneratePatcherAssembly(patchers, allFiles.ToArray(), assemblyParentPath);
    }
}