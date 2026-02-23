using System;
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
        var classNameSpace = classSymbol.ContainingNamespace.ToDisplayString();
        
        if (classNameSpace.Length > 0)
        {
            sb.Append(classNameSpace + ".");
        }

        sb.Append($"{classSymbol.Name} instance");
        
        var methodArguments = methodSymbol.Parameters.ToList();

        if (methodArguments.Count > 0)
        {
            sb.Append(", ");
            for (int i = 0; i < methodArguments.Count; i++)
            {
                var parameterSymbol = methodArguments[i];
                sb.Append($"ref ");
                var parameterNamespace = parameterSymbol.Type.ContainingNamespace.ToDisplayString();

                if (parameterNamespace.Length > 0)
                {
                    sb.Append(parameterNamespace + ".");
                }

                sb.Append($"{parameterSymbol.Type.Name} arg{i + 1}");

                if (i != methodArguments.Count - 1)
                {
                    sb.Append(", ");
                }
            }
        }

        if (methodSymbol.ReturnType.Name == "Void") return sb.ToString();
        
        sb.Append(", ref ");
        var returnTypeNamespace = methodSymbol.ReturnType.ContainingNamespace.ToDisplayString();

        if (returnTypeNamespace.Length > 0)
        {
            sb.Append(returnTypeNamespace + ".");
        }

        sb.Append($"{methodSymbol.ReturnType.Name} returnValue");

        return sb.ToString();
    }
    
    private void GenerateCode(SourceProductionContext context, Compilation compilation,
        ImmutableArray<ClassDeclarationSyntax> classDeclarationSyntaxes)
    {
        foreach (var classDeclarationSyntax in classDeclarationSyntaxes)
        {
            var semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);

            if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol interfaceSymbol)
                continue;

            var namespaceName = interfaceSymbol.ContainingNamespace.ToDisplayString();

            var className = classDeclarationSyntax.Identifier.Text;
            
            var generatedAttribute = interfaceSymbol.GetAttributes()
                .First(it => it.AttributeClass?.Name == "AccordPatchAttribute");

            if (generatedAttribute.ConstructorArguments[0].Value is not INamedTypeSymbol classSymbol)
                continue;
            
            if (generatedAttribute.ConstructorArguments[1].Value is not string methodName)
                continue;
            
            var method = classSymbol.GetMembers().OfType<IMethodSymbol>().First(it => it.Name == methodName);
            
            StringBuilder interfaceCode = new();
            
            // TODO: Update to check for attribute
            // Prefix
            /*if (patchTypes.Contains(0))
            {
                interfaceCode.Append($"bool Prefix({GeneratePatchParameters(classSymbol, method)});\n");
            }*/
            
          
            interfaceCode.Append($"void Postfix({GeneratePatchParameters(classSymbol, method)});\n");
           
            
            // Build up the source code
            var code = $@"// <auto-generated/>
namespace {namespaceName};

public partial interface I{className} : CrossAccord.Common.Interfaces.IAccordPatch {{ 
    {interfaceCode}
}}

public partial class {className} : I{className} {{
    private void Patch() {{
        CrossAccord.RuntimeManager.Patch(this);
    }}

    private void Unpatch() {{
        CrossAccord.RuntimeManager.Unpatch(this);
    }}
}}
";

            // Add the source code to the compilation.
            context.AddSource($"{className}.g.cs", SourceText.From(code, Encoding.UTF8));
        }
    }
}