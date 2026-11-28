namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads a text with parameters, a method of a default-language set such as
/// <c>public string Greeting(string name) => $"Hello, {name}!";</c>. The generated contract and class repeat its
/// parameters, so each is read with its type as generated code can write it.
/// </summary>
internal static class TextParameters
{
    /// <summary>
    /// The <c>LOC400</c> clause for a text written as a method without parameters (<see cref="IsTextWithoutParameters"/>).
    /// </summary>
    public const string WithoutParametersProblem = "it takes no parameters, so write it as a property";

    /// <summary>
    /// The <c>LOC400</c> clause for a method that takes <c>__arglist</c>, which the compiler counts as no parameters.
    /// </summary>
    public const string VarargProblem = "it takes __arglist, which the generated class can't pass on";

    /// <summary>
    /// Whether a member has the shape of a text with parameters: a public instance method that returns a
    /// <see cref="string"/> and takes at least one parameter. A method that takes <c>__arglist</c> counts too, so
    /// <see cref="FindProblem"/> reports it. A text without parameters is a property
    /// (<see cref="IsTextWithoutParameters"/>).
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
            ReturnType.SpecialType: SpecialType.System_String,
        } && (method.Parameters.Length > 0 || method.IsVararg);

        return isTextMethod;
    }

    /// <summary>
    /// Whether a member is a text written as a method without parameters, as in
    /// <c>public string Greeting() => "Hello";</c>. Code calling it as a text wouldn't compile, so <c>LOC400</c> says to
    /// write it as a property.
    /// </summary>
    /// <remarks>
    /// A method that plainly is no text is left alone:
    /// <list type="bullet">
    /// <item>an override or a method that hides a base member, such as <c>ToString()</c>;</item>
    /// <item>an interface implementation;</item>
    /// <item>a generic method, or one the compiler writes, as for a record.</item>
    /// </list>
    /// </remarks>
    public static bool IsTextWithoutParameters(ISymbol member, out IMethodSymbol method)
    {
        method = (member as IMethodSymbol)!;

        if (member is not IMethodSymbol
            {
                MethodKind: MethodKind.Ordinary,
                DeclaredAccessibility: Accessibility.Public,
                IsStatic: false,
                IsOverride: false,
                IsGenericMethod: false,
                IsImplicitlyDeclared: false,
                ReturnsByRef: false,
                ReturnsByRefReadonly: false,
                IsVararg: false,
                Parameters.Length: 0,
                ReturnType.SpecialType: SpecialType.System_String,
            })
        {
            return false;
        }

        var set = method.ContainingType;

        // A base member the method hides, such as ToString() written with new.
        for (var baseType = set.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (!baseType.GetMembers(method.Name).IsEmpty)
                return false;

        foreach (var @interface in set.AllInterfaces)
            foreach (var implemented in @interface.GetMembers(method.Name))
                if (SymbolEqualityComparer.Default.Equals(set.FindImplementationForInterfaceMember(implemented), method))
                    return false;

        return true;
    }

    /// <summary>
    /// Whether a class declares no other member of a method's name, so a cref and a key can name it alone.
    /// </summary>
    public static bool HasNameOfItsOwn(IMethodSymbol method)
    {
        var hasNameOfItsOwn = method.ContainingType.GetMembers(method.Name).Length is 1;
        return hasNameOfItsOwn;
    }

    /// <summary>
    /// What keeps a text method (<see cref="IsTextMethod"/>) from being a text, as the <c>LOC400</c> clause, or
    /// <see langword="null"/>.
    /// </summary>
    // The generated class repeats the parameters and hands the values on, and each of these wouldn't compile there or
    // would mean something else.
    public static string? FindProblem(IMethodSymbol method)
    {
        if (method.IsVararg)
            return VarargProblem;

        if (method.IsGenericMethod)
            return "it's generic";

        if (!HasNameOfItsOwn(method))
            return "its set has another method of that name, and a text is told from the others by its name";

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind is not RefKind.None)
                return $"its parameter '{parameter.Name}' is passed by reference, and the generated class passes each value on by value";

            if (parameter.IsParams)
                return $"its parameter '{parameter.Name}' is a params parameter";

            if (parameter.IsOptional)
                return $"its parameter '{parameter.Name}' has a default value, which another language's set could give another value";

            if (parameter.Type is { IsRefLikeType: true } or IPointerTypeSymbol or IFunctionPointerTypeSymbol)
                return $"its parameter '{parameter.Name}' is of a type the generated class can't pass on: a pointer, or a ref struct such as a span";

            if (parameter.Type.TypeKind is TypeKind.Dynamic)
                return $"its parameter '{parameter.Name}' is dynamic";
        }

        return null;
    }

    /// <summary>
    /// The parameters of a method, each with whether its wording shows it (<see cref="FindShownParameters"/>).
    /// </summary>
    /// <param name="declaration">The declaration of the method, or <see langword="null"/> when it has none to read.</param>
    public static EquatableArray<TextParameter> Read(IMethodSymbol method, MethodDeclarationSyntax? declaration, SemanticModel? semanticModel, CancellationToken cancellationToken)
    {
        var shown = declaration is not null && semanticModel is not null
            ? FindShownParameters(method, declaration, semanticModel, cancellationToken)
            : new bool[method.Parameters.Length];

        var parameters = ImmutableArray.CreateBuilder<TextParameter>(method.Parameters.Length);

        foreach (var parameter in method.Parameters)
            parameters.Add(new TextParameter(
                parameter.Name,
                ReadType(parameter.Type),
                shown[parameter.Ordinal],
                AcceptsNull(parameter.Type)));

        return new EquatableArray<TextParameter>(parameters.MoveToImmutable());
    }

    /// <summary>
    /// Which parameters the wording of a method shows, by position: those it uses for more than choosing a wording.
    /// <c>count is 1 ? "1 item" : $"{count} items"</c> shows <c>count</c>, and <c>salutation switch { ... }</c> only
    /// chooses by <c>salutation</c>.
    /// </summary>
    // Other languages must show what the default wording shows (LOC402). It errs to the side of showing, so a value
    // handed to a helper counts as shown.
    private static bool[] FindShownParameters(IMethodSymbol method, MethodDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var shown = new bool[method.Parameters.Length];

        if ((declaration.ExpressionBody?.Expression ?? (SyntaxNode?)declaration.Body) is not { } body)
            return shown;

        HashSet<string>? redeclared = null;

        // The pattern variables that stand for a parameter, read only when needed.
        Dictionary<ISymbol, int>? patternVariables = null;

        foreach (var node in body.DescendantNodesAndSelf())
        {
            // By name first, which binds nothing. What nameof names isn't a value.
            if (node is not IdentifierNameSyntax identifier || IsNameOfArgument(identifier))
                continue;

            int ordinal;

            if (method.Parameters.Any(parameter => String.Equals(parameter.Name, identifier.Identifier.ValueText, StringComparison.Ordinal)))
            {
                // Then by syntax, and by the compiler only where syntax can't tell.
                if (FindParameterBySyntax(identifier, method, body, ref redeclared) is { } found)
                {
                    ordinal = found;
                }
                else if (semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol is IParameterSymbol parameter
                         && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, method))
                {
                    ordinal = parameter.Ordinal;
                }
                else
                {
                    continue;
                }
            }
            else
            {
                patternVariables ??= FindPatternVariables(method, body, semanticModel, cancellationToken);

                if (patternVariables.Count is 0
                    || !patternVariables.Keys.Any(variable => String.Equals(variable.Name, identifier.Identifier.ValueText, StringComparison.Ordinal))
                    || semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol is not ILocalSymbol local
                    || !patternVariables.TryGetValue(local, out ordinal))
                {
                    continue;
                }
            }

            if (!shown[ordinal])
                shown[ordinal] = !OnlyChooses(identifier, body);
        }

        return shown;
    }

    /// <summary>
    /// Which parameters the default wording of a hand-written contract's text shows, read from the default-language set
    /// in this compilation (<see cref="FindShownParameters"/>). <see langword="null"/> where the build can't tell.
    /// </summary>
    // A generated contract marks this with [ShownValue], and a hand-written one doesn't. The set is found among the
    // classes whose name ends in the default language.
    public static bool[]? FindShownByDefaultSet(IMethodSymbol text, Compilation compilation, CancellationToken cancellationToken)
    {
        if (!SymbolEqualityComparer.Default.Equals(text.ContainingAssembly, compilation.Assembly)
            || SupportedLanguages.ReadCodes(compilation.Assembly) is not [var defaultLanguage, ..])
        {
            return null;
        }

        var reader = new WordingReader(compilation);

        foreach (var symbol in compilation.GetSymbolsWithName(name => SupportedLanguages.EndsInTypeSuffix(name, defaultLanguage), SymbolFilter.Type, cancellationToken))
        {
            if (symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } set
                || !set.AllInterfaces.Contains(text.ContainingType, SymbolEqualityComparer.Default)
                || set.FindImplementationForInterfaceMember(text) is not IMethodSymbol implementation
                || LocalizationDiscovery.GetImplementingPart(implementation) is not IMethodSymbol { DeclaringSyntaxReferences: [var reference, ..] } written
                || reference.GetSyntax(cancellationToken) is not MethodDeclarationSyntax declaration
                || reader.FindModel(declaration.SyntaxTree) is not { } model
                || !String.Equals(LocalizationDiscovery.ReadLanguage(set, compilation, cancellationToken), defaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return FindShownParameters(written, declaration, model, cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// The pattern variables that stand for a whole parameter, with its position: <c>n</c> in
    /// <c>count switch { 1 => "1 item", var n => $"{n} items" }</c>, and in <c>count is int n</c>.
    /// </summary>
    // Bound only for a switch or an is-pattern on a parameter whose patterns declare a variable.
    private static Dictionary<ISymbol, int> FindPatternVariables(IMethodSymbol method, SyntaxNode body, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);
        HashSet<string>? redeclared = null;

        foreach (var node in body.DescendantNodesAndSelf())
        {
            ExpressionSyntax input;
            IEnumerable<PatternSyntax> patterns;

            switch (node)
            {
                case SwitchExpressionSyntax @switch:
                    input = @switch.GoverningExpression;
                    patterns = @switch.Arms.Select(static arm => arm.Pattern);
                    break;
                case IsPatternExpressionSyntax @is:
                    input = @is.Expression;
                    patterns = [@is.Pattern];
                    break;
                case SwitchStatementSyntax @switch:
                    input = @switch.Expression;
                    patterns = @switch.Sections.SelectMany(static section => section.Labels.OfType<CasePatternSwitchLabelSyntax>()).Select(static label => label.Pattern);
                    break;
                default:
                    continue;
            }

            if (input is not IdentifierNameSyntax identifier
                || !method.Parameters.Any(parameter => String.Equals(parameter.Name, identifier.Identifier.ValueText, StringComparison.Ordinal)))
            {
                continue;
            }

            var designations = patterns.SelectMany(FindWholeValueDesignations).ToArray();
            if (designations.Length is 0)
                continue;

            var ordinal = FindParameterBySyntax(identifier, method, body, ref redeclared)
                          ?? (semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol is IParameterSymbol parameter
                              && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, method)
                              ? parameter.Ordinal
                              : null);

            if (ordinal is null)
                continue;

            foreach (var designation in designations)
                if (semanticModel.GetDeclaredSymbol(designation, cancellationToken) is { } variable)
                    variables[variable] = ordinal.Value;
        }

        return variables;
    }

    /// <summary>
    /// The variables a pattern declares for the whole value: <c>var n</c>, <c>int n</c> and <c>{ } n</c>, also in
    /// parentheses or with <see langword="and"/>. <c>{ Length: var length }</c> isn't one.
    /// </summary>
    private static IEnumerable<SingleVariableDesignationSyntax> FindWholeValueDesignations(PatternSyntax pattern)
    {
        var designation = pattern switch
        {
            VarPatternSyntax { Designation: SingleVariableDesignationSyntax variable } => variable,
            DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax variable } => variable,
            RecursivePatternSyntax { Designation: SingleVariableDesignationSyntax variable } => variable,
            _ => null,
        };

        if (designation is not null)
            return [designation];

        var designations = pattern switch
        {
            ParenthesizedPatternSyntax parenthesized => FindWholeValueDesignations(parenthesized.Pattern),
            BinaryPatternSyntax binary when binary.IsKind(SyntaxKind.AndPattern) => FindWholeValueDesignations(binary.Left).Concat(FindWholeValueDesignations(binary.Right)),
            _ => [],
        };

        return designations;
    }

    /// <summary>
    /// Whether a name is inside <c>nameof(...)</c>, which reads no value.
    /// </summary>
    private static bool IsNameOfArgument(IdentifierNameSyntax identifier)
    {
        var current = identifier.Parent;

        while (current is MemberAccessExpressionSyntax or QualifiedNameSyntax)
            current = current.Parent;

        var isNameOfArgument = current is ArgumentSyntax
        {
            Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } },
        };

        return isNameOfArgument;
    }

    /// <summary>
    /// The position of the parameter a name stands for, where the syntax alone tells: the name is read as a value, one
    /// parameter has it, and the body declares nothing else of that name. <see langword="null"/> where only the compiler
    /// can tell.
    /// </summary>
    // A name read as a value that nothing nearer redeclares is the parameter. This avoids binding the body, which costs
    // more than everything else read on every edit.
    internal static int? FindParameterBySyntax(IdentifierNameSyntax identifier, IMethodSymbol method, SyntaxNode body, ref HashSet<string>? redeclared)
    {
        var name = identifier.Identifier.ValueText;
        IParameterSymbol? found = null;

        foreach (var parameter in method.Parameters)
        {
            if (!String.Equals(parameter.Name, name, StringComparison.Ordinal))
                continue;

            // Two parameters of one name, which doesn't compile.
            if (found is not null)
                return null;

            found = parameter;
        }

        // A parameter named like its type (Color Color) may stand for the type.
        if (found is null || String.Equals(found.Type.Name, name, StringComparison.Ordinal) || !IsReadAsValue(identifier))
            return null;

        // Names the body declares itself, such as lambda parameters, collected once for all parameters.
        if (redeclared is null)
        {
            redeclared = new HashSet<string>(StringComparer.Ordinal);

            foreach (var token in body.DescendantTokens())
                if (token.IsKind(SyntaxKind.IdentifierToken) && token.Parent is not IdentifierNameSyntax)
                    redeclared.Add(token.ValueText);
        }

        return redeclared.Contains(name) ? null : found.Ordinal;
    }

    /// <summary>
    /// Whether a name stands where only a value can stand, and not a type, a member, or the name of an argument.
    /// </summary>
    private static bool IsReadAsValue(IdentifierNameSyntax identifier)
    {
        var isReadAsValue = identifier.Parent switch
        {
            InterpolationSyntax or ArgumentSyntax or ParenthesizedExpressionSyntax or ConditionalExpressionSyntax => true,
            IsPatternExpressionSyntax or SwitchExpressionSyntax or SwitchExpressionArmSyntax => true,
            ArrowExpressionClauseSyntax or ReturnStatementSyntax or PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
            BinaryExpressionSyntax binary => !binary.IsKind(SyntaxKind.IsExpression) && !binary.IsKind(SyntaxKind.AsExpression),
            MemberAccessExpressionSyntax member => member.Expression == identifier,
            _ => false,
        };

        return isReadAsValue;
    }

    /// <summary>
    /// Whether a value only chooses what is worded, as in a condition, a switch, a pattern or a comparison.
    /// </summary>
    private static bool OnlyChooses(SyntaxNode value, SyntaxNode body)
    {
        for (var current = value; current != body && current.Parent is { } parent; current = parent)
        {
            var chooses = parent switch
            {
                ConditionalExpressionSyntax conditional => conditional.Condition == current,
                SwitchExpressionSyntax @switch => @switch.GoverningExpression == current,
                SwitchExpressionArmSyntax arm => arm.Expression != current,
                SwitchStatementSyntax @switch => @switch.Expression == current,
                IfStatementSyntax @if => @if.Condition == current,
                WhileStatementSyntax @while => @while.Condition == current,
                DoStatementSyntax @do => @do.Condition == current,
                ForStatementSyntax @for => @for.Condition == current,
                SwitchLabelSyntax or WhenClauseSyntax or IsPatternExpressionSyntax => true,
                BinaryExpressionSyntax comparison => comparison.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
                    or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
                    or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression,
                _ => false,
            };

            if (chooses)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a parameter's type, or a part of it, doesn't bind yet, as one another generator writes, such as
    /// <c>IReadOnlyList&lt;OrderId&gt;</c>. It's copied as written, so the method's usings are repeated
    /// (<see cref="ReadUsings"/>).
    /// </summary>
    public static bool HasUnboundType(IMethodSymbol method)
    {
        var hasUnboundType = method.Parameters.Any(static parameter => IsUnbound(parameter.Type));
        return hasUnboundType;

        static bool IsUnbound(ITypeSymbol type)
        {
            var isUnbound = type switch
            {
                IErrorTypeSymbol => true,
                IArrayTypeSymbol array => IsUnbound(array.ElementType),
                INamedTypeSymbol named => named.TypeArguments.Any(IsUnbound),
                _ => false,
            };

            return isUnbound;
        }
    }

    /// <summary>
    /// The using directives in reach of a method, outermost first. A <see langword="global"/> one is left out, because it
    /// reaches every file already.
    /// </summary>
    public static (ImmutableArray<string> OfFile, ImmutableArray<string> OfNamespace) ReadUsings(MethodDeclarationSyntax declaration)
    {
        var ofFile = ImmutableArray.CreateBuilder<string>();
        var ofNamespace = ImmutableArray.CreateBuilder<string>();

        foreach (var ancestor in declaration.Ancestors().Reverse())
        {
            var (usings, target) = ancestor switch
            {
                CompilationUnitSyntax file => (file.Usings, ofFile),
                BaseNamespaceDeclarationSyntax @namespace => (@namespace.Usings, ofNamespace),
                _ => (default, ofFile),
            };

            foreach (var directive in usings)
                if (directive.GlobalKeyword.IsKind(SyntaxKind.None))
                    target.Add(directive.WithoutTrivia().NormalizeWhitespace().ToString());
        }

        return (ofFile.ToImmutable(), ofNamespace.ToImmutable());
    }

    /// <summary>
    /// Whether <c>GetTextByKey</c> accepts <see langword="null"/> for a parameter of the type. Only a non-annotated
    /// reference type in nullable code doesn't.
    /// </summary>
    private static bool AcceptsNull(ITypeSymbol type)
    {
        var acceptsNull = type is not { IsReferenceType: true, NullableAnnotation: NullableAnnotation.NotAnnotated };
        return acceptsNull;
    }

    /// <summary>
    /// A type as generated code can write it, in parts (<see cref="TypeNamePart"/>).
    /// </summary>
    internal static EquatableArray<TypeNamePart> ReadType(ITypeSymbol type)
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

            // int? is Nullable<int>.
            case INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] }:
                Write(underlying, parts);
                Append(parts, "?");
                return;

            // An unbound type is copied as written, and the contract repeats the method's usings (HasUnboundType).
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
    /// Writes an array as C# does: the element type, then the brackets from the outermost array in. A nullable inner
    /// array is written as a type of its own, so its <c>?</c> follows its own brackets.
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
    /// Writes a tuple with its element names, because a set implementing the contract must use the same.
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
                Append(parts, $" {CodeEscaping.EscapeIdentifier(elements[i].Name)}");
        }

        Append(parts, ")");
    }

    /// <summary>
    /// Writes a named type with its containing types and type arguments, starting a part for its namespace.
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
            parts.Add((LocalizationDiscovery.GetNamespaceName(type.ContainingNamespace), new StringBuilder()));
        }

        Append(parts, CodeEscaping.EscapeIdentifier(type.Name));

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
    /// The C# keyword for a type, such as <c>string</c>, or <see langword="null"/>. A keyword needs no namespace.
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
/// A parameter of a text. <see cref="IsShown"/> says whether the default wording shows it, which every other language is
/// held to (<c>LOC402</c>).
/// </summary>
internal readonly record struct TextParameter(
    string Name,
    EquatableArray<TypeNamePart> Type,
    bool IsShown,
    bool AcceptsNull);

/// <summary>
/// The using directives of the default-language set that its contract repeats, as written (<c>using MyApp.Domain;</c>).
/// Empty unless a parameter's type doesn't bind yet, because that type is copied as written.
/// </summary>
internal readonly record struct RepeatedUsings(
    EquatableArray<string> OfFile,
    EquatableArray<string> OfNamespace);

/// <summary>
/// A part of a type's name as generated code writes it: the namespace of the type it starts with, and the text up to
/// the next type in a namespace. <c>IReadOnlyList&lt;Order?&gt;</c> is <c>IReadOnlyList&lt;</c> of
/// <c>System.Collections.Generic</c> and <c>Order?&gt;</c> of <c>Order</c>'s namespace. A part without a namespace is
/// text alone, such as <c>string</c>.
/// </summary>
internal readonly record struct TypeNamePart(
    string? Namespace,
    string Text);
