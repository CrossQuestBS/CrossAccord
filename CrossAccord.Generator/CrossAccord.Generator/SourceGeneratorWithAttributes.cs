using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;


namespace CrossAccord.Generator;

[Generator]
public class SourceGeneratorWithAttributes : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                (s, _) => s is ClassDeclarationSyntax,
                (ctx, _) => GetClassDeclarationSyntaxForSourceGen(ctx))
            .Where(t => t.reportAttributeFound)
            .Select((t, _) => t.Item1);

        context.RegisterSourceOutput(context.CompilationProvider.Combine(provider.Collect()),
            ((ctx, t) => GenerateCode(ctx, t.Left, t.Right)));
    }

    private static (ClassDeclarationSyntax, bool reportAttributeFound) GetClassDeclarationSyntaxForSourceGen(
        GeneratorSyntaxContext context)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

        foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
        foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
        {
            if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                continue;

            string attributeName = attributeSymbol.ContainingType.ToDisplayString();

            if (attributeName == "CrossAccord.Common.Attributes.AccordPatchAttribute")
                return (classDeclarationSyntax, true);
        }

        return (classDeclarationSyntax, false);
    }

    private string GeneratePatchParameters(INamedTypeSymbol classSymbol, IMethodSymbol methodSymbol)
    {
        StringBuilder sb = new();

        List<string> listParameters = new();

        
        if (!methodSymbol.IsStatic)
        {
            var instanceParameter = "";
            var classNameSpace = classSymbol.ContainingNamespace.ToDisplayString().Replace("<global namespace>", "global::");
        
            if (classNameSpace.Length > 0 && classNameSpace != "global::")
            {
                instanceParameter += classNameSpace + ".";
            }

            instanceParameter += $"{classSymbol.Name} instance";
            listParameters.Add(instanceParameter);
        }
        
        var methodArguments = methodSymbol.Parameters.ToList();
        
        if (methodArguments.Count > 0)
        {
            for (int i = 0; i < methodArguments.Count; i++)
            {
                var currParameter = "";
                var parameterSymbol = methodArguments[i];

                if (parameterSymbol.RefKind == RefKind.In)
                {
                    currParameter += "in ";
                }
                else
                {
                    currParameter += "ref ";
                }
                
                var parameterNamespace = parameterSymbol.Type.ContainingNamespace.ToDisplayString().Replace("<global namespace>", "global::");

                if (parameterNamespace.Length > 0 && parameterNamespace != "global::")
                {
                    currParameter += parameterNamespace + ".";
                }

                currParameter += $"{parameterSymbol.Type.Name.Replace("<global namespace>", "global::")} arg{i + 1}";

                listParameters.Add(currParameter);
            }
        }

        if (methodSymbol.ReturnType.Name == "Void") return string.Join(", ", listParameters);

        var returnTypeParameter = ("ref ");
        var returnTypeNamespace = methodSymbol.ReturnType.ContainingNamespace.ToDisplayString().Replace("<global namespace>", "global::");

        if (returnTypeNamespace.Length > 0 && returnTypeNamespace != "global::")
        {
            returnTypeParameter += (returnTypeNamespace + ".");
        }

        returnTypeParameter += ($"{methodSymbol.ReturnType.Name} returnValue");

        listParameters.Add(returnTypeParameter);
        
        return string.Join(", ", listParameters);
    }
    
    private void GenerateCode(SourceProductionContext context, Compilation compilation,
        ImmutableArray<ClassDeclarationSyntax> classDeclarationSyntaxes)
    {
        foreach (var classDeclarationSyntax in classDeclarationSyntaxes)
        {
            var semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);

            if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
                continue;

            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString().Replace("<global namespace>", "global::");

            var className = classDeclarationSyntax.Identifier.Text;
            
            var generatedAttribute = classSymbol.GetAttributes()
                .First(it => it.AttributeClass?.Name == "AccordPatchAttribute");

            if (generatedAttribute.ConstructorArguments[0].Value is not INamedTypeSymbol patchClassSymbol)
                continue;
            
            if (generatedAttribute.ConstructorArguments[1].Value is not string methodName)
                continue;
            
            var methods = patchClassSymbol.GetMembers().OfType<IMethodSymbol>().ToArray();
            
            if (methods.Length == 0)
                continue;
            
            var method = patchClassSymbol.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(it => it.Name == methodName);

            if (method is null)
            {
                continue;
            }
            
            StringBuilder interfaceCode = new();

            var attributes = classSymbol.GetAttributes().Select(it => it.AttributeClass?.Name ?? "").ToArray();

            var hasPostfixAttribute = attributes.Contains("AccordPostfixAttribute");

            var hasPrefixAttribute = attributes.Contains("AccordPrefixAttribute");
            
            if (hasPostfixAttribute || (!hasPrefixAttribute && !hasPostfixAttribute))
            {
                interfaceCode.Append($"void Postfix({GeneratePatchParameters(patchClassSymbol, method)});\n");
            }

            if (hasPrefixAttribute)
            {
                interfaceCode.Append($"bool Prefix({GeneratePatchParameters(patchClassSymbol, method)});\n");
            }
            
            // Build up the source code
            var code = $@"// <auto-generated/>
namespace {namespaceName} {{
    public partial interface I{className} : CrossAccord.Common.Interfaces.IAccordPatch {{ 
        {interfaceCode}
    }}

    public partial class {className} : I{className} {{
        private void Patch() {{
            CrossAccord.RuntimeManager.Instance.Patch(this);
        }}

        private void Unpatch() {{
            CrossAccord.RuntimeManager.Instance.Unpatch(this);
        }}
    }}
}}
";

            // Add the source code to the compilation.
            context.AddSource($"{className}.g.cs", SourceText.From(code, Encoding.UTF8));
        }
    }
}