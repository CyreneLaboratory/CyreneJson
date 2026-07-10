namespace CyreneJson.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CyreneHandlerAttribute(Type type) : Attribute
{
    public const string ShortName = "CyreneHandler";
    public const string FullName = "CyreneHandlerAttribute";
    public Type Type { get; } = type;
}
