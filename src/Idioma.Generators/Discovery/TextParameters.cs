namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads a text that takes values: a method of a default-language set, such as
/// <c>public string Greeting(string name) => $"Hello, {name}!";</c>. The generated contract and class repeat its
/// parameters, so each is read with its type as generated code can write it.
/// </summary>
internal static class TextParameters
{
    /// <summary>
    /// What <c>LOC400</c> says of a text written as a method without parameters
    /// (<see cref="IsTextWithoutParameters"/>), as the clause of its message.
    /// </summary>
    public const string WithoutParametersProblem = "it takes no parameters, so write it as a property";

    /// <summary>
    /// Whether a member has the shape of a text that takes values: a public instance method, declared as a method,
    /// that returns a <see cref="string"/> and takes at least one parameter. A method without parameters is no text,
    /// as a text without values is a property (<see cref="IsTextWithoutParameters"/>).
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
    /// Whether a member is a text written as a method without parameters, as in
    /// <c>public string Greeting() => "Hello";</c>. It has the shape of a text that takes values
    /// (<see cref="IsTextMethod"/>) but takes none, so it is no text, and what calls it as one would not compile.
    /// <c>LOC400</c> says to write it as a property.
    /// </summary>
    /// <remarks>
    /// A method that plainly is no text is left alone:
    /// <list type="bullet">
    /// <item>An override, such as <c>ToString()</c>, or a method that hides a member of a class the set derives
    /// from.</item>
    /// <item>A method that implements a member of an interface.</item>
    /// <item>A generic method, or one the compiler writes, as for a record.</item>
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
                Parameters.Length: 0,
                ReturnType.SpecialType: SpecialType.System_String,
            })
        {
            return false;
        }

        var set = method.ContainingType;

        // A member of that name in a class the set derives from, which the method hides: ToString() written with new.
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
    /// <c>LOC400</c> message, or <see langword="null"/> when nothing does.
    /// </summary>
    // Each of these would not compile in the generated class, or would compile into something else than the set says:
    // the class repeats the parameters, and hands the values to the set of the user's language, through a function
    // where it has to find that set first.
    public static string? FindProblem(IMethodSymbol method)
    {
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
    /// The parameters of a method, in their order, each with whether the wording the method writes shows it
    /// (<see cref="FindShownParameters"/>).
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="declaration">The declaration of the method, or <see langword="null"/> when it has none to read.</param>
    /// <param name="semanticModel">The semantic model of the tree the declaration lies in.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
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
                new EquatableArray<string>(LocalizationDiscovery.GetSuppressedDiagnosticsOfType(parameter.Type)),
                AcceptsNull(parameter.Type)));

        return new EquatableArray<TextParameter>(parameters.MoveToImmutable());
    }

    /// <summary>
    /// Which parameters the wording of a method shows, by their position: those the method uses for more than choosing
    /// between wordings. <c>count is 1 ? "1 item" : $"{count} items"</c> shows <c>count</c>, and
    /// <c>salutation switch { Salutation.Madam => "Dear madam", _ => "Dear customer" }</c> only chooses by
    /// <c>salutation</c>.
    /// </summary>
    // What every other language is held to (LOC402): a value the default wording shows must be in theirs, and a value
    // it only chooses by may mean nothing in a language that makes no such distinction. Told from where the value
    // stands, which errs to the side of showing: a value handed on to a helper counts as shown.
    private static bool[] FindShownParameters(IMethodSymbol method, MethodDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var shown = new bool[method.Parameters.Length];

        if ((declaration.ExpressionBody?.Expression ?? (SyntaxNode?)declaration.Body) is not { } body)
            return shown;

        HashSet<string>? redeclared = null;

        // The pattern variables that stand for a parameter, read once, and only where a name may be one of them.
        Dictionary<ISymbol, int>? patternVariables = null;

        foreach (var node in body.DescendantNodesAndSelf())
        {
            // By its name first, which costs nothing: only a name a parameter carries is read, or that of a pattern
            // variable. What nameof names isn't read as a value.
            if (node is not IdentifierNameSyntax identifier || IsNameOfArgument(identifier))
                continue;

            int ordinal;

            if (method.Parameters.Any(parameter => String.Equals(parameter.Name, identifier.Identifier.ValueText, StringComparison.Ordinal)))
            {
                // Then by where it stands, and by the compiler only where that doesn't say which parameter it is.
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
    /// Which parameters the default wording of a text of a contract written by hand shows, by their position, read
    /// from the default-language set that implements it in this compilation (<see cref="FindShownParameters"/>).
    /// <see langword="null"/> where the build can't tell: the contract lies in another assembly, this one declares no
    /// languages, or no set of its default language here implements the text.
    /// </summary>
    // A generated contract marks this on the text ([ShownValue]). A contract written by hand marks nothing, so its
    // default-language set is read instead, which the build asks for in the contract's own assembly (LOC201). That set
    // is found among the classes whose name ends in the default language, as nearly every set's does.
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
    /// The pattern variables of a body that stand for a parameter as a whole, with the position of that parameter:
    /// <c>n</c> in <c>count switch { 1 => "1 item", var n => $"{n} items" }</c>, and in <c>count is int n</c>. Such a
    /// variable shows the value where it's worded, as the parameter would.
    /// </summary>
    // Bound only for a switch or an is-pattern on a parameter whose patterns declare a variable, which few bodies have.
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
    /// The variables a pattern declares for the value it matches as a whole: <c>var n</c>, <c>int n</c> and
    /// <c>{ } n</c>, also inside parentheses or next to another pattern with <see langword="and"/>. A variable of a part
    /// of the value, such as <c>{ Length: var length }</c>, isn't one.
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
    /// Whether a name stands in what <see langword="nameof"/> names, which reads no value: <c>name</c> in
    /// <c>nameof(name)</c>.
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
    /// The position of the parameter a name stands for, where the syntax says so by itself: the name stands where a
    /// value is read, as in <c>{name}</c> or <c>count is 1</c>, one parameter carries it, and the body declares nothing
    /// else of that name. <see langword="null"/> wherever only the compiler can tell.
    /// </summary>
    // A parameter is the nearest thing a name in the body of its method can stand for. So a name that is read as a
    // value, and that nothing nearer declares anew, is the parameter. This is what the generators read of every text
    // that takes values on every edit, and binding the body costs more than everything else read of it.
    internal static int? FindParameterBySyntax(IdentifierNameSyntax identifier, IMethodSymbol method, SyntaxNode body, ref HashSet<string>? redeclared)
    {
        var name = identifier.Identifier.ValueText;
        IParameterSymbol? found = null;

        foreach (var parameter in method.Parameters)
        {
            if (!String.Equals(parameter.Name, name, StringComparison.Ordinal))
                continue;

            // Two parameters of one name, which does not compile.
            if (found is not null)
                return null;

            found = parameter;
        }

        // A parameter named like its type (Color Color) may stand for the type.
        if (found is null || String.Equals(found.Type.Name, name, StringComparison.Ordinal) || !IsReadAsValue(identifier))
            return null;

        // What the body declares itself, as a lambda parameter or a pattern does: there the name stands for something
        // else, or does not compile. Every name of it once, for all the parameters.
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
    /// Whether a value stands where it only chooses what is worded: in the condition of a conditional expression or of
    /// a statement, in what a switch switches on, in a pattern or a <see langword="when"/> clause, or in a comparison.
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
    /// Whether a parameter of a method is of a type that binds to nothing yet, as one another generator writes, itself
    /// or in a part of it, such as <c>OrderId?</c> or <c>IReadOnlyList&lt;OrderId&gt;</c>. Such a type is written by
    /// the words it is written in, which name it only where the method's using directives are
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
    /// The using directives in reach of a method, as the file of a contract repeats them: those of its file, and those
    /// of the namespace declarations around it, the outermost first. A <see langword="global"/> one is left out,
    /// because it reaches every file already.
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
    /// Whether a parameter of the type may be <see langword="null"/>, as <c>GetTextByKey</c> checks a value that was
    /// passed for it. Not for a reference type without an annotation in code with nullable reference types, such as
    /// <c>string</c>. A value type is told at run time, and a type without annotations may be <see langword="null"/>.
    /// </summary>
    private static bool AcceptsNull(ITypeSymbol type)
    {
        var acceptsNull = type is not { IsReferenceType: true, NullableAnnotation: NullableAnnotation.NotAnnotated };
        return acceptsNull;
    }

    /// <summary>
    /// A type as generated code can write it, in parts: each part starts at a type that lies in a namespace, and holds
    /// what follows it up to the next such type.
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

            // int? is Nullable<int>, and is written as the type it wraps.
            case INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] }:
                Write(underlying, parts);
                Append(parts, "?");
                return;

            // A type that binds to nothing, as one another generator writes: by the words it is written in. The file
            // of the contract repeats the using directives of the method, so that they name the type there too
            // (HasUnboundType).
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
    /// Writes a tuple with the names its elements are given, because a set implementing the contract must name them
    /// alike.
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
/// A parameter of a text that takes values: its name, its type as generated code can write it, whether the
/// default-language wording shows the value, which every other language is then held to (<c>LOC402</c>), what
/// writing its type in generated code reports besides <c>CS0612</c> and <c>CS0618</c>, such as the id of a type marked
/// <c>[Experimental]</c> (<see cref="LocalizationDiscovery.GetSuppressedDiagnosticsOfType"/>), and whether
/// <c>GetTextByKey</c> accepts <see langword="null"/> for it.
/// </summary>
internal readonly record struct TextParameter(
    string Name,
    EquatableArray<TypeNamePart> Type,
    bool IsShown,
    EquatableArray<string> SuppressedDiagnostics,
    bool AcceptsNull);

/// <summary>
/// The using directives the file of a contract repeats, each as it is written (<c>using MyApp.Domain;</c>): those of
/// the files of the default-language set, and those of the namespace declarations around it. Empty unless a text takes
/// a value of a type that binds to nothing yet, as one another generator writes: that type is written by the words it
/// is written in, which name it only where these directives are.
/// </summary>
internal readonly record struct RepeatedUsings(
    EquatableArray<string> OfFile,
    EquatableArray<string> OfNamespace);

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
