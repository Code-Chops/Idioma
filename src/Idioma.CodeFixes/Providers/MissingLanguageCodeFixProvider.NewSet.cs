using CodeChops.Idioma.Generators;

namespace CodeChops.Idioma.CodeFixes;

/// <inheritdoc cref="MissingLanguageCodeFixProvider"/>
public sealed partial class MissingLanguageCodeFixProvider
{
    /// <summary>
    /// A set to add: the document its diagnostic is in, next to which its file goes, its name and namespace, its
    /// declaration without a body, the namespaces its file imports for the types it names, and the name and path of its
    /// file.
    /// </summary>
    private sealed record NewSet(Document Document, string Name, string Namespace, string Declaration, ImmutableSortedSet<string> Usings, string FileName, string? FilePath)
    {
        /// <summary>
        /// The set a diagnostic asks for, or <see langword="null"/> when the diagnostic names none, or a type or a file
        /// of the set's name exists already.
        /// </summary>
        public static async Task<NewSet?> ReadAsync(Document document, Compilation compilation, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            if (!diagnostic.Properties.TryGetValue(LocalizationSetsPart.SuggestedNameProperty, out var name) || name is null
                || !diagnostic.Properties.TryGetValue(LocalizationSetsPart.SuggestedDeclarationProperty, out var declaration) || declaration is null)
            {
                return null;
            }

            // A set of a library's contract has no namespace of its own, so it takes that of the app.
            if (!diagnostic.Properties.TryGetValue(LocalizationSetsPart.SuggestedNamespaceProperty, out var @namespace) || @namespace is null)
                @namespace = await FindNamespaceAsync(document, cancellationToken).ConfigureAwait(false);

            var fileName = $"{name}.cs";
            var filePath = document.FilePath is { } path && Path.GetDirectoryName(path) is { } directory ? Path.Combine(directory, fileName) : null;
            var set = new NewSet(document, name, @namespace, declaration, ImmutableSortedSet.Create<string>(StringComparer.Ordinal), fileName, filePath);

            if (compilation.GetTypeByMetadataName(@namespace.Length > 0 ? $"{@namespace}.{name}" : name) is not null || IsTaken(document.Project, set))
                return null;

            var (shortened, usings) = NameTypesAsFileCan(declaration, @namespace, compilation, cancellationToken);
            return set with { Declaration = shortened, Usings = usings };
        }

        /// <summary>
        /// The declaration with each type it names by its full name, as the contract of a library or the enum of
        /// <c>ILocalizationSet&lt;TEnum&gt;</c>, named as the new file reaches it, with the namespaces the file then
        /// imports. Each name is the first of these that the compiler binds to the type in that file:
        /// <list type="number">
        /// <item>The name alone, as where a global using imports its namespace.</item>
        /// <item>The name alone, with a <see langword="using"/> of its namespace.</item>
        /// <item>The full name, as the diagnostic writes it.</item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// The full name isn't the first choice, because a namespace around the set can take its first part:
        /// <c>Shop.IShopLocalization</c> means <c>Contoso.Shop.IShopLocalization</c> inside <c>namespace Contoso.Shop</c>.
        /// A <see langword="using"/> at the top of the file reads its namespace from the global namespace, so it isn't
        /// taken. A type it can't find stays as it is written, such as a contract the generator writes, which a workspace
        /// may not hold yet.
        /// </remarks>
        private static (string Declaration, ImmutableSortedSet<string> Usings) NameTypesAsFileCan(string declaration, string @namespace, Compilation compilation,
            CancellationToken cancellationToken)
        {
            var usings = ImmutableSortedSet.Create<string>(StringComparer.Ordinal);

            if (SyntaxFactory.ParseMemberDeclaration($"{declaration} {{ }}") is not ClassDeclarationSyntax { BaseList: { } baseList }
                || baseList.Types.Any(static type => type.Type.ContainsDiagnostics))
            {
                return (declaration, usings);
            }

            var names = baseList.Types.Select(static type => type.Type.ToString()).ToArray();
            var head = declaration.Substring(0, baseList.SpanStart);

            for (var i = 0; i < names.Length; i++)
            {
                // A contract, or the enum of ILocalizationSet<Shop.OrderStatus>, which a namespace around the set can
                // take the first part of just the same. Each name it tries stands in the place of the full one: alone,
                // or as the type argument.
                QualifiedNameSyntax written;
                Func<string, string> around;

                switch (baseList.Types[i].Type)
                {
                    case QualifiedNameSyntax qualified:
                        written = qualified;
                        around = static name => name;
                        break;

                    case GenericNameSyntax { TypeArgumentList.Arguments: [QualifiedNameSyntax argument] } generic:
                    {
                        var identifier = generic.Identifier.Text;
                        written = argument;
                        around = name => $"{identifier}<{name}>";
                        break;
                    }

                    default:
                        continue;
                }

                if (compilation.GetTypeByMetadataName(ToMetadataName(written)) is not { } type)
                    continue;

                var (left, right) = (written.Left, written.Right);

                var withUsing = usings.Add(left.ToString());

                foreach (var (name, imports) in new[] { (right.ToString(), usings), (right.ToString(), withUsing), (written.ToString(), usings) })
                {
                    names[i] = around(name);

                    if (Binds(Write(@namespace, $"{head}: {String.Join(", ", names)}", imports, "\n"), i, type, compilation, cancellationToken))
                    {
                        usings = imports;
                        break;
                    }

                    // As the diagnostic writes it until one binds, and when none does.
                    names[i] = around(written.ToString());
                }
            }

            return ($"{head}: {String.Join(", ", names)}", usings);
        }

        /// <summary>
        /// The name the compiler finds a type of a namespace by, without the <c>@</c> that escapes a keyword:
        /// <c>Shop.IShopLocalization</c>.
        /// </summary>
        private static string ToMetadataName(QualifiedNameSyntax name)
        {
            var metadataName = String.Join(".", name.ToString().Split('.').Select(static part => part.Trim().TrimStart('@')));
            return metadataName;
        }

        /// <summary>
        /// Whether a base type of the new set binds to <paramref name="type"/>, or its type argument does, in the file as
        /// it would be added to the project. The argument by itself, because the file imports the package's namespace
        /// only once the names are settled, so <c>ILocalizationSet&lt;TEnum&gt;</c> binds to nothing yet.
        /// </summary>
        private static bool Binds(string file, int index, INamedTypeSymbol type, Compilation compilation, CancellationToken cancellationToken)
        {
            var tree = CSharpSyntaxTree.ParseText(file, compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions, cancellationToken: cancellationToken);
            var model = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);

            if (tree.GetRoot(cancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault() is not { BaseList.Types: var types } || index >= types.Count)
                return false;

            var written = types[index].Type is GenericNameSyntax { TypeArgumentList.Arguments: [var argument] } ? argument : types[index].Type;

            var binds = SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(written, cancellationToken).Symbol, type);
            return binds;
        }

        /// <summary>
        /// The namespace for a set next to a document: the first namespace the document declares, and else the
        /// project's default namespace, which is empty when it has none.
        /// </summary>
        private static async Task<string> FindNamespaceAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var declared = root?.DescendantNodes(static node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault();

            if (declared is null)
                return document.Project.DefaultNamespace ?? "";

            // A namespace inside another is named with the ones around it.
            var parts = declared.AncestorsAndSelf().OfType<BaseNamespaceDeclarationSyntax>().Reverse()
                .SelectMany(static part => part.Name.ToString().Split('.'))
                .Select(static part => part.Trim().TrimStart('@'));

            return String.Join(".", parts);
        }
    }
}
