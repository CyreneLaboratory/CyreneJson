namespace CyreneJson.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class CyreneJsonAttribute : Attribute
{
    public const string ShortName = "CyreneJson";
    public const string FullName = "CyreneJsonAttribute";
}
