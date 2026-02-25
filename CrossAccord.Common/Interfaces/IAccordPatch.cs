using System.Reflection;

namespace CrossAccord.Common.Interfaces;

public interface IAccordPatch
{
    public MemberInfo MemberMethod { get; }
}