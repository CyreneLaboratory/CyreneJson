using CyreneJson.Attributes;
using Microsoft.CodeAnalysis;

namespace CyreneJson.BuildTask.Helpers;

public record CollectionInfo(string Name, int TypeArgs, CollectionKind Kind);

public record PolymorphicInfo(INamedTypeSymbol BaseType, List<INamedTypeSymbol> DerivedTypes);
