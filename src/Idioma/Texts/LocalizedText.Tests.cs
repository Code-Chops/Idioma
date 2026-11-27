using System.Reflection;
using System.Runtime.Loader;
using CodeChops.Idioma.Internals;
using CodeChops.Idioma.Tests.TestHelpers;

#region Fixture
// Top-level, because the generated code names these types from the top level of the assembly. The languages are those
// of LocalizationGenerator.GeneratedSets.Tests.cs: en and nl.
namespace CodeChops.Idioma.Tests.Members
{
    /// <summary>
    /// An enum with a text of its own, a member without a text, and two members of one value.
    /// </summary>
    [Localized(en: "Parcel status", nl: "Pakketstatus")]
    public enum ParcelStatus
    {
        [Localized(en: "Assigned", nl: "Toegewezen")]
        Assigned = 1,

        [Localized(en: "Picked", nl: "Gepickt")]
        Picked = 2,

        // Without a text on purpose, which the build would report (LOC501).
#pragma warning disable LOC501
        Lost = 4,
#pragma warning restore LOC501

        [Localized(en: "Collected", nl: "Opgehaald")]
        Collected = Picked,
    }

    public class Entity
    {
        [Localized(en: "ID", nl: "Nummer")]
        public virtual int Id => 0;

        [Localized(en: "Created", nl: "Aangemaakt")]
        public virtual DateTime Created => default;
    }

    [Localized(en: "Address", nl: "Adres")]
    public class Address : Entity
    {
        [Localized(en: "City", nl: "Plaats")]
        public string City { get; } = "";

        [Localized(en: "Note", nl: "Notitie")]
        public string Note = "";

        public string Unmarked { get; } = "";

        // Without a text of its own, so it keeps that of the property it overrides.
        public override int Id => 1;

        [Localized(en: "Registered", nl: "Geregistreerd")]
        public override DateTime Created => default;
    }

    public sealed class BusinessAddress : Address;

    [Localized(en: "Page", nl: "Pagina")]
    public sealed class Page<T>
    {
        [Localized(en: "Items", nl: "Regels")]
        public T[] Items { get; } = [];
    }

    public sealed record Line([property: Localized(en: "Quantity", nl: "Aantal")] int Quantity);

    /// <summary>
    /// More members than the first table of a type's names holds. Three of them have one length and the same first,
    /// middle and last letter.
    /// </summary>
    public sealed class ManyLabels
    {
        [Localized(en: "Alpha", nl: "Alpha (nl)")] public string Alpha { get; } = "";
        [Localized(en: "Bravo", nl: "Bravo (nl)")] public string Bravo { get; } = "";
        [Localized(en: "Charlie", nl: "Charlie (nl)")] public string Charlie { get; } = "";
        [Localized(en: "Delta", nl: "Delta (nl)")] public string Delta { get; } = "";
        [Localized(en: "Echo", nl: "Echo (nl)")] public string Echo { get; } = "";
        [Localized(en: "Foxtrot", nl: "Foxtrot (nl)")] public string Foxtrot { get; } = "";
        [Localized(en: "Golf", nl: "Golf (nl)")] public string Golf { get; } = "";
        [Localized(en: "Hotel", nl: "Hotel (nl)")] public string Hotel { get; } = "";
        [Localized(en: "India", nl: "India (nl)")] public string India { get; } = "";
        [Localized(en: "Saxon", nl: "Saxon (nl)")] public string Saxon { get; } = "";
        [Localized(en: "Sexon", nl: "Sexon (nl)")] public string Sexon { get; } = "";
        [Localized(en: "Sixon", nl: "Sixon (nl)")] public string Sixon { get; } = "";
    }

    /// <summary>
    /// Types no generated code knows, whose texts a test registers by hand. One per test, because a registered text stays
    /// for the whole run.
    /// </summary>
    public sealed class RegisteredLater;

    public sealed class RegisteredDutchFirst;

    public sealed class RegisteredDutchGermanEnglish;

    public sealed class RegisteredFrenchGerman;

    public sealed class RegisteredWithRegion;

    public sealed class RegisteredShort;
}
#endregion

namespace CodeChops.Idioma.Tests
{
    using Members;

    /// <summary>
    /// Tests for the texts of members marked <c>[Localized]</c>: <c>LocalizedText.GetTextByMember</c> gives the
    /// wording the source generator wrote for a member, by its type and name or by the member itself, and
    /// <c>GetText()</c> gives a value of an enum the wording of its member. Each is a string: the wording for the
    /// current UI culture at the call. This project's own build generates the texts of the fixture above.
    /// </summary>
    public sealed class LocalizedTextTests
    {
        #region Fixture
        /// <summary>
        /// Registers the text of a type and of its member <c>Name</c>, as an assembly's generated registrar does.
        /// </summary>
        private static void Register(Type registered, string[] languages, string[] own, string[] name)
        {
            GeneratedCode.RegisterMemberTexts(languages, (type, member) => type != registered
                ? null
                : member switch
                {
                    null => own,
                    "Name" => name,
                    _ => null,
                });
        }
        #endregion

        [Test]
        public void GetTextByMember_OfMarkedProperty_ShouldGiveItsWordingForTheCurrentUICulture()
        {
            // Arrange & Act & Assert: the user's language, a region of another, and the app's default language for a
            // culture the app lacks.
            using (new CulturesScope("nl-NL"))
                LocalizedText.GetTextByMember<Address>(nameof(Address.City)).ShouldBe("Plaats");

            using (new CulturesScope("en-GB"))
                LocalizedText.GetTextByMember<Address>(nameof(Address.City)).ShouldBe("City");

            using (new CulturesScope("de-DE"))
                LocalizedText.GetTextByMember<Address>(nameof(Address.City)).ShouldBe("City");
        }

        [Test]
        public void GetTextByMember_ShouldHoldTheWordingOfTheMomentItIsRead()
        {
            // Arrange: read for a Dutch user.
            string? text;

            using (new CulturesScope("nl-NL"))
                text = LocalizedText.GetTextByMember<Address>(nameof(Address.City));

            // Act & Assert: the string stays Dutch, and reading the text again gives the English user English.
            using (new CulturesScope("en-US"))
            {
                text.ShouldBe("Plaats");
                LocalizedText.GetTextByMember<Address>(nameof(Address.City)).ShouldBe("City");
            }
        }

        [Test]
        public void GetTextByMember_ShouldGiveTheSameTextThroughEveryOverload()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert: by a type the code only has at run time, and by the property itself, also as a class that
            // derives from its type gives it.
            LocalizedText.GetTextByMember<Address>(nameof(Address.City)).ShouldBe("Plaats");
            LocalizedText.GetTextByMember(typeof(Address), "City").ShouldBe("Plaats");
            LocalizedText.GetTextByMember(typeof(Address).GetProperty(nameof(Address.City))!).ShouldBe("Plaats");
            LocalizedText.GetTextByMember(typeof(BusinessAddress).GetProperty(nameof(Address.City))!).ShouldBe("Plaats");
        }

        [Test]
        [Arguments("nl-NL", "Plaats", "Adres", "Gepickt", "Regels")]
        [Arguments("nl-BE", "Plaats", "Adres", "Gepickt", "Regels")]
        [Arguments("en-US", "City", "Address", "Picked", "Items")]
        [Arguments("fr-FR", "City", "Address", "Picked", "Items")]
        public void GetTextByMember_ShouldGiveEveryKindOfMemberItsWordingForTheCulture(string culture, string property, string type, string enumMember, string generic)
        {
            // Arrange
            using var _ = new CulturesScope(culture);

            // Act & Assert: a property, a type, a member of an enum, and a property of a type with type parameters.
            LocalizedText.GetTextByMember<Address>(nameof(Address.City)).ShouldBe(property);
            LocalizedText.GetTextOfType<Address>().ShouldBe(type);
            LocalizedText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Picked)).ShouldBe(enumMember);
            LocalizedText.GetTextByMember<Page<int>>(nameof(Page<>.Items)).ShouldBe(generic);
        }

        [Test]
        public void GetTextByMember_ByNameThatIsAnotherStringObject_ShouldGiveTheSameText()
        {
            // Arrange: a name built at run time, which is equal to the member's name and not the same object.
            using var _ = new CulturesScope("nl-NL");
            var built = String.Concat("Ci", new string('t', 1), "y");

            // Act & Assert: a name is compared as an object first, and by its characters then.
            ReferenceEquals(built, nameof(Address.City)).ShouldBeFalse();
            LocalizedText.GetTextByMember<Address>(built).ShouldBe("Plaats");
            LocalizedText.GetTextByMember(typeof(Address), built).ShouldBe("Plaats");
            LocalizedText.GetTextByMember<Address>("").ShouldBeNull();
        }

        [Test]
        public void GetTextByMember_OfTypeWithManyMembers_ShouldGiveEachItsOwnText()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act: more names than the first table holds, some of one length with the same first, middle and last
            // letter, which get the same place. Asked twice, so that each is also found once it's kept.
            for (var round = 0; round < 2; round++)
            {
                foreach (var property in typeof(ManyLabels).GetProperties())
                {
                    // Assert
                    LocalizedText.GetTextByMember(property).ShouldBe($"{property.Name} (nl)");
                    LocalizedText.GetTextByMember<ManyLabels>(property.Name).ShouldBe($"{property.Name} (nl)");
                }
            }

            typeof(ManyLabels).GetProperties().Length.ShouldBe(12);
        }

        [Test]
        public void GetTextByMember_ByMoreNamesThanAreKept_ShouldStillFindEveryText()
        {
            // Arrange: names a caller makes up, more of them than a type keeps.
            using var _ = new CulturesScope("nl-NL");

            for (var i = 0; i < 600; i++)
                LocalizedText.GetTextByMember<Line>($"Made up {i}").ShouldBeNull();

            // Act & Assert: a member's text is found all the same, kept or not.
            LocalizedText.GetTextByMember<Line>(nameof(Line.Quantity)).ShouldBe("Aantal");
            LocalizedText.GetTextByMember<Line>(nameof(Line.Quantity)).ShouldBe("Aantal");
            LocalizedText.GetTextByMember<Line>("Made up 599").ShouldBeNull();
        }

        [Test]
        public void GetTextByMember_OfOneTypeAfterAnother_ShouldGiveEachItsOwnTexts()
        {
            // Arrange
            using var _ = new CulturesScope("en-US");

            // Act & Assert: by types the code holds, asked in turn, as a page with two forms asks.
            for (var round = 0; round < 3; round++)
            {
                LocalizedText.GetTextByMember(typeof(Address), nameof(Address.City)).ShouldBe("City");
                LocalizedText.GetTextByMember(typeof(Line), nameof(Line.Quantity)).ShouldBe("Quantity");
                LocalizedText.GetTextByMember(typeof(Line), nameof(Address.City)).ShouldBeNull();
                LocalizedText.GetTextOfType(typeof(Address)).ShouldBe("Address");
                LocalizedText.GetTextOfType(typeof(Line)).ShouldBeNull();
            }
        }

        [Test]
        public void GetTextByMember_OfMemberWithoutAText_ShouldGiveNull()
        {
            // Arrange & Act & Assert: a property that isn't marked, a name no member has, a name in another letter
            // case, and a type no assembly marks.
            LocalizedText.GetTextByMember<Address>(nameof(Address.Unmarked)).ShouldBeNull();
            LocalizedText.GetTextByMember<Address>("Street").ShouldBeNull();
            LocalizedText.GetTextByMember<Address>("city").ShouldBeNull();
            LocalizedText.GetTextByMember<string>(nameof(String.Length)).ShouldBeNull();
            LocalizedText.GetTextOfType(typeof(string)).ShouldBeNull();
            LocalizedText.GetTextByMember(typeof(Address).GetProperty(nameof(Address.Unmarked))!).ShouldBeNull();
        }

        [Test]
        public void GetTextByMember_OfFieldAndOfPropertyOfARecord_ShouldGiveItsText()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert
            LocalizedText.GetTextByMember<Address>(nameof(Address.Note)).ShouldBe("Notitie");
            LocalizedText.GetTextByMember(typeof(Address).GetField(nameof(Address.Note))!).ShouldBe("Notitie");
            LocalizedText.GetTextByMember<Line>(nameof(Line.Quantity)).ShouldBe("Aantal");
        }

        [Test]
        public void GetTextByMember_OfInheritedProperty_ShouldGiveTheTextOfTheNearestBaseClass()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert: an override without a text keeps that of the property it overrides. An override with a
            // text of its own shows its own. A class two levels down gets the nearest.
            LocalizedText.GetTextByMember<Entity>(nameof(Entity.Id)).ShouldBe("Nummer");
            LocalizedText.GetTextByMember<Address>(nameof(Address.Id)).ShouldBe("Nummer");
            LocalizedText.GetTextByMember(typeof(Address).GetProperty(nameof(Address.Id))!).ShouldBe("Nummer");

            LocalizedText.GetTextByMember<Entity>(nameof(Entity.Created)).ShouldBe("Aangemaakt");
            LocalizedText.GetTextByMember<Address>(nameof(Address.Created)).ShouldBe("Geregistreerd");
            LocalizedText.GetTextByMember<BusinessAddress>(nameof(Address.Created)).ShouldBe("Geregistreerd");
            LocalizedText.GetTextByMember<BusinessAddress>(nameof(Address.Id)).ShouldBe("Nummer");
        }

        [Test]
        public void GetTextByMember_OfTypeWithTypeArguments_ShouldGiveTheTextOfItsDefinition()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert
            LocalizedText.GetTextByMember(typeof(Page<>), "Items").ShouldBe("Regels");
            LocalizedText.GetTextByMember<Page<int>>("Items").ShouldBe("Regels");
            LocalizedText.GetTextByMember<Page<string>>("Items").ShouldBe("Regels");
            LocalizedText.GetTextByMember(typeof(Page<Address>).GetProperty("Items")!).ShouldBe("Regels");
        }

        [Test]
        public void GetTextOfType_ShouldGiveItsOwnTextWhichIsNotInherited()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert: a class and an enum. A class that derives from one with a text has none itself.
            LocalizedText.GetTextOfType<Address>().ShouldBe("Adres");
            LocalizedText.GetTextOfType<ParcelStatus>().ShouldBe("Pakketstatus");
            LocalizedText.GetTextOfType<BusinessAddress>().ShouldBeNull();
            LocalizedText.GetTextOfType<Entity>().ShouldBeNull();
            LocalizedText.GetTextOfType<string>().ShouldBeNull();
        }

        [Test]
        public void GetTextOfType_ShouldGiveTheSameTextThroughEveryOverload()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert: by a type the code only has at run time, and by the type as a member, which a Type is.
            LocalizedText.GetTextOfType<Address>().ShouldBe("Adres");
            LocalizedText.GetTextOfType(typeof(Address)).ShouldBe("Adres");
            LocalizedText.GetTextByMember(typeof(Address)).ShouldBe("Adres");
        }

        [Test]
        public void GetTextOfType_OfTypeWithTypeArguments_ShouldGiveTheTextOfItsDefinition()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert: the definition itself, and two types with type arguments through every overload.
            LocalizedText.GetTextOfType(typeof(Page<>)).ShouldBe("Pagina");
            LocalizedText.GetTextOfType<Page<int>>().ShouldBe("Pagina");
            LocalizedText.GetTextOfType(typeof(Page<Address>)).ShouldBe("Pagina");
            LocalizedText.GetTextByMember(typeof(Page<Address>)).ShouldBe("Pagina");
        }

        [Test]
        public void GetTextByMember_OfEnumMember_ShouldGiveItsTextByNameAndByField()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert: also for the member whose value another has, which a name tells apart.
            LocalizedText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Picked)).ShouldBe("Gepickt");
            LocalizedText.GetTextByMember(typeof(ParcelStatus).GetField(nameof(ParcelStatus.Picked))!).ShouldBe("Gepickt");
            LocalizedText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Collected)).ShouldBe("Opgehaald");
            LocalizedText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Lost)).ShouldBeNull();
        }

        [Test]
        public void GetText_OfEnumValue_ShouldGiveTheTextOfItsMember()
        {
            // Arrange & Act & Assert: the text GetTextByMember gives, in the user's language and in another.
            using (new CulturesScope("nl-NL"))
            {
                ParcelStatus.Picked.GetText().ShouldBe("Gepickt");
                ParcelStatus.Picked.GetText().ShouldBe(LocalizedText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Picked)));
                ParcelStatus.Assigned.GetText().ShouldBe("Toegewezen");
            }

            using (new CulturesScope("en-GB"))
                ParcelStatus.Picked.GetText().ShouldBe("Picked");
        }

        [Test]
        public void GetText_OfMembersWithOneValue_ShouldGiveTheTextOfTheFirst()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert: a value can't tell the two apart. The build warns about naming the second (LOC600).
#pragma warning disable LOC600
            ParcelStatus.Collected.GetText().ShouldBe("Gepickt");
#pragma warning restore LOC600
        }

        [Test]
        public void GetText_OfValueWithoutAText_ShouldShowAsToStringWritesIt()
        {
            // Arrange
            using var _ = new CulturesScope("nl-NL");

            // Act & Assert: a member that isn't marked, a combination of flags, and a number no member has.
            ParcelStatus.Lost.GetText().ShouldBe("Lost");
            (ParcelStatus.Assigned | ParcelStatus.Lost).GetText().ShouldBe("5");
            ((ParcelStatus)64).GetText().ShouldBe("64");
        }

        [Test]
        [NotInParallel(nameof(LocalizationRegistry))]
        public void GetText_AndGetTextByMember_ShouldAllocateNothing()
        {
            // Arrange: asked once, so that the text is found before the calls are counted. Apart from the tests that
            // register sets, which make the next read build the registry anew: that read is this thread's, and would
            // count.
            using var _ = new CulturesScope("nl-NL");
            var property = typeof(Address).GetProperty(nameof(Address.City))!;
            LocalizedText.GetTextByMember(property).ShouldBe("Plaats");

            // Act: per 10,000 calls.
            var ofValue = GeneratedSetsTests.Allocated(static () => ParcelStatus.Picked.GetText()) / 10_000;
            var byName = GeneratedSetsTests.Allocated(static () => LocalizedText.GetTextByMember<Address>(nameof(Address.City))) / 10_000;
            var byMember = GeneratedSetsTests.Allocated(() => LocalizedText.GetTextByMember(property)) / 10_000;
            var withoutText = GeneratedSetsTests.Allocated(static () => LocalizedText.GetTextByMember<Address>(nameof(Address.Unmarked))) / 10_000;

            // Assert
            ofValue.ShouldBe(0);
            byName.ShouldBe(0);
            byMember.ShouldBe(0);
            withoutText.ShouldBe(0);
        }

        [Test]
        public void GetTextByMember_WithNull_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Should.Throw<ArgumentNullException>(() => LocalizedText.GetTextByMember<Address>(null!)).ParamName.ShouldBe("memberName");
            Should.Throw<ArgumentNullException>(() => LocalizedText.GetTextByMember(null!, "City")).ParamName.ShouldBe("type");
            Should.Throw<ArgumentNullException>(() => LocalizedText.GetTextByMember(typeof(Address), null!)).ParamName.ShouldBe("memberName");
            Should.Throw<ArgumentNullException>(() => LocalizedText.GetTextByMember((MemberInfo)null!)).ParamName.ShouldBe("member");
            Should.Throw<ArgumentNullException>(() => LocalizedText.GetTextOfType(null!)).ParamName.ShouldBe("type");
        }

        [Test]
        public void RegisterMemberTexts_ShouldAlsoServeAMemberAskedForBefore()
        {
            // Arrange: asked for before its assembly registered, as a type of an assembly that loads later is.
            using var _ = new CulturesScope("nl-NL");
            LocalizedText.GetTextByMember<RegisteredLater>("Name").ShouldBeNull();
            LocalizedText.GetTextOfType<RegisteredLater>().ShouldBeNull();

            // Act: as an assembly's generated registrar does.
            Register(typeof(RegisteredLater), languages: ["en", "nl"], own: ["Later", "Later (nl)"], name: ["Name", "Naam"]);

            // Assert: the member isn't kept as one without a text.
            LocalizedText.GetTextByMember<RegisteredLater>("Name").ShouldBe("Naam");
            LocalizedText.GetTextOfType<RegisteredLater>().ShouldBe("Later (nl)");
            LocalizedText.GetTextByMember<RegisteredLater>("Other").ShouldBeNull();
            LocalizedText.GetTextByMember<Address>(nameof(Address.City)).ShouldBe("Plaats");
        }

        [Test]
        public void GetTextByMember_OfAssemblyLoadedLater_BeforeAnyOfItsCodeRuns_ShouldGiveItsText()
        {
            // Arrange: a plugin's type, read through reflection alone, as a table that shows the labels of a plugin's
            // model reads it. Its texts register as its code first runs, and none of it ran yet.
            var plugin = new AssemblyLoadContext("PluginWithLabels").LoadFromStream(new MemoryStream(GeneratorTestHelper.EmitLibraryWithGenerators("PluginWithLabels", """
                using CodeChops.Idioma;

                [assembly: SupportedLanguages("en", "nl")]

                namespace PluginWithLabels;

                public class Address
                {
                    [Localized(en: "City", nl: "Plaats")]
                    public string City { get; set; } = "";
                }
                """)));

            var address = plugin.GetType("PluginWithLabels.Address")!;

            // Act
            using var _ = new CulturesScope("nl-NL");
            var byName = LocalizedText.GetTextByMember(address, "City");
            var byProperty = LocalizedText.GetTextByMember(address.GetProperty("City")!);

            // Assert: the text, rather than none.
            byName.ShouldBe("Plaats");
            byProperty.ShouldBe("Plaats");
        }

        [Test]
        public void GetTextByMember_OfAssemblyWhoseModuleInitializerThrows_ShouldGiveNoTextRatherThanThrow()
        {
            // Arrange: a plugin whose own code throws as it first runs, read through reflection alone.
            var plugin = new AssemblyLoadContext("PluginThatFailsToStart").LoadFromStream(new MemoryStream(GeneratorTestHelper.EmitLibrary("PluginThatFailsToStart", """
                using System;
                using System.Runtime.CompilerServices;

                namespace PluginThatFailsToStart;

                public class Address
                {
                    public string City { get; set; } = "";
                }

                internal static class Startup
                {
                    [ModuleInitializer]
                    internal static void Run() => throw new InvalidOperationException("No settings.");
                }
                """)));

            var address = plugin.GetType("PluginThatFailsToStart.Address")!;

            // Act
            var byName = LocalizedText.GetTextByMember(address, "City");
            var ofType = LocalizedText.GetTextOfType(address);

            // Assert: the label has no text, and what the plugin throws is left for the plugin's own code to meet.
            byName.ShouldBeNull();
            ofType.ShouldBeNull();
        }

        [Test]
        public void GetTextByMember_OfAssemblyWithLanguagesInAnotherOrder_ShouldGiveTheWordingOfTheCulturesLanguage()
        {
            // Arrange: an assembly whose default language is Dutch, in this app, whose default language is English.
            Register(typeof(RegisteredDutchFirst), languages: ["nl", "en"], own: ["Eerst", "First"], name: ["Naam", "Name"]);

            // Act & Assert: each wording by the place of its language, not by the place of the app's.
            using (new CulturesScope("nl-NL"))
                LocalizedText.GetTextByMember<RegisteredDutchFirst>("Name").ShouldBe("Naam");

            using (new CulturesScope("en-US"))
            {
                LocalizedText.GetTextByMember<RegisteredDutchFirst>("Name").ShouldBe("Name");
                LocalizedText.GetTextOfType<RegisteredDutchFirst>().ShouldBe("First");
            }
        }

        [Test]
        public void GetTextByMember_ForCultureTheAppDoesNotShow_ShouldGiveTheAppDefaultLanguageElseTheFirstWording()
        {
            // Arrange: this app shows English and Dutch, and its default language is English. One assembly has Dutch,
            // German and English, and another has French and German alone.
            Register(typeof(RegisteredDutchGermanEnglish), languages: ["nl", "de", "en"], own: ["Eerst", "Zuerst", "First"], name: ["Naam", "Namen", "Name"]);
            Register(typeof(RegisteredFrenchGerman), languages: ["fr", "de"], own: ["Premier", "Zuerst"], name: ["Nom", "Namen"]);

            // Act & Assert: a German user gets the app's default language, though the first assembly has German,
            // because the app's own texts are English for them too. The second assembly has no English, so it gives
            // its first wording.
            using (new CulturesScope("de-DE"))
            {
                LocalizedText.GetTextByMember<RegisteredDutchGermanEnglish>("Name").ShouldBe("Name");
                LocalizedText.GetTextOfType<RegisteredDutchGermanEnglish>().ShouldBe("First");
                LocalizedText.GetTextByMember<RegisteredFrenchGerman>("Name").ShouldBe("Nom");
            }

            // A Dutch user, whose culture the app shows, falls back the same way where the assembly lacks Dutch.
            using (new CulturesScope("nl-NL"))
            {
                LocalizedText.GetTextByMember<RegisteredDutchGermanEnglish>("Name").ShouldBe("Naam");
                LocalizedText.GetTextByMember<RegisteredFrenchGerman>("Name").ShouldBe("Nom");
            }
        }

        [Test]
        public void GetTextByMember_OfAssemblyWithARegion_ShouldGiveTheRegionItsWordingAndOtherRegionsThatOfTheLanguage()
        {
            // Arrange: an assembly with British English next to English, whose codes are in another letter case.
            Register(typeof(RegisteredWithRegion), languages: ["en", "nl", "EN-gb"], own: ["Color", "Kleur", "Colour"], name: ["Name", "Naam", "Name (GB)"]);

            // Act & Assert
            using (new CulturesScope("en-GB"))
                LocalizedText.GetTextOfType<RegisteredWithRegion>().ShouldBe("Colour");

            using (new CulturesScope("en-US"))
                LocalizedText.GetTextOfType<RegisteredWithRegion>().ShouldBe("Color");

            using (new CulturesScope("nl-BE"))
                LocalizedText.GetTextOfType<RegisteredWithRegion>().ShouldBe("Kleur");
        }

        [Test]
        public void GetTextByMember_OfTextWithFewerWordingsThanLanguages_ShouldGiveTheFirstWording()
        {
            // Arrange: code written by hand, because the generator writes a wording per language.
            Register(typeof(RegisteredShort), languages: ["en", "nl"], own: ["Short"], name: ["Name"]);

            // Act & Assert: no wording at the place of Dutch, so the first one.
            using var _ = new CulturesScope("nl-NL");
            LocalizedText.GetTextByMember<RegisteredShort>("Name").ShouldBe("Name");
            LocalizedText.GetTextOfType<RegisteredShort>().ShouldBe("Short");
        }

        [Test]
        public void RegisterMemberTexts_WithNull_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Should.Throw<ArgumentNullException>(() => GeneratedCode.RegisterMemberTexts(null!, static (_, _) => null)).ParamName.ShouldBe("languages");
            Should.Throw<ArgumentNullException>(() => GeneratedCode.RegisterMemberTexts(["en"], null!)).ParamName.ShouldBe("find");
        }

        [Test]
        public void LocalizedText_ShouldBeStatic()
        {
            // Arrange & Act
            var type = typeof(LocalizedText);

            // Assert: it only hosts the texts that carry their own wording. A text itself is a string.
            (type.IsAbstract && type.IsSealed).ShouldBeTrue();
        }

        [Test]
        public void MetadataName_ShouldMatchSourceGeneratorReferences()
        {
            // Assert: the source generator writes LocalizedText.Inline as an extension on this type by name, so a
            // rename or namespace change must update the generator's emission in the same commit.
            typeof(LocalizedText).FullName.ShouldBe("CodeChops.Idioma.LocalizedText");
        }
    }
}
