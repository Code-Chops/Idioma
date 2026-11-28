namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes the types a contract's file names, by their name alone where the file can, and else by their full name.
/// </summary>
internal static class ContractTypeNames
{
    /// <summary>
    /// The namespace and a dot, or nothing for the contract's own namespace or the global one.
    /// </summary>
    public static string GetNamespacePrefix(string contractNamespace, string typeNamespace)
    {
        var isOwnNamespace = typeNamespace.Length is 0 || String.Equals(typeNamespace, contractNamespace, StringComparison.Ordinal);
        var prefix = isOwnNamespace ? "" : $"{CodeEscaping.EscapeNamespace(typeNamespace)}.";
        return prefix;
    }

    /// <summary>
    /// The parameters of a text that takes values, as the contract and the generated class declare them.
    /// </summary>
    public static string RenderParameters(LocalizationMember member, string @namespace)
    {
        var parameters = String.Join(", ", member.Parameters.Select(parameter
            => $"{RenderType(parameter.Type, @namespace)} {CodeEscaping.EscapeIdentifier(parameter.Name)}"));

        return parameters;
    }

    /// <summary>
    /// A parameter's type as the contract's file names it, as <see cref="ContractDocComments.GetCrefType"/> does, such
    /// as <c>System.DateOnly</c>.
    /// </summary>
    public static string RenderType(EquatableArray<TypeNamePart> type, string @namespace)
    {
        var name = new StringBuilder();

        foreach (var part in type)
        {
            if (part.Namespace is { } typeNamespace)
                name.Append(GetNamespacePrefix(@namespace, typeNamespace));

            name.Append(part.Text);
        }

        return name.ToString();
    }
}
