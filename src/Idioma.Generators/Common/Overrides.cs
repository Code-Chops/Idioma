namespace CodeChops.Idioma.Generators;

/// <summary>
/// Walks the chain of members that override one another, for the analyzers and the code fixes alike.
/// </summary>
internal static class Overrides
{
    /// <summary>
    /// The member a property, method or event overrides, or <see langword="null"/>.
    /// </summary>
    public static ISymbol? GetOverridden(ISymbol member)
    {
        var overridden = member switch
        {
            IPropertySymbol property => property.OverriddenProperty,
            IMethodSymbol method => method.OverriddenMethod,
            IEventSymbol @event => (ISymbol?)@event.OverriddenEvent,
            _ => null,
        };

        return overridden;
    }

    /// <summary>
    /// Whether a member is the other one, or overrides it directly or through members in between, as a rewording
    /// overrides the implementation of a text.
    /// </summary>
    public static bool IsOrOverrides(ISymbol member, ISymbol? other)
    {
        for (var current = member; current is not null; current = GetOverridden(current))
            if (SymbolEqualityComparer.Default.Equals(current, other))
                return true;

        return false;
    }

    /// <summary>
    /// The member at the start of the chain a member overrides, or <see langword="null"/> for a member that overrides
    /// nothing.
    /// </summary>
    public static ISymbol? FindFirstDeclaration(ISymbol member)
    {
        ISymbol? first = null;

        for (var current = GetOverridden(member); current is not null; current = GetOverridden(current))
            first = current;

        return first;
    }
}
