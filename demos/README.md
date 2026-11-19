# Run a demo

This folder has two demos of [Idioma](../README.md). Each is a complete app in one file, which `dotnet run` runs without a project:

- [`SetDemo.cs`](SetDemo.cs) writes its texts in a set per language.
- [`InlineTextDemo.cs`](InlineTextDemo.cs) writes its text inline.

Both support English, Dutch, and British English (`en-GB`), a [regional variant](../README.md#regional-variants-such-as-british-english) of English, and print their texts in each.

## In your browser

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Code-Chops/Idioma?quickstart=1)

The codespace opens both demos and runs them. After an edit, run one again in its terminal:

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

Remove a Dutch wording from either file, or the parameter of the Dutch `Greeting` in `SetDemo.cs`, and it no longer builds.