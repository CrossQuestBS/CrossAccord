using System.Reflection;
using CrossAccord.Common.Attributes;
using CrossAccord.Sample;

namespace CrossAccord.Tests.Patches;

[AccordPatch(typeof(SimpleClass), nameof(SimpleClass.Run))]
public partial class SimpleClassPatch : IDisposable
{
    public SimpleClassPatch()
    {
        Patch();
    }

    public MethodInfo Method { get; } = typeof(SimpleClass).GetMethod(nameof(SimpleClass.Run))!;
    
    public void Postfix(SimpleClass instance, ref string arg1, ref bool returnValue)
    {
        Console.WriteLine("[POSTFIX HOOKED!]");
    }

    public void Dispose()
    {
       Unpatch();
    }
}