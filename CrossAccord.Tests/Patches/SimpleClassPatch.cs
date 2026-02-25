using System.Reflection;
using CrossAccord.Common.Attributes;
using CrossAccord.Sample;

namespace CrossAccord.Tests.Patches;

[AccordPatch(typeof(SimpleClass), nameof(SimpleClass.Run))]
[AccordPostfix]
[AccordPrefix]
public partial class SimpleClassPatch : IDisposable
{
    public bool shouldRunOriginal { get; set; }= true;
    
    public SimpleClassPatch()
    {
        Patch();
    }

    public MethodInfo Method { get; } = typeof(SimpleClass).GetMethod(nameof(SimpleClass.Run))!;
    
    public void Postfix(SimpleClass instance, ref string arg1, ref bool returnValue)
    {
        returnValue = false;
        Console.WriteLine($"[Postfix] arg1: {arg1}, modified returnValue: {returnValue}");
    }

    public bool Prefix(SimpleClass instance, ref string arg1, ref bool returnValue)
    {
        Console.WriteLine($"[Prefix] modified arg1: {arg1}, run [ORIG]: {(shouldRunOriginal ? "True" : "False")}");
        return shouldRunOriginal;
    }

    public void Dispose()
    {
       Unpatch();
    }
}