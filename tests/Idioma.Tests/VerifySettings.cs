using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// The snapshot settings of these tests.
/// </summary>
public static class VerifySettings
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // A snapshot with or without a final newline is the same snapshot: editors and git hooks add or strip one, and
        // that must never fail a test (VerifyTests/Verify#1826). IgnoreTrailingNewline tolerates only a verified file
        // with one newline more than the received text, so the received text loses its own: generated source ends in a
        // line break, and Rider strips the final newline when it saves a snapshot (insert_final_newline = false in the
        // snapshot section of .editorconfig).
        VerifierSettings.IgnoreTrailingNewline();
        VerifierSettings.AddScrubber(static builder =>
        {
            var length = builder.Length;
            while (length > 0 && builder[length - 1] is '\n' or '\r')
                length--;

            builder.Length = length;
        });

        // Snapshots live next to the test file: <TestFileName>.<Method>.verified.<ext>, keyed on the test file's name
        // rather than the class name, so they nest under the test file in the IDE.
        DerivePathInfo(
            (sourceFile, _, _, method) => new(
                directory: Path.GetDirectoryName(sourceFile)!,
                typeName: Path.GetFileNameWithoutExtension(sourceFile),
                methodName: method.Name));
    }
}
