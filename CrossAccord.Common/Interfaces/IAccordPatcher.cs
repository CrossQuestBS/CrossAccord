namespace CrossAccord.Common.Interfaces;

public interface IAccordPatcher
{
    public void Patch(IAccordPatch patch);
    public void Unpatch(IAccordPatch patch);
}