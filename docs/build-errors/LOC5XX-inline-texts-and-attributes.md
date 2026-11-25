# Build errors about inline texts and attributes

Each code has an example and the fix. The [list of every code](../build-errors.md#all-codes) shows which ones your IDE can fix.

## LOC500

An argument of `LocalizedText.Inline` or of `[Localized]` has no language name. Every parameter is a string, so two languages could swap unnoticed. Name each one: `LocalizedText.Inline(en: "Orders", nl: "Bestellingen")`, or `[Localized(en: "City", nl: "Plaats")]`.

The IDE fix names each argument after the language of its position.

## LOC501

A member of an enum has no `[Localized]`, and another member of that enum has one. This is a warning. Users would see the member's name as written, in every language. It's usually a member added later, so mark it too:

```text
warning LOC501: 'ParcelStatus.Lost' has no text, but other members of 'ParcelStatus' have one, so users would see 'Lost' as written. Mark it [Localized(en: "...", nl: "...")].
```

An enum without a marked member gets no warning, also when the enum itself is marked, because its values may never be shown. Neither does a member with the value of a marked member, such as `Default = Active`, because it shows that member's text.

The IDE fix marks the member with its name as the wording in your default language, as in `[Localized(en: "Lost")]`. That's what users see now. The compiler then names each language the attribute lacks (`CS7036`).

For a member that's never shown, turn the warning off around it: `#pragma warning disable LOC501`.

Where a missing text has to be a build error, write a [set of the enum](../enums.md#a-set-of-the-enum) instead. An enum that has a set gets its `GetText()` from the set, also while its members are still marked.

## LOC502

A member marked `[Localized]` has a text that can't be registered, so `LocalizedText.GetTextByMember` won't find it. This is a warning, with these causes:

- Its type is private or protected inside another type, or file-local. The generated code that holds the text can't name such a type. Make the type internal or public.
- Its type, or a type around it, is marked `[Obsolete]` as an error. Naming such a type is an error that no pragma turns off. Make the `[Obsolete]` a warning, or remove `[Localized]`.
- A wording is `null`. Write a wording for that language.
- It's written in the `@code` of a `.razor` file. The source generator can't read what Razor generates, so write the type in a C# file.
- The attribute is on an indexer, which has no name to find a text by. Remove it.
- The attribute is on the field behind a property, as in `[field: Localized(...)]`. Write it without `field:`.
- The attribute is written through an alias, as after `using Label = LocalizedAttribute;`. The source generator finds the attribute by its name, so write it as `[Localized]`.

```text
warning LOC502: The text of 'Line.Article' can't be registered because generated code can't name 'Line', which is private, so it won't be found. Make 'Line' internal or public.
```

See [attributes](../attributes.md).
