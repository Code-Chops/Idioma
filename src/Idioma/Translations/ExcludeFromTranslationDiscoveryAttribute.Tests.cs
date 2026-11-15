using System.Reflection;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ExcludeFromTranslationDiscoveryAttribute"/>: its usage and the metadata the generator matches
/// it by. Its effect is tested with the generator.
/// </summary>
public sealed class ExcludeFromTranslationDiscoveryAttributeTests
{
    [Test]
    public void ShouldTargetSetsAndTheirMembersAndNotBeInherited()
    {
        // Arrange & Act
        var usage = typeof(ExcludeFromTranslationDiscoveryAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert: a set, and a property or method of a set that is no text. A set deriving from an excluded set
        // registers itself like any other.
        usage.ValidOn.ShouldBe(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method);
        usage.Inherited.ShouldBeFalse();
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the source generator locates this type by its metadata-name string, so a rename or namespace change
        // must update the generator constant in the same commit.
        typeof(ExcludeFromTranslationDiscoveryAttribute).FullName.ShouldBe("CodeChops.Idioma.ExcludeFromTranslationDiscoveryAttribute");
    }
}
