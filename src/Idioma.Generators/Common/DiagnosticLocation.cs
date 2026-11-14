using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// A source location to carry through the pipeline: the syntax tree it lies in, and its span there.
/// </summary>
/// <remarks>
/// The tree, and not only its path and line numbers: a diagnostic at a location inside its tree is one that
/// <c>#pragma warning disable</c>, <c>[SuppressMessage]</c>, and the severities of an <c>.editorconfig</c> apply to, and
/// one a code fix finds its document by. A location made from a path alone looks the same in the error list, but none
/// of those reach it.
/// </remarks>
// A tree compares by reference, and stays the same object for as long as its file is not edited, so a pipeline step
// holding a location is reused across edits in other files. An edit in the file itself gives a new tree, and with it a
// new location, also where nothing moved: the steps holding one then run again. So what a generator writes comes from
// models without a location (the contracts, the recorded sets, the registrar, the languages of the factory), and only
// reporting runs again.
internal readonly record struct DiagnosticLocation(SyntaxTree? Tree, TextSpan Span)
{
    public static DiagnosticLocation From(Location location)
    {
        var from = new DiagnosticLocation(location.SourceTree, location.SourceSpan);
        return from;
    }

    /// <summary>
    /// The location inside its tree, or no location for one that lies in no tree, such as the default value.
    /// </summary>
    public Location ToLocation()
    {
        var location = this.Tree is null ? Location.None : Location.Create(this.Tree, this.Span);
        return location;
    }
}
