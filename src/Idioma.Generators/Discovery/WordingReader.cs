using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads the wordings of a class and its base classes, each once, with one semantic model per file. Lives within one
/// transform only, so it may hold a semantic model.
/// </summary>
// A wording written as a string is read from its syntax, which binds nothing. One model per file keeps what it bound,
// so the file's usings aren't bound anew for each member.
internal sealed class WordingReader(Compilation compilation, SemanticModel? semanticModel = null)
{
    /// <summary>
    /// Per compilation, the semantic model of each file a reader asked for besides its own.
    /// </summary>
    // Shared, because every set that derives from another reads that other set's file. A model is thread-safe and
    // answers as any other model of its file would. The compilation holds them, so they go when it does.
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<SyntaxTree, SemanticModel?>> SharedModels = new();

    private ConcurrentDictionary<SyntaxTree, SemanticModel?>? Models { get; set; }

    private Dictionary<SyntaxNode, string?> Wordings { get; } = [];

    /// <summary>
    /// The semantic model of a tree, the same one each time, or <see langword="null"/> for a tree the compilation
    /// doesn't hold.
    /// </summary>
    // An IDE can show the syntax of another project's member in a tree this compilation can't model. It's left unread,
    // as in a build, which sees no syntax of it at all.
    public SemanticModel? FindModel(SyntaxTree tree)
    {
        // A result read from the file depends on it (TransformCache).
        TransformCache.RecordRead(tree);

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

        // A wording written as a string needs no model.
        wording = this.FindModel(declaration.SyntaxTree) is not { } model
            ? null
            : declaration is PropertyDeclarationSyntax property
                ? ConstantWording.Read(property, model, cancellationToken)
                : MethodWording.Read((MethodDeclarationSyntax)declaration, model, cancellationToken);

        this.Wordings[declaration] = wording;

        return wording;
    }
}
