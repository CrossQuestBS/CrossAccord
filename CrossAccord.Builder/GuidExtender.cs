namespace CrossAccord.Builder;

public static class GuidExtender
{
    public static string ToClassSafeString(this Guid guid)
    {
        return guid.ToString().Replace("-", "").ToUpper();
    }
}