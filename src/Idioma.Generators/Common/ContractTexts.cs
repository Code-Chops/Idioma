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
    /// <param name="localizationSet"><c>ILocalizationSet</c>, which is no contract itself.</param>
    public static bool ImplementsText(ISymbol member, INamedTypeSymbol set, INamedTypeSymbol localizationSet)
    {
        var explicitly = member switch
        {
            IPropertySymbol property => property.ExplicitInterfaceImplementations.CastArray<ISymbol>(),
            IMethodSymbol method => method.ExplicitInterfaceImplementations.CastArray<ISymbol>(),
            _ => ImmutableArray<ISymbol>.Empty,
        };

        if (!explicitly.IsEmpty)
            return explicitly.Any(text => IsContract(text.ContainingType, localizationSet));

        if (member.DeclaredAccessibility is not Accessibility.Public)
            return false;

        // Asked of the set which member implements the text, and not told by the name alone.
        foreach (var contract in set.AllInterfaces)
        {
            if (!IsContract(contract, localizationSet))
                continue;

            foreach (var text in contract.GetMembers(member.Name))
            {
                if (IsOrOverrides(member, set.FindImplementationForInterfaceMember(text)))
                    return true;

                // A property by the name of a text that takes values, which the set doesn't implement: the wording of
                // that text, written as the default language wrote it before it took values.
                if (member is IPropertySymbol && text is IMethodSymbol && set.FindImplementationForInterfaceMember(text) is null)
                    return true;
            }
        }

        return false;
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

    private static bool IsContract(INamedTypeSymbol @interface, INamedTypeSymbol localizationSet)
    {
        var isContract = !SymbolEqualityComparer.Default.Equals(@interface, localizationSet) && PackageNames.IsContract(@interface);
        return isContract;
    }
}
