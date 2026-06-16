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

    public List<INamedTypeSymbol> PendingTypes { get; } = [];
    public Dictionary<string, INamedTypeSymbol> AllTypes { get; } = [];
    public Dictionary<string, INamedTypeSymbol> EntryTypes { get; } = [];
    public Dictionary<string, PolymorphicInfo> BaseTypes { get; } = [];

    public TypeCollector(string projDir, string refFile)
    {
        Compilation = CSharpCompilation.Create("CyreneJsonAnalysis", LoadSyntaxTrees(projDir),
            LoadReferences(refFile), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        LoadBuiltinHandlers();
        LoadOutHandlers();
    }

    #region Env

    private static List<SyntaxTree> LoadSyntaxTrees(string projDir)
    {
        var syntaxTrees = new List<SyntaxTree>();
        foreach (var file in Directory.GetFiles(projDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

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

            // Except '`'
            var name = handler.Type.Name;
            var backtick = name.IndexOf('`');
            if (backtick >= 0) name = name[..backtick];

            Collections.Add(new CollectionInfo(name, handler.Type.GetGenericArguments().Length, handler.Kind));
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
                    if (attr.ConstructorArguments.Length < 2) continue;
                    if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol typeSymbol) continue;
                    if (attr.ConstructorArguments[1].Value is not int kindValue) continue;

                    Collections.Add(new CollectionInfo(
                        typeSymbol.Name, typeSymbol.IsGenericType ? typeSymbol.TypeParameters.Length : 0, (CollectionKind)kindValue));
                }
            }
        }
    }

    #endregion

    public TypeCollector Collect()
    {
        CollectPendingTypes(Compilation.GlobalNamespace);
        CollectEntryTypes();
        CollectDerivedTypes();
        CollectProperties();
        CollectBaseTypes();
        return this;
    }

    #region Source

    public void CollectPendingTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            PendingTypes.Add(type);
            CollectNestedTypes(type);
        }
        foreach (var child in ns.GetNamespaceMembers()) CollectPendingTypes(child);
    }

    public void CollectNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            PendingTypes.Add(nested);
            CollectNestedTypes(nested);
        }
    }

    #endregion

    #region Collect

    private void CollectEntryTypes()
    {
        foreach (var tree in Compilation.SyntaxTrees)
        {
            var model = Compilation.GetSemanticModel(tree);
            foreach (var classDecl in tree.GetRoot().DescendantNodes())
            {
                if (classDecl is not ClassDeclarationSyntax syntax) continue;
                var symbol = model.GetDeclaredSymbol(syntax);
                if (symbol == null) continue;
                if (!TypeSymbolHelper.HasJsonAttribute(symbol)) continue;

                var key = TypeSymbolHelper.GetFullyQualifiedName(symbol);
                EntryTypes[key] = symbol;
                if (!symbol.IsUnboundGenericType && symbol.TypeParameters.Length == 0) AllTypes.TryAdd(key, symbol);
            }
        }
    }

    private void CollectDerivedTypes()
    {
        foreach (var (key, value) in EntryTypes)
            foreach (var candidate in PendingTypes)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate, value)) continue;
                if (candidate.IsUnboundGenericType || candidate.TypeParameters.Length > 0) continue;
                if (!TypeSymbolHelper.IsDerivedFrom(candidate, value)) continue;

                AllTypes.TryAdd(TypeSymbolHelper.GetFullyQualifiedName(candidate), candidate);
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

            foreach (var member in type.GetMembers())
            {
                if (member is not IPropertySymbol prop) continue;
                if (prop.DeclaredAccessibility != Accessibility.Public) continue;
                CollectPropType(toScan, prop.Type);
            }
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
                var match = Collections.FirstOrDefault(c => c.Name == named.Name && c.TypeArgs == named.TypeArguments.Length);
                if (match != null)
                {
                    // Self
                    var collKey = TypeSymbolHelper.GetFullyQualifiedName(named);
                    if (AllTypes.TryAdd(collKey, named)) toScan.Enqueue(named);

                    // Element
                    var elements = match.Kind switch
                    {
                        CollectionKind.List => [named.TypeArguments[0]],
                        CollectionKind.Dictionary => named.TypeArguments,
                        _ => []
                    };
                    foreach (var element in elements) CollectPropType(toScan, element);
                    return;
                }
            }

            // Object
            if (named.TypeKind is TypeKind.Class or TypeKind.Struct
                && named.SpecialType == SpecialType.None && named.TypeParameters.Length == 0)
            {
                var key = TypeSymbolHelper.GetFullyQualifiedName(named);
                if (AllTypes.TryAdd(key, named)) toScan.Enqueue(named);

                // Derived
                foreach (var candidate in PendingTypes)
                {
                    if (SymbolEqualityComparer.Default.Equals(candidate, named)) continue;
                    if (candidate.TypeParameters.Length > 0) continue;
                    if (!TypeSymbolHelper.IsDerivedFrom(candidate, named)) continue;

                    if (AllTypes.TryAdd(TypeSymbolHelper.GetFullyQualifiedName(candidate), candidate)) toScan.Enqueue(candidate);
                }
            }
        }
    }

    private void CollectBaseTypes()
    {
        foreach (var (key, type) in AllTypes)
        {
            if (type.IsSealed || type.TypeKind != TypeKind.Class) continue;

            var derived = new List<INamedTypeSymbol>();
            foreach (var candidate in AllTypes.Values)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate, type)) continue;
                if (candidate.TypeParameters.Length != 0) continue;
                if (!TypeSymbolHelper.IsDerivedFrom(candidate, type)) continue;
                derived.Add(candidate);
            }

            if (derived.Count == 0) continue;
            BaseTypes[key] = new PolymorphicInfo(type, derived);
        }
    }

    #endregion
}
