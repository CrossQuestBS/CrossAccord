using Mono.Cecil;

namespace CrossAccord.Builder;

public static class AssemblyHelper
{
    private static DefaultAssemblyResolver _resolver;
    
    public static void InitializeResolver(string assemblyParentPath)
    {
        _resolver = new DefaultAssemblyResolver();

        _resolver.AddSearchDirectory(assemblyParentPath);
    }

    public static AssemblyDefinition ReadAssemblyInMemory(string path, bool readWrite = true)
    {
        return AssemblyDefinition.ReadAssembly(path,
            new ReaderParameters { ReadWrite = readWrite, InMemory = true, AssemblyResolver = _resolver });
    }
}