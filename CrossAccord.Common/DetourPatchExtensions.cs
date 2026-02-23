using System.Reflection;
using CrossAccord.Common.Interfaces;

namespace CrossAccord.Common;

public static class DetourPatchExtensions
{
    public static MethodInfo? GetPatchMethodInfo(this IAccordPatch instance, string methodName)
    {
        var patchMethod = instance.GetType().GetMethod(methodName);

        if (patchMethod is null)
            return null;

        return patchMethod;
    }
}