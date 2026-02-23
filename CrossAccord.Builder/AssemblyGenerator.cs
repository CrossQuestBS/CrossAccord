using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;

namespace CrossAccord.Builder;

public static class AssemblyGenerator
{
    private static DefaultAssemblyResolver _resolver;
    
    public static void InitializeResolver(string assemblyParentPath)
    {
        _resolver = new DefaultAssemblyResolver();

        _resolver.AddSearchDirectory(assemblyParentPath);
    }

    private static AssemblyDefinition ReadAssembly(string path)
    {
        return AssemblyDefinition.ReadAssembly(path,
            new ReaderParameters { ReadWrite = true, InMemory = true, AssemblyResolver = _resolver });
    }
    
    public static Dictionary<string, SyntaxTree> GetAllPatchers(string[] assemblies)
    {
        Dictionary<string, SyntaxTree> output = new();
        
        foreach (var assemblyPath in assemblies)
        {
            var assemblyParentPath = Directory.GetParent(assemblyPath).FullName;
            Console.WriteLine(assemblyParentPath);
            
            InitializeResolver(assemblyParentPath);

            using var assembly = ReadAssembly(assemblyPath);

            var patches = GetPatchesFromAssembly(assembly);

            foreach (var (key, value) in patches)
            {
                output.TryAdd(key, value);
            }
        }

        return output;
    }
    
    public static Dictionary<string,SyntaxTree> GetPatchesFromAssembly(AssemblyDefinition assemblyDefinition)
    {
        Dictionary<string, SyntaxTree> output = new();
        
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

            var key = methodDefinition.FullName;
            var code = GetCode(methodDefinition);
            output.Add(key, code);
        }

        return output;
    }
    
    


    public static SyntaxTree GetCode(MethodDefinition methodDefinition)
    {
        var className = methodDefinition.DeclaringType.Name;
        var fullClassName = methodDefinition.DeclaringType.FullName;
        var methodName = methodDefinition.Name;

        var parameters = "";

        if (methodDefinition.IsStatic)
            throw new NotImplementedException();

        var parameterSimpleValue = "instance";

        parameters += $"global::{fullClassName} instance";

        if (methodDefinition.Parameters.Count > 0)
        {
            parameterSimpleValue += ", ";
            parameterSimpleValue += string.Join(", ", methodDefinition.Parameters.Select((it, idx) => $"ref arg{idx + 1}"));
            parameters += ", ";
            parameters += string.Join(", ", methodDefinition.Parameters.Select((it, idx) => $"ref global::{it.ParameterType.FullName} arg{idx+1}"));
        }

        if (methodDefinition.ReturnType.Name != "Void")
        {
            parameters += $", ref global::{methodDefinition.ReturnType.FullName} returnValue";
            parameterSimpleValue += ", ref returnValue";
        }
        
        return CSharpSyntaxTree.ParseText($@"using System;
using System.Collections.Generic;
using System.Reflection;
using CrossAccord.Common.Interfaces;
using CrossAccord.Common;

namespace CrossAccord.Generated.{fullClassName}.{methodName};

public class {methodName}Patcher : IAccordPatcher
{{
    private static readonly MethodInfo OriginalMethod =
        typeof(global::{fullClassName}).GetMethod(""{methodName}"")!;

    private delegate bool PrefixDelegate({parameters});

    private delegate void PostfixDelegate({parameters});

    private static readonly Dictionary<IAccordPatch, PostfixDelegate> PostfixDict = new();
    private static readonly Dictionary<IAccordPatch, PrefixDelegate> PrefixDict = new();

    public static {methodName}Patcher Instance {{ get; }} = new();

    private {methodName}Patcher()
    {{
        global::CrossAccord.RuntimeManager.RegisterPatcher(this, OriginalMethod);
    }}

    public void Patch(IAccordPatch instance)
    {{
        var prefixMethodInfo = instance.GetPatchMethodInfo(""Prefix"");

        if (prefixMethodInfo is not null && prefixMethodInfo.ValidatePatch(OriginalMethod))
        {{
            PrefixDict.Add(instance, (PrefixDelegate)Delegate.CreateDelegate(typeof(PrefixDelegate), instance, prefixMethodInfo));
        }}

        var postfixMethodInfo = instance.GetPatchMethodInfo(""Postfix"");

        if (postfixMethodInfo is not null && postfixMethodInfo.ValidatePatch(OriginalMethod))
        {{
            PostfixDict.Add(instance, (PostfixDelegate)Delegate.CreateDelegate(typeof(PostfixDelegate), instance, postfixMethodInfo));
        }}
    }}

    public void Unpatch(IAccordPatch instance)
    {{
        PrefixDict.Remove(instance);
        PostfixDict.Remove(instance);
    }}

    internal bool Prefix({parameters})
    {{
        foreach (var (key, prefixDelegate) in PrefixDict)
        {{
            try
            {{
                if (!prefixDelegate({parameterSimpleValue}))
                    return false;
            }}
            catch (Exception e)
            {{
                Unpatch(key);
            }}
        }}

        return true;
    }}

    internal void Postfix({parameters})
    {{
        foreach (var (postfixDetour, postfixDelegate) in PostfixDict)
        {{
            try
            {{
                postfixDelegate({parameterSimpleValue});
            }}
            catch (Exception e)
            {{
                Unpatch(postfixDetour);
            }}
        }}
    }}
}}");
    }
}
