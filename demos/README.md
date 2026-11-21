# Run a demo

This folder has four demos of [Idioma](../README.md). Each is a complete app in one file, which `dotnet run` runs without a project:

- [`SetDemo.cs`](SetDemo.cs) writes its texts in a set per language, including a plural.
- [`EnumSetDemo.cs`](EnumSetDemo.cs) writes the texts of an enum in a set of the enum.
- [`InlineTextDemo.cs`](InlineTextDemo.cs) writes its text inline.
- [`AttributeDemo.cs`](AttributeDemo.cs) writes its texts as attributes on a class, its properties and the members of an enum.

All four support English and Dutch. `SetDemo.cs`, `InlineTextDemo.cs` and `AttributeDemo.cs` also support British English (`en-GB`), a [regional variant](../README.md#regional-variants-such-as-british-english) of English. Each prints its texts in each of its languages, and for a German user. German isn't one of their languages, so that user gets English, the default, as a [fallback](../docs/advanced-topics.md#fallback-order).

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