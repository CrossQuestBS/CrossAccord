using Mono.Cecil;

namespace CrossAccord.Builder;

public class Patcher
{
    private DefaultAssemblyResolver _resolver;
    
    public void InitializeResolver(string assemblyParentPath)
    {
        _resolver = new DefaultAssemblyResolver();

        _resolver.AddSearchDirectory(assemblyParentPath);
    }

    private AssemblyDefinition ReadAssembly(string path)
    {
        return AssemblyDefinition.ReadAssembly(path,
            new ReaderParameters { ReadWrite = true, InMemory = true, AssemblyResolver = _resolver });
    }

    public void TryPatchAssembly(string path)
    {
        var assemblyParentPath = Directory.GetParent(path).Parent.FullName;
        InitializeResolver(assemblyParentPath);
    } 
}