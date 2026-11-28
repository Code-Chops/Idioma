using CodeChops.Idioma.Generators;

namespace CodeChops.Idioma.CodeFixes;

/// <inheritdoc cref="MissingLanguageCodeFixProvider"/>
public sealed partial class MissingLanguageCodeFixProvider
{
    /// <summary>
    /// A set to add, whose file goes next to <c>Document</c>. <c>Declaration</c> has no body, and names each type as the
    /// diagnostic does, which <see cref="NameTypes"/> shortens once the fix is chosen.
    /// </summary>
    private sealed record NewSet(Document Document, string Name, string Namespace, string Declaration, string FileName, string? FilePath)
    {
        /// <summary>
        /// The declaration with each type named as the new file reaches it, with the usings that takes
        /// (<see cref="NameTypesAsFileCan"/>).
        /// </summary>
        // Not when the light bulb asks, because each name tried compiles the project with the new file once more.
        public (string Declaration, ImmutableSortedSet<string> Usings) NameTypes(Compilation compilation, CancellationToken cancellationToken)
        {
            var named = NameTypesAsFileCan(this.Declaration, this.Namespace, compilation, cancellationToken);
            return named;
        }

        /// <summary>
        /// The set a diagnostic asks for, or <see langword="null"/> when it names none or the name is taken.
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
            var set = new NewSet(document, name, @namespace, declaration, fileName, filePath);

            if (compilation.GetTypeByMetadataName(@namespace.Length > 0 ? $"{@namespace}.{name}" : name) is not null || IsTaken(document.Project, set))
                return null;

            return set;
        }

        /// <summary>
        /// The declaration with each fully named type renamed as the new file reaches it, with the usings it then needs.
        /// Each name is the first that binds of: the name alone, the name alone with a <see langword="using"/>, and the
        /// full name.
        /// </summary>
        /// <remarks>
        /// The full name comes last, because a namespace around the set can take its first part:
        /// <c>Shop.IShopLocalization</c> means <c>Contoso.Shop.IShopLocalization</c> inside <c>namespace Contoso.Shop</c>.
        /// A type the compilation lacks, such as a generated contract, stays as written.
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
                // A contract, or the enum of ILocalizationSet<Shop.OrderStatus>.
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

                    // The full name, until one binds or when none does.
                    names[i] = around(written.ToString());
                }
            }

            return ($"{head}: {String.Join(", ", names)}", usings);
        }

        /// <summary>
        /// The metadata name, without the <c>@</c> that escapes a keyword.
        /// </summary>
        private static string ToMetadataName(QualifiedNameSyntax name)
        {
            var metadataName = String.Join(".", name.ToString().Split('.').Select(static part => part.Trim().TrimStart('@')));
            return metadataName;
        }

        /// <summary>
        /// Whether a base type of the new file, or its type argument, binds to <paramref name="type"/>. Only the argument
        /// is checked, because the package's using is added later, so <c>ILocalizationSet&lt;TEnum&gt;</c> doesn't bind yet.
        /// </summary>
        private static bool Binds(string file, int index, INamedTypeSymbol type, Compilation compilation, CancellationToken cancellationToken)
        {
            var tree = CSharpSyntaxTree.ParseText(file, compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions, cancellationToken: cancellationToken);
            var model = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);

            if (tree.GetRoot(cancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault() is not { BaseList.Types: var types } || index >= types.Count)
                return false;

            var written = types[index].Type is GenericNameSyntax { TypeArgumentList.Arguments: [var argument] } ? argument : types[index].Type;

            var binds = IsSameType(model.GetSymbolInfo(written, cancellationToken).Symbol, type);
            return binds;
        }

        /// <summary>
        /// Whether a type bound in the compilation with the new file is <paramref name="type"/> of the compilation without
        /// it: the same full name in the same assembly.
        /// </summary>
        // Not by symbol, because the compilation with the new file declares the project's own types anew, and a type of
        // one compilation never equals one of another. Only a type of a reference is shared.
        private static bool IsSameType(ISymbol? bound, INamedTypeSymbol type)
        {
            var isSame = bound is INamedTypeSymbol named
                         && String.Equals(named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparison.Ordinal)
                         && String.Equals(named.ContainingAssembly?.Name, type.ContainingAssembly?.Name, StringComparison.Ordinal);

            return isSame;
        }

        /// <summary>
        /// The document's first namespace, else the project's default namespace, else empty.
        /// </summary>
        private static async Task<string> FindNamespaceAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var declared = root?.DescendantNodes(static node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault();

            if (declared is null)
                return document.Project.DefaultNamespace ?? "";

            // The outermost, which the walk meets first.
            var parts = declared.Name.ToString().Split('.').Select(static part => part.Trim().TrimStart('@'));

            return String.Join(".", parts);
        }
    }
}
