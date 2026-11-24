namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes the types the file of a contract names: each by its name alone where the file can, and by its full name
/// elsewhere.
/// </summary>
internal static class ContractTypeNames
{
    /// <summary>
    /// What the file of a contract writes in front of the name of a type: nothing for a type of its own namespace,
    /// where the file lies, or of the global namespace, and else the full name of the type's namespace and a dot.
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
    /// The type of a parameter as the file of a contract names it: like a class a cref names (<see cref="ContractDocComments.GetCrefType"/>),
    /// a type of the contract's own namespace by its name alone, and a type of another namespace by its full name, as
    /// in <c>System.DateOnly</c>.
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
