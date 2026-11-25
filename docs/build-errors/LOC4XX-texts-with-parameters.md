# Build errors about texts with parameters

Each code has an example and the fix. The [list of every code](../build-errors.md#all-codes) shows which ones your IDE can fix.

## LOC400

A public method of the default-language set that returns a `string` is a text with parameters. LOC400 means it can't be one, because the method:

- takes no parameters
- is generic
- shares its name with another method of the set
- takes more than eight parameters
- has a parameter that is passed by reference, is `params`, has a default value, or is `dynamic`
- has a parameter of a type the generated class can't pass on, such as a pointer or a span
- has the name of an enum member, in a [set of an enum](../enums.md#a-set-of-the-enum), where the text of a member is a property

A method that can't become a property isn't reported: an override such as `ToString()`, or a method that another interface of the set asks for.

## LOC401

A set names a parameter differently than the contract does. The compiler checks only the types and order of parameters, so two values of one type could swap unnoticed:

```csharp
// The contract has Invited(string host, string guest)
public string Invited(string guest, string host) => $"{host} has invited {guest}"; // LOC401
```

Use the same parameter names in every language.

The IDE fix gives every parameter the contract's name. When the set has the contract's names in another order, on parameters of one type, the fix moves the names and leaves the body as it is. Each use then reads the parameter of its name, so the example above becomes `Invited(string host, string guest) => $"{host} has invited {guest}"`. A rename doesn't help here, because it renames the uses too and keeps the swap. Swapped names on parameters of different types get no fix, because the body was written for the positions.

When a name is one the contract doesn't have, as `aantal` for `count`, the fix renames it along with its uses and its doc comments. A call in your project that names it, as in `ItemsInCart(aantal: 2)`, names it anew too. There's no fix when:

- a name the contract gives another parameter stands next to one it lacks, because a use could then mean either parameter
- the new name is already used in the method
- the method is partial

## LOC402

A translation never uses a value that the default wording shows:

```csharp
public string Greeting(string name) => $"Hello, {name}!"; // In ShopLocalizationEn
public string Greeting(string name) => "Hallo!";          // In ShopLocalizationNl: LOC402, the name is missing
```

Use the value in every language. Any use counts, in a condition or in the wording, so `count is 1 ? "1 artikel" : "meer artikelen"` uses `count`. A value that the default wording only uses to choose between wordings, such as a form of address, may go unused.

An inline text is held to the same rule. The wording of the default language shows a value where it holds one in an interpolated string:

```csharp
LocalizedText.Inline(en: $"Hello, {name}!", nl: "Hallo!") // LOC402: the 'nl' wording doesn't use 'name'
```

A value is a local variable, a parameter, or a field or property of your own assembly. A constant is wording, so it isn't a value. Neither is a text, such as `ShopLocalization.Checkout`, which another language may word its own way. The build reports only what it can read, so these get no error:

- a wording that isn't written in the call, such as a variable or the result of a method
- a wording that calls a local function or a delegate, which may read the value
- a wording that reads a local variable declared with the value or assigned it, as in `var name = user.Name;`
- a wording that reads a local variable the build can't follow, such as one from a pattern, a deconstruction or `out var`

## LOC403

A wording holds a placeholder of a format string, such as `{0}`. This is a warning. Nothing fills the placeholder in, so users would see `{0}` as written.

- In a set, write the text as a method that takes the value: `public string Greeting(string name) => $"Hello, {name}!";`.
- In an inline text, write the value into every language: `LocalizedText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")`.
- On a member, `[Localized]` takes constants, so its wording can't show a value. Write the text as a method of a set.

The build reads only the strings a text returns:

- a string by itself
- the text of an interpolated string
- a part of a concatenation, a conditional (`?:` or `??`), or a `switch`

It doesn't read a string passed to a method, because the method may fill the placeholder in. So `string.Format("{0} items", count)` gets no warning. A string kept in a local variable isn't read either.

A text that an attribute names as its error message gets no warning either, because the attribute fills the placeholder in. That's a validation attribute with `ErrorMessageResourceName` and `ErrorMessageResourceType`, as in `[Required(ErrorMessageResourceName = nameof(ShopLocalization.NameRequired), ErrorMessageResourceType = typeof(ShopLocalization))]`. Its `{0}` shows the name of the field. A display name, as `[Display]` reads it with `Name` and `ResourceType`, is shown as written, so its placeholder is still reported.

A placeholder is a number in braces, as `string.Format` reads it. An alignment or a format may follow the number:

- `{0}`, `{1,8}`, `{1, -8}` and `{0:N2}` are placeholders.
- `{name}`, `{}` and `{1, 2, 3}` are text, so they get no warning.

A wording that shows such braces on purpose, as `"Type {0} to confirm"` might, needs the warning turned off: `#pragma warning disable LOC403` around the text.

The IDE fix writes a parameter in place of each placeholder of a set's text. A method uses its own parameters, the first for `{0}`. A property becomes a method:

- When the contract's text takes no values yet, as in the default-language set, the method takes an `object?` per placeholder number. So `"Hello, {0}!"` becomes `public string Greeting(object? arg0) => $"Hello, {arg0}!";`. Then give each parameter its name and type.
- When the contract's text takes values, the method takes the contract's parameters. So fix the default-language set first, and name its parameters before you fix the other languages.

The fix changes the text in every language of your project at once, so every set still implements the contract. It counts the placeholders of every language, so each language gets the same parameters: `arg0`, `arg1` and so on. A Dutch wording that holds `{0}` alone still takes `arg1` when the English one holds `{1}`. A Dutch wording without a placeholder becomes a method too. The build then reports the value Dutch leaves out (`LOC402`).

Fixing Dutch changes the default-language set too. A set that implements the text explicitly changes with it, and so does one that overrides it.

Code that shows the text, as in `ShopLocalization.Greeting`, then has to pass the value, and the compiler points at each place that does.

The fix changes every language or none. There's no fix when:

- a language's text can't become a method, such as one with a setter or one the constructor assigns
- code of your project uses the text as a property of a set, such as another text that shows it, because that code would no longer compile. The override of the text in a regional set is the exception: its `base.Greeting` becomes `base.Greeting(arg0)`
- code of your project names the text of the generated class: in `nameof(ShopLocalization.Greeting)`, or as the resource of an attribute such as `[Display(Name = nameof(ShopLocalization.Greeting), ResourceType = typeof(ShopLocalization))]`. That code would go on compiling, and the attribute would throw at run time, because it reads a property by its name
- it's the text of an enum member, in a [set of the enum](../enums.md#a-set-of-the-enum), because the contract takes that text from the enum
- the contract's text takes no values and the contract is another assembly's or one you wrote by hand, because the fix doesn't change a contract
- the text overrides a text of another assembly's set

A wording in a raw string keeps its placeholder and its warning, so write its value by hand.

An inline text gets no fix, because its wording has no parameter to take the value. Neither does a label written with `[Localized]`.
