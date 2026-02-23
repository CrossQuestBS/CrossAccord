// See https://aka.ms/new-console-template for more information

using System.Reflection;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace CrossAccord.Builder;

public static class GeneratePatches
{
    public static void Run()
    {
        string projectDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location).Parent.Parent.Parent
            .Parent.FullName;
        var assembliesPath = Path.Join(projectDirectory, "CrossAccord.Tests/bin/Debug/net10.0");
        
        List<string> assemblies = new();

        foreach (var file in Directory.GetFiles(assembliesPath))
        {
            if (file.EndsWith(".DS_Store"))
                continue;

            if (!file.EndsWith(".dll"))
                continue;

            Console.WriteLine(file);

            assemblies.Add(file);
        }

        Console.WriteLine();
        var patchers = AssemblyGenerator.GetAllPatchers(assemblies.ToArray());

        Console.WriteLine("Patcher result: \n");
        foreach (var (key, value) in patchers)
        {
            Console.WriteLine($"Patcher for {key}\n");
        }

        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);

        var domain = AppDomain.CurrentDomain.GetAssemblies();
        List<MetadataReference> metadataReferences = new();
        
        
        metadataReferences.AddRange(NetStandard21.References.All);
        
        var assemblyyy = Assembly.GetExecutingAssembly();
        MetadataReference[] references =
            new MetadataReference[]
            {
                MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
                MetadataReference.CreateFromFile(typeof(CrossAccord.Common.Attributes.AccordPatchAttribute).Assembly
                    .Location),
                MetadataReference.CreateFromFile(Path.Combine(assembliesPath, "CrossAccord.Sample.dll")),
                MetadataReference.CreateFromFile(Path.Combine(assembliesPath, "CrossAccord.dll")),
                
            };

        metadataReferences.AddRange(references);
        
        CSharpCompilation compilation = CSharpCompilation.Create(
            "CrossAccord.Generated",
            syntaxTrees: patchers.Values.ToArray(),
            references: metadataReferences.ToArray(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using (var ms = new MemoryStream())
        {
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
            else
            {
                // load assembly and add to cache
                ms.Seek(0, SeekOrigin.Begin);

                var fileStream = File.Create(projectDirectory + "/CrossAccord.Generated.dll");

                ms.CopyTo(fileStream);
                fileStream.Close();
            }
        }
    }
}