namespace CyreneJson.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class CyreneEntryAttribute : Attribute
{
    public const string ShortName = "CyreneEntry";
    public const string FullName = "CyreneEntryAttribute";

    public Type? EntryType { get; }
    public int PayloadIndex { get; }
    public CyreneEntryAttribute(Type type) => EntryType = type;
    public CyreneEntryAttribute(int payloadIndex = 0) => PayloadIndex = payloadIndex;
}
