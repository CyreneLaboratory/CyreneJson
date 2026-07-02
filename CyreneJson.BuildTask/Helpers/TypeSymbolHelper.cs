using CyreneJson.Attributes;
using Microsoft.CodeAnalysis;

namespace CyreneJson.BuildTask.Helpers;

public static class TypeSymbolHelper
{
    public static bool HasJsonAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name is CyreneJsonAttribute.ShortName or CyreneJsonAttribute.FullName);
    }

    public static bool IsHandlerAttribute(AttributeData attr)
    {
        return attr.AttributeClass?.Name is CyreneHandlerAttribute.ShortName or CyreneHandlerAttribute.FullName;
    }

    public static bool IsPrimitiveOrWellKnown(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or
            SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or
            SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or
            SpecialType.System_Decimal or SpecialType.System_String or SpecialType.System_Char or
            SpecialType.System_Object or SpecialType.System_DateTime) return true;
        if (type.TypeKind == TypeKind.Enum) return true;
        if (type.ToDisplayString() is "System.Guid" or "System.TimeSpan" or "System.DateTimeOffset" or "System.Uri") return true;
        return false;
    }

    public static string GetFullyQualifiedName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    public static bool IsSourceType(INamedTypeSymbol type)
    {
        return !type.IsImplicitlyDeclared && type.Locations.Any(l => l.IsInSource);
    }

    public static bool IsDerivedFrom(INamedTypeSymbol candidate, INamedTypeSymbol baseType)
    {
        var current = candidate.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType)) return true;
            current = current.BaseType;
        }
        return false;
    }
}
