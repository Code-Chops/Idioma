using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="IInlineText{TSelf}"/>: which text type implements it for itself, and the metadata the generator
/// writes it by.
/// </summary>
public sealed class IInlineTextTests
{
    [Test]
    public void LocalizableText_ShouldImplementItForItself()
    {
        // Arrange & Act & Assert
        typeof(LocalizableText).GetInterfaces().ShouldContain(typeof(IInlineText<LocalizableText>));
    }

    [Test]
    public void ResourceText_ShouldNotImplementItForItself()
    {
        // Arrange & Act
        var interfaces = typeof(ResourceText).GetInterfaces();

        // Assert: it inherits the interface as LocalizableText implements it, which is what keeps Translated off it.
        interfaces.ShouldContain(typeof(IInlineText<LocalizableText>));
        interfaces.ShouldNotContain(typeof(IInlineText<ResourceText>));
    }

    [Test]
    public void Metadata_ShouldMatchWhatGeneratorWrites()
    {
        // Arrange & Act & Assert: the generated factory names the interface, with one type argument, in its constraint.
        typeof(IInlineText<>).FullName.ShouldBe("CodeChops.Idioma.Internals.IInlineText`1");
        typeof(IInlineText<>).GetGenericArguments().ShouldHaveSingleItem();
    }
}
