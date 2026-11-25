# Run a demo

This folder has four demos of [Idioma](../README.md). Each is a complete app in one file, which `dotnet run` runs without a project:

- [`SetDemo.cs`](SetDemo.cs) writes its texts in a set per language, including a plural.
- [`InlineTextDemo.cs`](InlineTextDemo.cs) writes its text inline, and shows it in each language with [`UseCulture`](../docs/advanced-topics.md#another-language).
- [`AttributeDemo.cs`](AttributeDemo.cs) writes its texts as attributes on a class and its properties.
- [`EnumDemo.cs`](EnumDemo.cs) gives two enums their texts, one in each [way](../docs/enums.md): a set of the enum, and an attribute on each member.

All four support English and Dutch. `SetDemo.cs` and `AttributeDemo.cs` also support British English (`en-GB`), a [regional variant](../docs/language-codes.md#regional-variants) of English. Each prints its texts in each of its languages.

`SetDemo.cs` also prints them for two cultures without a set of their own, to show the [fallback](../docs/advanced-topics.md#fallback-order):

- American English (`en-US`) gets English, its parent culture.
- German gets English too, because it's the default language.

## In your browser

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Code-Chops/Idioma?quickstart=1)

The codespace opens the demos and runs them. After an edit, run one again in its terminal:

```shell
cd demos
dotnet run SetDemo.cs
```

A codespace takes a GitHub account, and its use counts towards the free hours of that account.

## On your machine

Run one from this folder in a clone of the repository:

```shell
dotnet run SetDemo.cs
```

A demo runs with .NET 10 or later. Its first line fetches the latest version of the package.

A comment in each file says how to break its build. For example, remove a Dutch wording from any of the files, and it no longer builds.