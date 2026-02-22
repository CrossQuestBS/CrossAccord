using System;

namespace CrossAccord.Common.Attributes;

/// <summary>
/// Generate patches on nonstatic class <br/>
/// <b>NOTE:</b> This will by default generate to Postfix if not specified.
/// </summary>
/// <param name="declaringType"></param>
/// <param name="methodName"></param>
[AttributeUsage(AttributeTargets.Class)]
public class AccordPatchAttribute(Type declaringType, string methodName) : Attribute
{
    public Type DeclaringType = declaringType;
    public string MethodName = methodName;
}