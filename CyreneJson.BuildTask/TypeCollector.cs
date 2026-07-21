using CyreneJson.Attributes;
using CyreneJson.BuildTask.Helpers;
using CyreneJson.Handlers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CyreneJson.BuildTask;

public class TypeCollector
{
    private CSharpCompilation Compilation { get; }
    private List<CollectionInfo> Collections { get; } = [];
    public HashSet<string> Errors { get; } = [];

    public List<INamedTypeSymbol> PendingTypes { get; } = [];
    public Dictionary<string, INamedTypeSymbol> AllTypes { get; } = [];
    public Dictionary<string, INamedTypeSymbol> EntryTypes { get; } = [];
    public Dictionary<string, PolymorphicInfo> BaseTypes { get; } = [];
    public string? GenOptions { get; private set; }

    public TypeCollector(string sourceFile, string refFile)
    {
        Compilation = CSharpCompilation.Create("CyreneJsonAnalysis", LoadSyntaxTrees(sourceFile),
            LoadReferences(refFile), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        LoadBuiltinHandlers();
        LoadOutHandlers();
    }

    #region Env

    private static List<SyntaxTree> LoadSyntaxTrees(string sourceFile)
    {
        var syntaxTrees = new List<SyntaxTree>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(sourceFile))
        {
            var file = line.Trim();
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) continue;
            if (!seen.Add(Path.GetFullPath(file))) continue;

            syntaxTrees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Preview), path: file));
        }
        return syntaxTrees;
    }

    private static List<MetadataReference> LoadReferences(string refFile)
    {
        var refs = new List<MetadataReference>();
        foreach (var line in File.ReadAllLines(refFile))
        {
            var path = line.Trim();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        return refs;
    }

    private void LoadBuiltinHandlers()
    {
        foreach (var attr in typeof(BclCollectionHandler).GetCustomAttributes(false))
        {
            if (attr is not CyreneHandlerAttribute handler) continue;

            var key = handler.Type.FullName;
            if (key == null)
            {
                Errors.Add($"Type '{handler.Type}' must have a full name.");
                continue;
            }

            AddCollection(key, handler.Type.GetGenericArguments().Length);
        }
    }

    private void LoadOutHandlers()
    {
        foreach (var tree in Compilation.SyntaxTrees)
        {
            var semanticModel = Compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is not ClassDeclarationSyntax classDecl) continue;
                var symbol = semanticModel.GetDeclaredSymbol(classDecl);
                if (symbol == null) continue;

                foreach (var attr in symbol.GetAttributes())
                {
                    if (!TypeSymbolHelper.IsHandlerAttribute(attr)) continue;
                    if (attr.ConstructorArguments.Length < 1) continue;
                    if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol typeSymbol) continue;

                    AddCollection(TypeSymbolHelper.GetTypeDefinitionKey(typeSymbol),
                        typeSymbol.IsGenericType ? typeSymbol.TypeParameters.Length : 0);
                }
            }
        }
    }

    private void AddCollection(string key, int typeArgs)
    {
        if (typeArgs is not (1 or 2))
        {
            Errors.Add($"Type '{key}' must have 1 or 2 generic arguments.");
            return;
        }

        Collections.Add(new CollectionInfo(key, typeArgs));
    }

    #endregion

    public TypeCollector Collect()
    {
        CollectPendingTypes(Compilation.GlobalNamespace);
        CollectEntryTypesFromType();
        CollectEntryTypesFromMethod();
        foreach (var value in EntryTypes.Values) CollectDerivedTypesFor(value, null);
        CollectProperties();
        CollectBaseTypes();
        CollectGenOptions();
        return this;
    }

    private void CollectGenOptions()
    {
        foreach (var tree in Compilation.SyntaxTrees)
        {
            var model = Compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is not ClassDeclarationSyntax classDecl) continue;
                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol) continue;

                var attr = symbol.GetAttributes().FirstOrDefault(TypeSymbolHelper.IsSourceGenOptionsAttribute);
                if (attr == null) continue;

                GenOptions = TypeSymbolHelper.RenderAttribute(attr);
                if (GenOptions != null) return;
            }
        }
    }

    #region Source

    public void CollectPendingTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!TypeSymbolHelper.IsSourceType(type)) continue;
            PendingTypes.Add(type);
            CollectNestedTypes(type);
        }
        foreach (var child in ns.GetNamespaceMembers()) CollectPendingTypes(child);
    }

    public void CollectNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            if (!TypeSymbolHelper.IsSourceType(nested)) continue;
            PendingTypes.Add(nested);
            CollectNestedTypes(nested);
        }
    }

    #endregion

    #region Collect

    private void AddEntryType(INamedTypeSymbol symbol)
    {
        var key = TypeSymbolHelper.GetFullyQualifiedName(symbol);
        if (TypeSymbolHelper.ContainsOpenArgument(symbol))
        {
            Errors.Add($"Entry type '{key}' cannot be an open generic type.");
            return;
        }

        EntryTypes[key] = symbol;
        AllTypes.TryAdd(key, symbol);
    }

    private void CollectEntryTypesFromType()
    {
        foreach (var tree in Compilation.SyntaxTrees)
        {
            var model = Compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is not ClassDeclarationSyntax syntax) continue;
                var classSymbol = model.GetDeclaredSymbol(syntax);
                if (classSymbol == null) continue;

                foreach (var attr in classSymbol.GetAttributes())
                {
                    if (attr.AttributeClass?.Name is not (CyreneEntryAttribute.ShortName or CyreneEntryAttribute.FullName)) continue;
                    if (attr.ConstructorArguments.Length != 1) continue;
                    if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol entry) continue;

                    AddEntryType(entry);
                }
            }
        }
    }

    private void CollectEntryTypesFromMethod()
    {
        foreach (var tree in Compilation.SyntaxTrees)
        {
            var model = Compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is not InvocationExpressionSyntax invocation) continue;

                var info = model.GetSymbolInfo(invocation);
                if ((info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is not IMethodSymbol symbol) continue;

                var canonical = symbol.ReducedFrom ?? symbol;
                var payloadIndex = TypeSymbolHelper.IsJsonSerializerMethod(canonical) ? 0 : TypeSymbolHelper.GetEntryPayloadIndex(canonical);
                if (payloadIndex < 0) continue;
                if (payloadIndex >= symbol.TypeArguments.Length) continue;
                if (symbol.TypeArguments[payloadIndex] is not INamedTypeSymbol entry) continue;

                // Supported generic
                if (entry.IsGenericType && Collections.Any(c => c.Key == TypeSymbolHelper.GetTypeDefinitionKey(entry) && c.TypeArgs == entry.TypeArguments.Length))
                {
                    CollectPropType([], entry);
                    continue;
                }
                if (!TypeSymbolHelper.IsSourceType(entry)) continue;

                AddEntryType(entry);
            }
        }
    }

    private void CollectProperties()
    {
        var visited = new HashSet<string>();
        var toScan = new Queue<INamedTypeSymbol>(AllTypes.Values);
        while (toScan.Count > 0)
        {
            var type = toScan.Dequeue();
            var key = TypeSymbolHelper.GetFullyQualifiedName(type);
            if (!visited.Add(key)) continue;
            if (!TypeSymbolHelper.IsSourceType(type)) continue;

            foreach (var member in type.GetMembers())
            {
                if (member is not IPropertySymbol prop) continue;
                if (prop.DeclaredAccessibility != Accessibility.Public) continue;
                CollectPropType(toScan, prop.Type);
            }
        }
    }

    private void CollectDerivedTypesFor(INamedTypeSymbol baseType, Queue<INamedTypeSymbol>? toScan)
    {
        foreach (var candidate in PendingTypes)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, baseType)) continue;
            if (TypeSymbolHelper.ContainsOpenArgument(candidate)) continue;
            if (!TypeSymbolHelper.IsDerivedFrom(candidate, baseType)) continue;

            var key = TypeSymbolHelper.GetFullyQualifiedName(candidate);
            if (AllTypes.TryAdd(key, candidate)) toScan?.Enqueue(candidate);
        }
    }

    private void CollectBaseTypesFor(INamedTypeSymbol type, Queue<INamedTypeSymbol> toScan)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (!TypeSymbolHelper.IsSourceType(current)) return;
            if (TypeSymbolHelper.ContainsOpenArgument(current)) return;
            if (TypeSymbolHelper.IsPrimitiveOrWellKnown(current)) return;

            var key = TypeSymbolHelper.GetFullyQualifiedName(current);
            if (AllTypes.TryAdd(key, current)) toScan.Enqueue(current);

            CollectDerivedTypesFor(current, toScan);

            if (EntryTypes.ContainsKey(key)) return; // Base type itself registered
            current = current.BaseType;
        }
    }

    private void CollectPropType(Queue<INamedTypeSymbol> toScan, ITypeSymbol propType)
    {
        if (TypeSymbolHelper.IsPrimitiveOrWellKnown(propType)) return;

        // Nullable
        if (propType is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T)
        {
            CollectPropType(toScan, nullable.TypeArguments[0]);
            return;
        }

        // Array
        if (propType is IArrayTypeSymbol array)
        {
            CollectPropType(toScan, array.ElementType);
            return;
        }

        if (propType is INamedTypeSymbol named)
        {
            // Collection
            if (named.IsGenericType)
            {
                var key = TypeSymbolHelper.GetTypeDefinitionKey(named);
                var name = TypeSymbolHelper.GetFullyQualifiedName(named);
                var match = Collections.FirstOrDefault(c => c.Key == key && c.TypeArgs == named.TypeArguments.Length);
                if (match == null)
                {
                    Errors.Add($"Unsupported generic type '{name}'.");
                    return;
                }

                AllTypes.TryAdd(name, named);
                var elements = match.TypeArgs switch
                {
                    1 => [named.TypeArguments[0]],
                    2 => named.TypeArguments,
                    _ => []
                };
                foreach (var element in elements) CollectPropType(toScan, element);

                return;
            }

            // Object
            if (named.TypeKind is TypeKind.Class or TypeKind.Struct && named.SpecialType == SpecialType.None)
            {
                if (!TypeSymbolHelper.IsSourceType(named)) return;
                var key = TypeSymbolHelper.GetFullyQualifiedName(named);
                if (AllTypes.TryAdd(key, named)) toScan.Enqueue(named);

                CollectDerivedTypesFor(named, toScan); // Derived
                CollectBaseTypesFor(named, toScan); // Base chain
            }
        }
    }

    private void CollectBaseTypes()
    {
        foreach (var (key, type) in AllTypes)
        {
            if (type.IsSealed || type.TypeKind != TypeKind.Class) continue;
            if (!TypeSymbolHelper.IsSourceType(type)) continue;

            var derived = new List<INamedTypeSymbol>();
            foreach (var candidate in AllTypes.Values)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate, type)) continue;
                if (TypeSymbolHelper.ContainsOpenArgument(candidate)) continue;
                if (!TypeSymbolHelper.IsDerivedFrom(candidate, type)) continue;
                derived.Add(candidate);
            }

            if (derived.Count == 0) continue;
            BaseTypes[key] = new PolymorphicInfo(type, derived);
        }
    }

    #endregion
}
