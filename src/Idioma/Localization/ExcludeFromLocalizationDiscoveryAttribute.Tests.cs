using System.Reflection;
using CodeChops.Idioma.Generators;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ExcludeFromLocalizationDiscoveryAttribute"/>: its usage and the metadata the generator matches
/// it by. Its effect is tested with the generator.
/// </summary>
public sealed class ExcludeFromLocalizationDiscoveryAttributeTests
{
    [Test]
    public void ShouldTargetSetsAndTheirMembersAndNotBeInherited()
    {
        // Arrange & Act
        var usage = typeof(ExcludeFromLocalizationDiscoveryAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert: a set, and a property or method of a set that isn't a text. A set deriving from an excluded set
        // is registered automatically, like any other.
        usage.ValidOn.ShouldBe(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method);
        usage.Inherited.ShouldBeFalse();
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the source generator locates this type by the name in its constant, so a rename or namespace change
        // that misses the constant fails here.
        typeof(ExcludeFromLocalizationDiscoveryAttribute).FullName.ShouldBe(PackageNames.ExcludeFromLocalizationDiscoveryAttribute);
    }
}
