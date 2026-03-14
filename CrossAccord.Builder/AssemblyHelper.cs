using Mono.Cecil;

namespace CrossAccord.Builder;

public static class AssemblyHelper
{
    private static DefaultAssemblyResolver? _resolver;
    
    public static void InitializeResolver(string assemblyParentPath, string[] extraPaths)
    {
        _resolver = new DefaultAssemblyResolver();

        _resolver.AddSearchDirectory(assemblyParentPath);
        foreach (var path in extraPaths)
        {
            _resolver.AddSearchDirectory(path);
        }
    }

    public static AssemblyDefinition ReadAssemblyInMemory(string path, bool readWrite = true)
    {
        if (_resolver is null)
            throw new Exception("AssemblyResolver is null, make sure to call AssemblyHelper.InitializeResolver");
        
        return AssemblyDefinition.ReadAssembly(path,
            new ReaderParameters { ReadWrite = readWrite, InMemory = true, AssemblyResolver = _resolver });
    }
}