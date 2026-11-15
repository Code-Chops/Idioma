namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads a text that takes values: a method of a default-language set, such as
/// <c>public string Greeting(string name) => $"Hello, {name}!";</c>. The generated contract and class repeat its
/// parameters, so each is read with its type as generated code can write it.
/// </summary>
internal static class TextParameters
{
    /// <summary>
    /// Whether a member has the shape of a text that takes values: a public instance method, declared as a method,
    /// that returns a <see cref="string"/> and takes at least one parameter. A method without parameters is no text,
    /// as a text without values is a property.
    /// </summary>
    public static bool IsTextMethod(ISymbol member, out IMethodSymbol method)
    {
        method = (member as IMethodSymbol)!;

        var isTextMethod = member is IMethodSymbol
        {
            MethodKind: MethodKind.Ordinary,
            DeclaredAccessibility: Accessibility.Public,
            IsStatic: false,
            ReturnsByRef: false,
            ReturnsByRefReadonly: false,
            Parameters.Length: > 0,
            ReturnType.SpecialType: SpecialType.System_String,
        };

        return isTextMethod;
    }

    /// <summary>
    /// Whether a class declares no other member of a method's name. Only then can a doc comment link to the method by
    /// its name alone, and only then is the name a key of its own.
    /// </summary>
    public static bool HasNameOfItsOwn(IMethodSymbol method)
    {
        var hasNameOfItsOwn = method.ContainingType.GetMembers(method.Name).Length is 1;
        return hasNameOfItsOwn;
    }

    /// <summary>
    /// What keeps a method of the shape of a text (<see cref="IsTextMethod"/>) from being one, as the clause of the
    /// <c>LOC019</c> message, or <see langword="null"/> when nothing does.
    /// </summary>
    // Each of these would not compile in the generated class, or would compile into something else than the set says:
    // the class repeats the parameters, keeps the values in the text, and hands them to the set of the user's language
    // when the text is shown.
    public static string? FindProblem(IMethodSymbol method)
    {
        if (method.IsGenericMethod)
            return "it is generic";

        if (!HasNameOfItsOwn(method))
            return "its set has another method of that name, and a text is told from the others by its name";

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind is not RefKind.None)
                return $"its parameter '{parameter.Name}' is passed by reference, and a text keeps its values until it is shown";

            if (parameter.IsParams)
                return $"its parameter '{parameter.Name}' is a params parameter";

            if (parameter.IsOptional)
                return $"its parameter '{parameter.Name}' has a default value, which another language's set could give another value";

            if (parameter.Type is { IsRefLikeType: true } or IPointerTypeSymbol or IFunctionPointerTypeSymbol)
                return $"its parameter '{parameter.Name}' is of a type a text cannot keep until it is shown: a pointer, or a ref struct such as a span";

            if (parameter.Type.TypeKind is TypeKind.Dynamic)
                return $"its parameter '{parameter.Name}' is dynamic";
        }

        return null;
    }

    /// <summary>
    /// The parameters of a method, in their order.
    /// </summary>
    public static EquatableArray<TextParameter> Read(IMethodSymbol method)
    {
        var parameters = ImmutableArray.CreateBuilder<TextParameter>(method.Parameters.Length);

        foreach (var parameter in method.Parameters)
            parameters.Add(new TextParameter(parameter.Name, ReadType(parameter.Type)));

        return new EquatableArray<TextParameter>(parameters.MoveToImmutable());
    }

    /// <summary>
    /// A type as generated code can write it, in parts: each part starts at a type that lies in a namespace, and holds
    /// what follows it up to the next such type.
    /// </summary>
    private static EquatableArray<TypeNamePart> ReadType(ITypeSymbol type)
    {
        var parts = new List<(string? Namespace, StringBuilder Text)>();
        Write(type, parts);

        return new EquatableArray<TypeNamePart>([.. parts.Select(static part => new TypeNamePart(part.Namespace, part.Text.ToString()))]);
    }

    private static void Write(ITypeSymbol type, List<(string? Namespace, StringBuilder Text)> parts)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                WriteArray(array, parts);
                return;

            // int? is Nullable<int>, and is written as the type it wraps.
            case INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] }:
                Write(underlying, parts);
                Append(parts, "?");
                return;

            // A type that binds to nothing, as one another generator writes: by the words it is written in.
            case IErrorTypeSymbol:
                Append(parts, type.ToDisplayString());
                break;

            case INamedTypeSymbol { IsTupleType: true, TupleElements: { IsDefaultOrEmpty: false } elements }:
                WriteTuple(elements, parts);
                break;

            case INamedTypeSymbol named when GetKeyword(named) is { } keyword:
                Append(parts, keyword);
                break;

            case INamedTypeSymbol named:
                WriteNamed(named, parts);
                break;

            default:
                Append(parts, type.ToDisplayString());
                break;
        }

        if (type is { NullableAnnotation: NullableAnnotation.Annotated, IsValueType: false })
            Append(parts, "?");
    }

    /// <summary>
    /// Writes an array as C# does: the type its innermost arrays hold, then the brackets from the outermost array in.
    /// An array that may be <see langword="null"/> inside another is written as a type of its own, so that its
    /// <c>?</c> stands behind its own brackets.
    /// </summary>
    private static void WriteArray(IArrayTypeSymbol array, List<(string? Namespace, StringBuilder Text)> parts)
    {
        var element = array.ElementType;
        while (element is IArrayTypeSymbol { NullableAnnotation: not NullableAnnotation.Annotated } inner)
            element = inner.ElementType;

        Write(element, parts);

        for (ITypeSymbol current = array; current is IArrayTypeSymbol level && !ReferenceEquals(current, element); current = level.ElementType)
            Append(parts, $"[{new string(',', level.Rank - 1)}]");

        if (array.NullableAnnotation is NullableAnnotation.Annotated)
            Append(parts, "?");
    }

    /// <summary>
    /// Writes a tuple with the names its elements are given: a set implementing the contract must name them alike.
    /// </summary>
    private static void WriteTuple(ImmutableArray<IFieldSymbol> elements, List<(string? Namespace, StringBuilder Text)> parts)
    {
        Append(parts, "(");

        for (var i = 0; i < elements.Length; i++)
        {
            if (i > 0)
                Append(parts, ", ");

            Write(elements[i].Type, parts);

            if (elements[i].IsExplicitlyNamedTupleElement)
                Append(parts, $" {TranslationSetsGenerator.EscapeIdentifier(elements[i].Name)}");
        }

        Append(parts, ")");
    }

    /// <summary>
    /// Writes a class, struct, interface, enum, or delegate by its name: inside the types that hold it, with its type
    /// arguments, and starting a part of its own for the namespace it lies in.
    /// </summary>
    private static void WriteNamed(INamedTypeSymbol type, List<(string? Namespace, StringBuilder Text)> parts)
    {
        if (type.ContainingType is { } container)
        {
            WriteNamed(container, parts);
            Append(parts, ".");
        }
        else
        {
            parts.Add((TranslationsDiscovery.GetNamespaceName(type.ContainingNamespace), new StringBuilder()));
        }

        Append(parts, TranslationSetsGenerator.EscapeIdentifier(type.Name));

        if (type.TypeArguments.Length is 0)
            return;

        Append(parts, "<");

        for (var i = 0; i < type.TypeArguments.Length; i++)
        {
            if (i > 0)
                Append(parts, ", ");

            Write(type.TypeArguments[i], parts);
        }

        Append(parts, ">");
    }

    private static void Append(List<(string? Namespace, StringBuilder Text)> parts, string text)
    {
        if (parts.Count is 0)
            parts.Add((null, new StringBuilder()));

        parts[parts.Count - 1].Text.Append(text);
    }

    /// <summary>
    /// The keyword C# has for a type, such as <c>string</c> for <see cref="string"/>, or <see langword="null"/> for a
    /// type without one. A keyword names its type wherever it stands, so it needs no namespace.
    /// </summary>
    private static string? GetKeyword(INamedTypeSymbol type)
    {
        var keyword = type.SpecialType switch
        {
            SpecialType.System_Object => "object",
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Char => "char",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Byte => "byte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_String => "string",
            SpecialType.System_IntPtr when type.IsNativeIntegerType => "nint",
            SpecialType.System_UIntPtr when type.IsNativeIntegerType => "nuint",
            _ => null,
        };

        return keyword;
    }
}

/// <summary>
/// A parameter of a text that takes values: its name, and its type as generated code can write it.
/// </summary>
internal readonly record struct TextParameter(
    string Name,
    EquatableArray<TypeNamePart> Type);

/// <summary>
/// A part of a type's name as generated code writes it: the namespace the type at its start lies in (<c>""</c> for the
/// global namespace), and the text from that type's own name on, up to the next type that lies in a namespace.
/// <c>IReadOnlyList&lt;Order?&gt;</c> is two parts: <c>IReadOnlyList&lt;</c> of <c>System.Collections.Generic</c>,
/// and <c>Order?&gt;</c> of the namespace of <c>Order</c>. A part without a namespace is text alone, such as the
/// keyword <c>string</c> or the parenthesis a tuple starts with.
/// </summary>
internal readonly record struct TypeNamePart(
    string? Namespace,
    string Text);
