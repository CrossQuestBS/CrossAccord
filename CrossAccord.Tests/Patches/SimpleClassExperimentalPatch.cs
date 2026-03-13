using System.Reflection;
using CrossAccord.Common.Attributes;
using CrossAccord.Sample;

namespace CrossAccord.Tests.Patches;

[AccordPatch(typeof(SimpleClass), nameof(SimpleClass.RunWithIn))]
public partial class SimpleClassExperimentalPatch : IDisposable
{
    public MemberInfo MemberMethod { get; } = typeof(SimpleClass).GetMember("RunWithIn").FirstOrDefault()!;

    public SimpleClassExperimentalPatch()
    {
        Patch();
    }
    
    public void Postfix(SimpleClass instance, in string arg1, ref bool returnValue)
    {
        Console.WriteLine("[Postfix] with value: " + arg1);
    }

    public void Dispose()
    {
        Unpatch();
    }
}