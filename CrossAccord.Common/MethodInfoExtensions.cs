using System;
using System.Reflection;

namespace CrossAccord.Common;

public static class MethodInfoExtensions
{
    public static bool ValidatePatch(this MethodInfo methodInfo, MethodInfo originalMethod)
    {
        var parameters = methodInfo.GetParameters();
        var originalParameters = originalMethod.GetParameters();

        if (parameters[0].ParameterType.FullName != originalMethod.DeclaringType?.FullName)
            return false;
        
        var prefixParameters = parameters[1..^1];

        if (prefixParameters.Length != originalParameters.Length)
            return false;

        for (int i = 0; i < prefixParameters.Length; i++)
        {
            var prefixParameter = prefixParameters[i];
            var originalParameter = originalParameters[i];

            if (originalParameter.ParameterType.FullName + "&" != prefixParameter.ParameterType.FullName)
                return false;
        }

        return true;
    }
}