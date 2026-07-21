using System.Globalization;
using CyreneJson.Attributes;
using Microsoft.CodeAnalysis;

namespace CyreneJson.BuildTask.Helpers;

public static class TypeSymbolHelper
{
    public static bool IsHandlerAttribute(AttributeData attr)
    {
        return attr.AttributeClass?.Name is CyreneHandlerAttribute.ShortName or CyreneHandlerAttribute.FullName;
    }

    public static bool IsSourceGenOptionsAttribute(AttributeData attr)
    {
        return attr.AttributeClass?.Name == "JsonSourceGenerationOptionsAttribute";
    }

    public static bool IsJsonSerializerMethod(IMethodSymbol method)
    {
        if (method.ContainingType?.ToDisplayString() != "System.Text.Json.JsonSerializer") return false;
        return method.Name is "Serialize" or "SerializeAsync" or "SerializeToUtf8Bytes" or "SerializeToNode" or
            "SerializeToElement" or "SerializeToDocument" or "Deserialize" or "DeserializeAsync";
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

    public static int GetEntryPayloadIndex(IMethodSymbol method)
    {
        foreach (var attr in method.GetAttributes())
        {
            if (attr.AttributeClass?.Name is not (CyreneEntryAttribute.ShortName or CyreneEntryAttribute.FullName)) continue;
            if (attr.ConstructorArguments.Length == 0) return 0;
            if (attr.ConstructorArguments[0].Value is int index) return index;
        }
        return -1;
    }

    public static string GetFullyQualifiedName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    public static string GetTypeDefinitionKey(INamedTypeSymbol type)
    {
        return GetMetadataName(type.OriginalDefinition);
    }

    private static string GetMetadataName(INamedTypeSymbol type)
    {
        if (type.ContainingType != null) return $"{GetMetadataName(type.ContainingType)}+{type.MetadataName}";
        if (type.ContainingNamespace is { IsGlobalNamespace: false } ns) return $"{ns.ToDisplayString()}.{type.MetadataName}";
        return type.MetadataName;
    }

    // Skip all source types, need filter generic type with user type arg before use this method
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

    public static bool ContainsOpenArgument(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter) return true;
        if (type is IArrayTypeSymbol array) return ContainsOpenArgument(array.ElementType);
        if (type is not INamedTypeSymbol named) return false;
        return named.TypeArguments.Any(ContainsOpenArgument);
    }

    public static string? RenderAttribute(AttributeData attr)
    {
        var attrType = attr.AttributeClass;
        if (attrType == null) return null;

        var args = new List<string>();
        foreach (var ctorArg in attr.ConstructorArguments)
        {
            var rendered = RenderConstant(ctorArg);
            if (rendered == null) return null;
            args.Add(rendered);
        }
        foreach (var named in attr.NamedArguments)
        {
            var rendered = RenderConstant(named.Value);
            if (rendered == null) return null;
            args.Add($"{named.Key} = {rendered}");
        }

        var name = attrType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return args.Count == 0 ? $"[{name}]" : $"[{name}({string.Join(", ", args)})]";
    }

    private static string? RenderConstant(TypedConstant constant)
    {
        if (constant.IsNull) return "null";

        return constant.Kind switch
        {
            TypedConstantKind.Enum => RenderEnum(constant),
            TypedConstantKind.Type => constant.Value is ITypeSymbol type ? $"typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})" : null,
            TypedConstantKind.Primitive => RenderPrimitive(constant.Value),
            _ => null
        };
    }

    private static string? RenderPrimitive(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            string s => $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
            char c => $"'{c}'",
            float f => f.ToString("R", CultureInfo.InvariantCulture) + "f",
            double d => d.ToString("R", CultureInfo.InvariantCulture) + "d",
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
            IFormattable n => n.ToString(null, CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static string? RenderEnum(TypedConstant constant)
    {
        if (constant.Type is not INamedTypeSymbol enumType) return null;
        var fqn = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        foreach (var m in enumType.GetMembers().OfType<IFieldSymbol>())
            if (m.HasConstantValue && Equals(m.ConstantValue, constant.Value))
                return $"{fqn}.{m.Name}";

        return $"({fqn})({Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture)})";
    }
}
