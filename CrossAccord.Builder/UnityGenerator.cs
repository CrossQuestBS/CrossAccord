using IPA.BuildProcess.Interfaces;

namespace CrossAccord.Builder;

public class UnityGenerator : IPreLinkerBuild
{
    public int executeOrder => 1;

    public void Execute(List<string> files)
    {
        var crossAccordPath = files.First(it => it.EndsWith("CrossAccord.dll"));

        var libDirectory = Path.GetDirectoryName(crossAccordPath);

        if (libDirectory is null)
            throw new DirectoryNotFoundException($"Did not find library directory from path {crossAccordPath}");

        var gameDirectory = Path.GetDirectoryName(libDirectory);

        if (gameDirectory is null)
            throw new DirectoryNotFoundException($"Did not find game directory from path {crossAccordPath}");

        var allGameAssemblies = Directory.GetFiles(gameDirectory, "*.dll", SearchOption.AllDirectories)
            .Where(fileName => fileName.EndsWith(".dll")).ToList();

        var patchers = AssemblyGenerator.GetAllPatchers(allGameAssemblies.ToArray());

        SharedState.PatcherInfos = patchers;

        AssemblyGenerator.GeneratePatcherAssembly(patchers, allGameAssemblies.ToArray(), libDirectory);
    }
}