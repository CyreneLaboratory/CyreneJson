namespace CyreneJson.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CyreneHandlerAttribute(Type type, CollectionKind kind) : Attribute
{
    public const string ShortName = "CyreneHandler";
    public const string FullName = "CyreneHandlerAttribute";
    public Type Type { get; } = type;
    public CollectionKind Kind { get; } = kind;
}

public enum CollectionKind
{
    List,
    Dictionary
}
