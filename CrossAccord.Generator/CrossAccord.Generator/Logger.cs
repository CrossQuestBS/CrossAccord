using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CrossAccord.Generator;

internal static class Log
{
    public static List<string> Logs { get; } = new();
    
    public static void Print(string msg) => Logs.Add("//\t" + msg);

// More print methods ...

    public static void FlushLogs(SourceProductionContext context)
    {
        context.AddSource($"logs.g.cs_{Guid.NewGuid().ToString().Replace("-", "")}", SourceText.From(string.Join("\n", Logs), Encoding.UTF8));
    }
}