// See https://aka.ms/new-console-template for more information

using CrossAccord;
using CrossAccord.Sample;
using CrossAccord.Tests.Patches;

var simpleClass = new SimpleClass();

using (var patch = new SimpleClassPatch())
{
    Console.WriteLine("[START] - Patching SimpleClass OWOWO");
    var returnValue = simpleClass.Run("Patch run!");
    Console.WriteLine("[END] return value is: " + returnValue + "\n");
    
    Console.WriteLine("[START] - Patching SimpleClass without running original");
    patch.shouldRunOriginal = false;
    returnValue = simpleClass.Run("Patch run!");
    Console.WriteLine("[END] return value is: " + returnValue + "\n");
}
Console.WriteLine("[START] - No longer patching SimpleClass");
var returnValue2 = simpleClass.Run("No patches!");
Console.WriteLine("[END] return value is: " + returnValue2);