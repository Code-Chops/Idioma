using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// A source location as plain values: a <see cref="Location"/> compares by reference, so carrying one through the
/// pipeline would defeat incremental caching.
/// </summary>
internal readonly record struct DiagnosticLocation(
    string FilePath,
    int Start,
    int Length,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter)
{
    public static DiagnosticLocation From(Location location)
    {
        var span = location.GetLineSpan();

        return new DiagnosticLocation(
            FilePath: span.Path,
            Start: location.SourceSpan.Start,
            Length: location.SourceSpan.Length,
            StartLine: span.StartLinePosition.Line,
            StartCharacter: span.StartLinePosition.Character,
            EndLine: span.EndLinePosition.Line,
            EndCharacter: span.EndLinePosition.Character);
    }

    public Location ToLocation()
    {
        return Location.Create(
            this.FilePath,
            new TextSpan(this.Start, this.Length),
            new LinePositionSpan(new LinePosition(this.StartLine, this.StartCharacter), new LinePosition(this.EndLine, this.EndCharacter)));
    }
}
