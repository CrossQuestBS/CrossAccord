namespace CrossAccord.Test.Examples;

using Common.Attributes;


[AccordPatch(typeof(SimpleClass), nameof(SimpleClass.Run))]
public partial class SimpleClassPatch
{
    
}