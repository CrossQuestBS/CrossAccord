using System.Reflection;

namespace CrossAccord.Test.Examples;

using Common.Attributes;


[AccordPatch(typeof(SimpleClass), nameof(SimpleClass.Run))]
public partial class SimpleClassPatch
{
    public MethodInfo Method { get; } = typeof(SimpleClass).GetMethod(nameof(SimpleClass.Run))!;
    public void Postfix(SimpleClass instance, ref string arg1, ref bool returnValue)
    {
        throw new System.NotImplementedException();
    }
}