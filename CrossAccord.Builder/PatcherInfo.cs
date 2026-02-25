using Microsoft.CodeAnalysis;

namespace CrossAccord.Builder;

public class PatcherInfo
{
    public PatcherInfo(string assemblyName, string methodFullName, SyntaxTree generatedCode, Guid guid)
    {
        AssemblyName = assemblyName;
        MethodFullName = methodFullName;
        GeneratedCode = generatedCode;
        Guid = guid;
    }

    public string AssemblyName { get; }
    public string MethodFullName { get; }
    
    public SyntaxTree GeneratedCode { get; }
    
    public Guid Guid { get; }
}