using Microsoft.CodeAnalysis;

namespace CyreneJson.BuildTask.Helpers;

public record CollectionInfo(string Key, int TypeArgs);

public record PolymorphicInfo(INamedTypeSymbol BaseType, List<INamedTypeSymbol> DerivedTypes);
