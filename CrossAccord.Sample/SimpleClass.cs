using System;

namespace CrossAccord.Sample;

public class SimpleClass
{
    public bool Run(string example)
    {
        Console.WriteLine($"[ORIG] arg1: {example}, returning true");
        return true;
    }

    public bool RunWithIn(in string value)
    {
        Console.WriteLine($"Value is : " + value);
        return false;
    }
}