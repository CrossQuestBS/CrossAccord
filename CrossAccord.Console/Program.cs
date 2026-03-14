// See https://aka.ms/new-console-template for more information

using System.Reflection;
using CrossAccord.Builder;

string projectDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location).Parent.Parent.Parent
    .Parent.FullName;
        
var assembliesPath = Path.Join(projectDirectory, "CrossAccord.Tests/bin/Release/net10.0");
        
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
AssemblyGenerator.GeneratePatcherAssembly(patchers, assemblies.ToArray(), assembliesPath);
AssemblyPatcher.PatchAll(patchers, Path.Join(assembliesPath , "CrossAccord.Generated.dll"), assembliesPath);