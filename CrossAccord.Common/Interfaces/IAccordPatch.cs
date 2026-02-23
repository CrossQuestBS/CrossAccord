using System.Reflection;

namespace CrossAccord.Common.Interfaces;

public interface IAccordPatch
{
    public MethodInfo Method { get; }
}