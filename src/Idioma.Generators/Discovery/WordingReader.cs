using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads the wordings of the members of one class and the classes it derives from, each once, and with one semantic
/// model per file. Lives within one transform only, so it may hold a semantic model. The model of another file than
/// its own is shared by every reader of the compilation.
/// </summary>
// Discovery reads every text of every set again on every edit, in any file. A wording written as a string is read from
// its syntax, which binds nothing. What needs binding is bound in one model per file, which keeps what it bound, where
// a model per member would bind the file's usings and the class anew for each. A default-language set shows a wording
// twice, as the default wording of its text and next to the link to it, and reads it once.
internal sealed class WordingReader(Compilation compilation, SemanticModel? semanticModel = null)
{
    /// <summary>
    /// Per compilation, the semantic model of each file a reader asked for besides its own.
    /// </summary>
    // Every set that derives from another reads the file of that other set, and a model made for each of them would
    // bind that file's usings and classes once per deriving set. A model answers from several threads at once, and
    // gives the same answers as any other model of its file. Held by the compilation, so the models go when it does.
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<SyntaxTree, SemanticModel?>> SharedModels = new();

    private ConcurrentDictionary<SyntaxTree, SemanticModel?>? Models { get; set; }

    private Dictionary<SyntaxNode, string?> Wordings { get; } = [];

    /// <summary>
    /// The semantic model of a tree, the same one each time, or <see langword="null"/> for a tree the compilation
    /// doesn't hold.
    /// </summary>
    // A member of another assembly has its syntax here only where an IDE references that project as a compilation, and
    // then in a tree this compilation has no semantic model for. Left unread, as in a build, which references the
    // project's assembly and sees no syntax at all.
    public SemanticModel? FindModel(SyntaxTree tree)
    {
        if (tree == semanticModel?.SyntaxTree)
            return semanticModel;

        this.Models ??= SharedModels.GetOrCreateValue(compilation);

        if (!this.Models.TryGetValue(tree, out var model))
            model = this.Models.GetOrAdd(tree, compilation.ContainsSyntaxTree(tree) ? compilation.GetSemanticModel(tree) : null);

        return model;
    }

    /// <summary>
    /// The constant a property returns, or the wording a method writes (<see cref="MethodWording"/>), when this
    /// compilation holds its declaration, else <see langword="null"/>.
    /// </summary>
    public string? Read(ISymbol member, CancellationToken cancellationToken)
    {
        // A partial member is read where it's implemented, because the part that declares it has no body.
        var written = LocalizationDiscovery.GetImplementingPart(member);

        var wording = written.DeclaringSyntaxReferences.Length > 0
            ? this.Read(written.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken), cancellationToken)
            : null;

        return wording;
    }

    /// <summary>
    /// The wording of a property when it is a constant (<see cref="ConstantWording"/>), or the wording a method writes
    /// (<see cref="MethodWording"/>), else <see langword="null"/>.
    /// </summary>
    public string? Read(SyntaxNode? declaration, CancellationToken cancellationToken)
    {
        if (declaration is not (PropertyDeclarationSyntax or MethodDeclarationSyntax))
            return null;

        if (this.Wordings.TryGetValue(declaration, out var wording))
            return wording;

        // The model only when the wording asks for it: a wording written as a string needs none.
        wording = this.FindModel(declaration.SyntaxTree) is not { } model
            ? null
            : declaration is PropertyDeclarationSyntax property
                ? ConstantWording.Read(property, model, cancellationToken)
                : MethodWording.Read((MethodDeclarationSyntax)declaration, model, cancellationToken);

        this.Wordings[declaration] = wording;

        return wording;
    }
}
