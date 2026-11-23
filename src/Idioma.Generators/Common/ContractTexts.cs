namespace CodeChops.Idioma.Generators;

/// <summary>
/// Tells which members of a set are texts: those that implement a text of a contract.
/// </summary>
// One place, so that every analyzer that reads the texts of a set reads the same members.
internal static class ContractTexts
{
    /// <summary>
    /// Whether a property or method of a set implements a text of a contract, or overrides the implementation of one:
    /// explicitly, or as the member the set implements the text through.
    /// </summary>
    /// <remarks>
    /// A public property counts too when the contract has a method by its name that the set doesn't implement. It's
    /// that text's wording in a language still written as a property, after the default language made the text a
    /// method.
    /// </remarks>
    /// <param name="member">The property or method.</param>
    /// <param name="set">The class that declares the member.</param>
    public static bool ImplementsText(ISymbol member, INamedTypeSymbol set)
    {
        if (FindImplementedTexts(member, set).Any())
            return true;

        // A property by the name of a text that takes values, which the set doesn't implement: the wording of that text,
        // written as the default language wrote it before it took values.
        var isWordingOfMethod = GetDeclaringPart(member) is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, ExplicitInterfaceImplementations.IsEmpty: true } property
                                && set.AllInterfaces
                                    .Where(PackageNames.IsContract)
                                    .SelectMany(contract => contract.GetMembers(property.Name))
                                    .Any(text => text is IMethodSymbol && set.FindImplementationForInterfaceMember(text) is null);

        return isWordingOfMethod;
    }

    /// <summary>
    /// The texts of the contracts a property or method of a set implements, or overrides the implementation of: those it
    /// implements explicitly, else those the set implements through it.
    /// </summary>
    /// <param name="member">The property or method.</param>
    /// <param name="set">The class that declares the member.</param>
    public static IEnumerable<ISymbol> FindImplementedTexts(ISymbol member, INamedTypeSymbol set)
    {
        member = GetDeclaringPart(member);

        var explicitly = member switch
        {
            IPropertySymbol property => property.ExplicitInterfaceImplementations.CastArray<ISymbol>(),
            IMethodSymbol method => method.ExplicitInterfaceImplementations.CastArray<ISymbol>(),
            _ => ImmutableArray<ISymbol>.Empty,
        };

        if (!explicitly.IsEmpty)
            return explicitly.Where(static text => PackageNames.IsContract(text.ContainingType));

        if (member.DeclaredAccessibility is not Accessibility.Public)
            return [];

        // Asked of the set which member implements the text, and not told by the name alone: a method of the same name
        // with other parameters implements nothing.
        var implementedTexts = set.AllInterfaces
            .Where(PackageNames.IsContract)
            .SelectMany(contract => contract.GetMembers(member.Name))
            .Where(text => IsOrOverrides(member, set.FindImplementationForInterfaceMember(text)));

        return implementedTexts;
    }

    /// <summary>
    /// The part of a partial member that declares it, for the part that implements it, which holds the wording. The set
    /// names the declaring part as the implementation of a text.
    /// </summary>
    private static ISymbol GetDeclaringPart(ISymbol member)
    {
        var declaringPart = member switch
        {
            IMethodSymbol { PartialDefinitionPart: { } method } => method,
            IPropertySymbol { PartialDefinitionPart: { } property } => property,
            _ => member,
        };

        return declaringPart;
    }

    /// <summary>
    /// Whether a member is the one that implements a text, or overrides it. A set deriving from another names the
    /// member of that set as the implementation, and its own member is what words the text.
    /// </summary>
    private static bool IsOrOverrides(ISymbol member, ISymbol? implementation)
    {
        for (var current = member; current is not null; current = current switch
                 {
                     IPropertySymbol property => property.OverriddenProperty,
                     IMethodSymbol method => method.OverriddenMethod,
                     _ => null,
                 })
        {
            if (SymbolEqualityComparer.Default.Equals(current, implementation))
                return true;
        }

        return false;
    }
}
