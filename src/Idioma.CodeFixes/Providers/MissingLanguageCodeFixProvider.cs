using System.Composition;
using System.Text;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a contract without a set for one of the languages (<c>LOC201</c>). It adds the set the message names, in a file
/// of its own next to the file the diagnostic is in: <c>ShopLocalizationDe.cs</c> with
/// <c>public class ShopLocalizationDe : ILocalizationSet, IShopLocalization</c>. A region of a language that has a set
/// derives from that set instead, as in <c>public class ShopLocalizationEnGb : ShopLocalizationEn</c>.
/// </summary>
/// <remarks>
/// The new set has no texts. The compiler then names each text the set lacks (<c>CS0535</c>), and the IDE implements
/// them, so a translation is never filled in with the wording of another language. The fix isn't offered when a type of
/// that name exists, or a file of that name lies next to the diagnostic, because that class then needs changing, not
/// adding.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingLanguageCodeFixProvider))]
[Shared]
public sealed class MissingLanguageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizationSetsGenerator.MissingLanguageDiagnosticId];

    // None: a fix that adds a document doesn't merge with the batch fixer.
    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var compilation = await document.Project.GetCompilationAsync(context.CancellationToken).ConfigureAwait(false);

        if (compilation is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(LocalizationSetsGenerator.SuggestedNameProperty, out var name) || name is null
                || !diagnostic.Properties.TryGetValue(LocalizationSetsGenerator.SuggestedNamespaceProperty, out var @namespace) || @namespace is null
                || !diagnostic.Properties.TryGetValue(LocalizationSetsGenerator.SuggestedDeclarationProperty, out var declaration) || declaration is null)
            {
                continue;
            }

            var fileName = $"{name}.cs";
            var filePath = document.FilePath is { } path && Path.GetDirectoryName(path) is { } directory ? Path.Combine(directory, fileName) : null;

            if (compilation.GetTypeByMetadataName(@namespace.Length > 0 ? $"{@namespace}.{name}" : name) is not null
                || (filePath is not null && document.Project.Documents.Any(other => String.Equals(other.FilePath, filePath, StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Add '{name}'",
                    createChangedSolution: async cancellationToken =>
                    {
                        var newLine = (await document.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString().Contains("\r\n") ? "\r\n" : "\n";
                        var text = Write(@namespace, declaration, ImportsPackage(compilation, declaration), newLine);

                        return document.Project.Solution.AddDocument(DocumentId.CreateNewId(document.Project.Id), fileName, text, document.Folders, filePath);
                    },
                    equivalenceKey: $"AddSet:{name}"),
                diagnostic);
        }
    }

    /// <summary>
    /// The file of the new set: the package's namespace where the set lists <c>ILocalizationSet</c> and no global using
    /// imports it, the set's namespace, and the set without texts.
    /// </summary>
    private static string Write(string @namespace, string declaration, bool importsPackage, string newLine)
    {
        var text = new StringBuilder();

        if (importsPackage)
            text.Append($"using {PackageNames.Namespace};").Append(newLine).Append(newLine);

        if (@namespace.Length > 0)
        {
            var escaped = String.Join(".", @namespace.Split('.').Select(static part => SyntaxFacts.GetKeywordKind(part) is SyntaxKind.None ? part : $"@{part}"));
            text.Append($"namespace {escaped};").Append(newLine).Append(newLine);
        }

        text.Append(declaration).Append(newLine)
            .Append('{').Append(newLine)
            .Append('}').Append(newLine);

        return text.ToString();
    }

    /// <summary>
    /// Whether the new file imports the package's namespace itself: where the set lists <c>ILocalizationSet</c>, and no
    /// global using of the project imports that namespace already.
    /// </summary>
    private static bool ImportsPackage(Compilation compilation, string declaration)
    {
        if (!declaration.Contains("ILocalizationSet"))
            return false;

        foreach (var tree in compilation.SyntaxTrees)
        {
            if (tree.GetRoot() is not CompilationUnitSyntax root)
                continue;

            foreach (var directive in root.Usings)
            {
                if (directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && directive.StaticKeyword.IsKind(SyntaxKind.None) && directive.Alias is null
                    && String.Equals(directive.Name?.ToString(), PackageNames.Namespace, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
