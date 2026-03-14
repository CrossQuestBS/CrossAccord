using System.Reflection;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Mono.Cecil;

namespace CrossAccord.Builder;

public static class AssemblyGenerator
{
    public static PatcherInfo[] GetAllPatchers(string[] assemblies)
    {
        List<PatcherInfo> output = new();
        var extraPaths = assemblies.Select(it => Directory.GetParent(it).FullName);

        foreach (var assemblyPath in assemblies)
        {
            var assemblyParentPath = Directory.GetParent(assemblyPath).FullName;
            
            AssemblyHelper.InitializeResolver(assemblyParentPath, extraPaths.ToArray());

            using var assembly = AssemblyHelper.ReadAssemblyInMemory(assemblyPath, false);

            var patches = GetPatchesFromAssembly(assembly);
            
            output.AddRange(patches);
        }
        
        return output.GroupBy(it => $"{it.AssemblyName}_{it.MethodFullName}").Select(x => x.First()).ToArray();;
    }
    
    public static PatcherInfo[] GetPatchesFromAssembly(AssemblyDefinition assemblyDefinition)
    {
        List<PatcherInfo> output = new();
        
        var crossPatchTypes = assemblyDefinition.MainModule.Types.Where(it =>
            it.HasCustomAttributes && 
            it.CustomAttributes.Any(it => it.AttributeType.Name.Contains("AccordPatchAttribute"))).ToArray();

        foreach (var patchType in crossPatchTypes)
        {
            var attribute = patchType.CustomAttributes.First(it => it.AttributeType.Name == "AccordPatchAttribute");
            var patchClassType = (TypeReference)attribute.ConstructorArguments[0].Value;
            var patchMethod = (string)attribute.ConstructorArguments[1].Value;
            var properType = patchClassType.Resolve();

            var methodDefinition = properType.Methods.First(it =>
                it.Name == patchMethod && it.DeclaringType.FullName == patchClassType.FullName);
            
            var guid = Guid.NewGuid();
            var code = GetCode(methodDefinition, guid);

            var patchInfo = new PatcherInfo(methodDefinition.Module.Name, methodDefinition.FullName, code, guid);
            
            output.Add(patchInfo);
        }

        return output.ToArray();
    }
    

    public static void GeneratePatcherAssembly(PatcherInfo[] allPatchers, string[] assemblies, string libraryPath)
    {
        var patchers = allPatchers;
        
        List<MetadataReference> metadataReferences = new();
        
        metadataReferences.AddRange(NetStandard21.References.All);

        foreach (var assemblyPath in assemblies)
        {
            metadataReferences.Add(MetadataReference.CreateFromFile(assemblyPath));
        }
        
        CSharpCompilation compilation = CSharpCompilation.Create(
            "CrossAccord.Generated",
            syntaxTrees: patchers.Select(it => it.GeneratedCode),
            references: metadataReferences.ToArray(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        // first compile
        EmitResult result = compilation.Emit(ms);

        // if failed, get error message
        if (!result.Success)
        {
            IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError ||
                diagnostic.Severity == DiagnosticSeverity.Error);
            
            foreach (Diagnostic diagnostic in failures)
            {
                throw new Exception(string.Format("Failed to compile code '{0}'! {1}: {2}", "Uhm?", diagnostic.Id,
                    diagnostic.GetMessage()));
            }

            throw new Exception("Unknown error while compiling code '" + "Uhm?" + "'!");
        }
        // on success, load into assembly

        // load assembly and add to cache
        ms.Seek(0, SeekOrigin.Begin);

        var fileStream = File.Create(Path.Join(libraryPath , "CrossAccord.Generated.dll"));

        ms.CopyTo(fileStream);
        fileStream.Close();
    }
    
    public static SyntaxTree GetCode(MethodDefinition methodDefinition, Guid guid)
    {
        var fullClassName = methodDefinition.DeclaringType.FullName;
        var methodName = methodDefinition.Name;
        var generatedClassName = $"{methodName}Patcher_{guid.ToClassSafeString()}".Replace(".ctor", "Constructor");

        var parameters = "";

        List<string> totalParameters = new();
        List<string> simpleParameters = new();

        var parameterSimpleValue = "";

        if (!methodDefinition.IsStatic)
        {
            totalParameters.Add($"global::{fullClassName} instance");
            simpleParameters.Add("instance");
        }
        
        if (methodDefinition.Parameters.Count > 0)
        {
            
            simpleParameters.AddRange(methodDefinition.Parameters.Select((it, idx) => $"{(it.IsIn ? "" : "ref")} arg{idx + 1}").ToArray());
            totalParameters.AddRange( methodDefinition.Parameters.Select((it, idx) => $"{(it.IsIn ? "in" : "ref")} global::{it.ParameterType.FullName.Replace("&", "").Replace("modreq(System.Runtime.InteropServices.InAttribute)", "")} arg{idx+1}").ToArray());
        }

        if (methodDefinition.ReturnType.Name != "Void")
        {
            totalParameters.Add($"ref global::{methodDefinition.ReturnType.FullName} returnValue");
            simpleParameters.Add("ref returnValue");
        }

        parameters = string.Join(", ", totalParameters);
        parameterSimpleValue = string.Join(", ", simpleParameters);

        return CSharpSyntaxTree.ParseText($@"using System;
using System.Collections.Generic;
using System.Reflection;
using CrossAccord.Common.Interfaces;
using CrossAccord.Common;

namespace CrossAccord.Generated.{fullClassName}.{methodName.Replace(".", "Dot")};


public class {generatedClassName} : IAccordPatcher
{{
    public static MemberInfo OriginalMemberInfo {{ get; }} =
        typeof(global::{fullClassName}).GetMember(""{methodName}"", (global::System.Reflection.BindingFlags)~0)[0]!;

    private delegate bool PrefixDelegate({parameters});

    private delegate void PostfixDelegate({parameters});

    private static readonly Dictionary<IAccordPatch, PostfixDelegate> PostfixDict = new();
    private static readonly Dictionary<IAccordPatch, PrefixDelegate> PrefixDict = new();

    public static {generatedClassName} Instance {{ get; }} = new();

    private {generatedClassName}()
    {{
    }}

    public void Patch(IAccordPatch instance)
    {{
        var prefixMethodInfo = instance.GetPatchMethodInfo(""Prefix"");

        if (prefixMethodInfo is not null && prefixMethodInfo.ValidatePatch(OriginalMemberInfo))
        {{
            PrefixDict.Add(instance, (PrefixDelegate)Delegate.CreateDelegate(typeof(PrefixDelegate), instance, prefixMethodInfo));
        }}

        var postfixMethodInfo = instance.GetPatchMethodInfo(""Postfix"");

        if (postfixMethodInfo is not null && postfixMethodInfo.ValidatePatch(OriginalMemberInfo))
        {{
            PostfixDict.Add(instance, (PostfixDelegate)Delegate.CreateDelegate(typeof(PostfixDelegate), instance, postfixMethodInfo));
        }}
    }}

    public void Unpatch(IAccordPatch instance)
    {{
        PrefixDict.Remove(instance);
        PostfixDict.Remove(instance);
    }}

    public bool Prefix({parameters})
    {{
        foreach (var keyValue in PrefixDict)
        {{
            try
            {{
                if (!keyValue.Value({parameterSimpleValue}))
                    return false;
            }}
            catch (Exception e)
            {{
                Unpatch(keyValue.Key);
            }}
        }}

        return true;
    }}

    public void Postfix({parameters})
    {{
        foreach (var keyValue in PostfixDict)
        {{
            try
            {{
                keyValue.Value({parameterSimpleValue});
            }}
            catch (Exception e)
            {{
                Unpatch(keyValue.Key);
            }}
        }}
    }}
}}");
    }
}
