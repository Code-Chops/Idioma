namespace CodeChops.Idioma.Generators;

/// <summary>
/// Tells which members of a set are texts, the same way for every analyzer.
/// </summary>
internal static class ContractTexts
{
    /// <summary>
    /// Whether a property or method of a set implements a text of a contract, or overrides its implementation.
    /// </summary>
    /// <param name="set">The class that declares the member.</param>
    public static bool ImplementsText(ISymbol member, INamedTypeSymbol set)
    {
        if (FindImplementedTexts(member, set).Any())
            return true;

        // A public property named like an unimplemented method of the contract is that text's wording, still written
        // as a property after the default language made the text take values.
        var isWordingOfMethod = GetDeclaringPart(member) is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, ExplicitInterfaceImplementations.IsEmpty: true } property
                                && set.AllInterfaces
                                    .Where(PackageNames.IsContract)
                                    .SelectMany(contract => contract.GetMembers(property.Name))
                                    .Any(text => text is IMethodSymbol && set.FindImplementationForInterfaceMember(text) is null);

        return isWordingOfMethod;
    }

    /// <summary>
    /// The contract texts a member implements explicitly, else those the set implements through it.
    /// </summary>
    /// <param name="set">The class that declares the member.</param>
    public static IEnumerable<ISymbol> FindImplementedTexts(ISymbol member, INamedTypeSymbol set)
    {
        member = GetDeclaringPart(member);

        var explicitly = GetExplicitImplementations(member);

        if (!explicitly.IsEmpty)
            return explicitly.Where(static text => PackageNames.IsContract(text.ContainingType));

        if (member.DeclaredAccessibility is not Accessibility.Public)
            return [];

        // Asked of the set, because a method of the same name with other parameters implements nothing.
        var implementedTexts = set.AllInterfaces
            .Where(PackageNames.IsContract)
            .SelectMany(contract => contract.GetMembers(member.Name))
            .Where(text => Overrides.IsOrOverrides(member, set.FindImplementationForInterfaceMember(text)));

        return implementedTexts;
    }

    /// <summary>
    /// Whether a property or method implements an interface member explicitly, as in
    /// <c>string IShopLocalization.Checkout => ...</c>, which can be neither virtual nor overridden.
    /// </summary>
    public static bool IsExplicitImplementation(ISymbol member)
    {
        return !GetExplicitImplementations(member).IsEmpty;
    }

    /// <summary>
    /// The interface members a property or method implements explicitly.
    /// </summary>
    private static ImmutableArray<ISymbol> GetExplicitImplementations(ISymbol member)
    {
        var explicitly = member switch
        {
            IPropertySymbol property => property.ExplicitInterfaceImplementations.CastArray<ISymbol>(),
            IMethodSymbol method => method.ExplicitInterfaceImplementations.CastArray<ISymbol>(),
            _ => ImmutableArray<ISymbol>.Empty,
        };

        return explicitly;
    }

    /// <summary>
    /// The declaring part of a partial member, which the set names as the implementation of a text.
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
}
