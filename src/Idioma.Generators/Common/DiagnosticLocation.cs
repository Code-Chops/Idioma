using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// A source location to carry through the pipeline: the syntax tree it lies in, and its span there.
/// </summary>
/// <remarks>
/// It keeps the tree, because only a location inside a tree is reached by pragmas, <c>[SuppressMessage]</c>,
/// <c>.editorconfig</c> severities and code fixes.
/// </remarks>
// A tree compares by reference, so an edit in its file makes every step holding a location run again. So generated code
// comes from models without a location, and discovery keeps a set's locations apart (SetLocations).
internal readonly record struct DiagnosticLocation(SyntaxTree? Tree, TextSpan Span)
{
    public static DiagnosticLocation From(Location location)
    {
        var from = new DiagnosticLocation(location.SourceTree, location.SourceSpan);
        return from;
    }

    /// <summary>
    /// The location inside its tree, or <see cref="Location.None"/> without one.
    /// </summary>
    public Location ToLocation()
    {
        var location = this.Tree is null
            ? Location.None
            : Location.Create(this.Tree, this.Span);

        return location;
    }
}
