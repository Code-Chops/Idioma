using System.Reflection;
using System.Text.Json;
using CodeChops.Idioma.Internals;
using CodeChops.Idioma.Tests.GeneratedSets;

#region Fixture
// Top-level, because the generated code names these types from the top level of the assembly. The languages are those
// of GeneratedSets.Tests.cs: en and nl.
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

        // Without a text on purpose, which the build would report (LOC503).
#pragma warning disable LOC503
        Lost = 4,
#pragma warning restore LOC503

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
    /// A type no generated code knows, whose text a test records by hand.
    /// </summary>
    public sealed class RecordedLater;
}
#endregion

namespace CodeChops.Idioma.Tests
{
    using CodeChops.Idioma.Tests.Members;

    /// <summary>
    /// Tests for the texts of members marked <c>[Localized]</c>: <c>LocalizableText.GetTextByMember</c> finds the text
    /// the source generator wrote for a member, by its type and name or by the member itself, and <c>GetText()</c>
    /// gives a value of an enum the text of its member. This project's own build generates the texts of the fixture
    /// above.
    /// </summary>
    public sealed class LocalizableTextMembersTests
    {
        #region Fixture
        private sealed class UICultureScope : IDisposable
        {
            private CultureInfo Previous { get; } = CultureInfo.CurrentUICulture;

            public UICultureScope(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

            public void Dispose() => CultureInfo.CurrentUICulture = this.Previous;
        }
        #endregion

        [Test]
        public void GetTextByMember_OfMarkedProperty_ShouldGiveItsTextInTheUsersLanguage()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act
            var text = LocalizableText.GetTextByMember<Address>(nameof(Address.City))!;

            // Assert: the user's language, another on request, and the default language for one the app lacks.
            text.ToString().ShouldBe("Plaats");
            text.ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe("City");
            text.ToString(CultureInfo.GetCultureInfo("de-DE")).ShouldBe("City");
            text.DefaultText.ShouldBe("City");
        }

        [Test]
        public void GetTextByMember_ShouldGiveOneTextThatFollowsALanguageSwitch()
        {
            // Arrange
            var text = LocalizableText.GetTextByMember<Address>(nameof(Address.City))!;

            // Act & Assert: the same object, which shows each user their language.
            using (new UICultureScope("nl-NL"))
                text.ToString().ShouldBe("Plaats");

            using (new UICultureScope("en-US"))
                text.ToString().ShouldBe("City");

            LocalizableText.GetTextByMember<Address>(nameof(Address.City)).ShouldBeSameAs(text);
        }

        [Test]
        public void GetTextByMember_ShouldGiveTheSameTextThroughEveryOverload()
        {
            // Arrange
            var text = LocalizableText.GetTextByMember<Address>(nameof(Address.City));

            // Act & Assert: by a type the code only has at run time, and by the property itself, also as a class that
            // derives from its type gives it.
            text.ShouldNotBeNull();
            LocalizableText.GetTextByMember(typeof(Address), "City").ShouldBeSameAs(text);
            LocalizableText.GetTextByMember(typeof(Address).GetProperty(nameof(Address.City))!).ShouldBeSameAs(text);
            LocalizableText.GetTextByMember(typeof(BusinessAddress).GetProperty(nameof(Address.City))!).ShouldBeSameAs(text);
        }

        [Test]
        public void GetTextByMember_ShouldHaveTheMembersNameAsKeyAndItsFullNameAsFullKey()
        {
            // Arrange & Act
            var property = LocalizableText.GetTextByMember<Address>(nameof(Address.City))!;
            var type = LocalizableText.GetTextOfType<Address>()!;
            var enumMember = LocalizableText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Picked))!;
            var generic = LocalizableText.GetTextByMember<Page<int>>(nameof(Page<>.Items))!;

            // Assert: a type with type parameters keeps its arity, so that Page and Page<T> don't share keys.
            property.Key.ShouldBe("City");
            property.FullKey.ShouldBe("CodeChops.Idioma.Tests.Members.Address.City");
            type.Key.ShouldBe("Address");
            type.FullKey.ShouldBe("CodeChops.Idioma.Tests.Members.Address");
            enumMember.Key.ShouldBe("Picked");
            enumMember.FullKey.ShouldBe("CodeChops.Idioma.Tests.Members.ParcelStatus.Picked");
            generic.Key.ShouldBe("Items");
            generic.FullKey.ShouldBe("CodeChops.Idioma.Tests.Members.Page`1.Items");
        }

        [Test]
        public void GetTextByMember_ByNameThatIsAnotherStringObject_ShouldGiveTheSameText()
        {
            // Arrange: a name built at run time, which is equal to the member's name and not the same object.
            var text = LocalizableText.GetTextByMember<Address>(nameof(Address.City));
            var built = String.Concat("Ci", new string('t', 1), "y");

            // Act & Assert: a name is compared as an object first, and by its characters then.
            ReferenceEquals(built, nameof(Address.City)).ShouldBeFalse();
            text.ShouldNotBeNull();
            LocalizableText.GetTextByMember<Address>(built).ShouldBeSameAs(text);
            LocalizableText.GetTextByMember(typeof(Address), built).ShouldBeSameAs(text);
            LocalizableText.GetTextByMember<Address>("").ShouldBeNull();
        }

        [Test]
        public void GetTextByMember_OfTypeWithManyMembers_ShouldGiveEachItsOwnText()
        {
            // Arrange & Act: more names than the first table holds, some of one length with the same first, middle and
            // last letter, which get the same place. Asked twice, so that each is also found once it's kept.
            for (var round = 0; round < 2; round++)
            {
                foreach (var property in typeof(ManyLabels).GetProperties())
                {
                    var text = LocalizableText.GetTextByMember(property);

                    // Assert
                    text.ShouldNotBeNull(property.Name).ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe($"{property.Name} (nl)");
                    LocalizableText.GetTextByMember<ManyLabels>(property.Name).ShouldBeSameAs(text);
                }
            }

            typeof(ManyLabels).GetProperties().Length.ShouldBe(12);
        }

        [Test]
        public void GetTextByMember_ByMoreNamesThanAreKept_ShouldStillFindEveryText()
        {
            // Arrange: names a caller makes up, more of them than a type keeps.
            for (var i = 0; i < 600; i++)
                LocalizableText.GetTextByMember<Line>($"Made up {i}").ShouldBeNull();

            // Act & Assert: a member's text is found all the same, kept or not.
            LocalizableText.GetTextByMember<Line>(nameof(Line.Quantity)).ShouldNotBeNull();
            LocalizableText.GetTextByMember<Line>(nameof(Line.Quantity)).ShouldBeSameAs(LocalizableText.GetTextByMember<Line>(nameof(Line.Quantity)));
            LocalizableText.GetTextByMember<Line>("Made up 599").ShouldBeNull();
        }

        [Test]
        public void GetTextByMember_OfOneTypeAfterAnother_ShouldGiveEachItsOwnTexts()
        {
            // Arrange & Act & Assert: by types the code holds, asked in turn, as a page with two forms asks.
            for (var round = 0; round < 3; round++)
            {
                LocalizableText.GetTextByMember(typeof(Address), nameof(Address.City))!.DefaultText.ShouldBe("City");
                LocalizableText.GetTextByMember(typeof(Line), nameof(Line.Quantity))!.DefaultText.ShouldBe("Quantity");
                LocalizableText.GetTextByMember(typeof(Line), nameof(Address.City)).ShouldBeNull();
                LocalizableText.GetTextOfType(typeof(Address))!.DefaultText.ShouldBe("Address");
                LocalizableText.GetTextOfType(typeof(Line)).ShouldBeNull();
            }
        }

        [Test]
        public void GetTextByMember_OfMemberWithoutAText_ShouldGiveNull()
        {
            // Arrange & Act & Assert: a property that isn't marked, a name no member has, a name in another letter
            // case, and a type no assembly marks.
            LocalizableText.GetTextByMember<Address>(nameof(Address.Unmarked)).ShouldBeNull();
            LocalizableText.GetTextByMember<Address>("Street").ShouldBeNull();
            LocalizableText.GetTextByMember<Address>("city").ShouldBeNull();
            LocalizableText.GetTextByMember<string>(nameof(String.Length)).ShouldBeNull();
            LocalizableText.GetTextOfType(typeof(string)).ShouldBeNull();
            LocalizableText.GetTextByMember(typeof(Address).GetProperty(nameof(Address.Unmarked))!).ShouldBeNull();
        }

        [Test]
        public void GetTextByMember_OfFieldAndOfPropertyOfARecord_ShouldGiveItsText()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert
            LocalizableText.GetTextByMember<Address>(nameof(Address.Note))!.ToString().ShouldBe("Notitie");
            LocalizableText.GetTextByMember(typeof(Address).GetField(nameof(Address.Note))!)!.ToString().ShouldBe("Notitie");
            LocalizableText.GetTextByMember<Line>(nameof(Line.Quantity))!.ToString().ShouldBe("Aantal");
        }

        [Test]
        public void GetTextByMember_OfInheritedProperty_ShouldGiveTheTextOfTheNearestBaseClass()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");
            var id = LocalizableText.GetTextByMember<Entity>(nameof(Entity.Id))!;

            // Act & Assert: an override without a text keeps that of the property it overrides, with that property's
            // key. An override with a text of its own shows its own. A class two levels down gets the nearest.
            LocalizableText.GetTextByMember<Address>(nameof(Address.Id)).ShouldBeSameAs(id);
            LocalizableText.GetTextByMember(typeof(Address).GetProperty(nameof(Address.Id))!).ShouldBeSameAs(id);
            id.FullKey.ShouldBe("CodeChops.Idioma.Tests.Members.Entity.Id");

            LocalizableText.GetTextByMember<Entity>(nameof(Entity.Created))!.ToString().ShouldBe("Aangemaakt");
            LocalizableText.GetTextByMember<Address>(nameof(Address.Created))!.ToString().ShouldBe("Geregistreerd");
            LocalizableText.GetTextByMember<BusinessAddress>(nameof(Address.Created))!.ToString().ShouldBe("Geregistreerd");
            LocalizableText.GetTextByMember<BusinessAddress>(nameof(Address.Id)).ShouldBeSameAs(id);
        }

        [Test]
        public void GetTextByMember_OfTypeWithTypeArguments_ShouldGiveTheTextOfItsDefinition()
        {
            // Arrange
            var text = LocalizableText.GetTextByMember(typeof(Page<>), "Items");

            // Act & Assert
            text.ShouldNotBeNull();
            LocalizableText.GetTextByMember<Page<int>>("Items").ShouldBeSameAs(text);
            LocalizableText.GetTextByMember<Page<string>>("Items").ShouldBeSameAs(text);
            LocalizableText.GetTextByMember(typeof(Page<Address>).GetProperty("Items")!).ShouldBeSameAs(text);
        }

        [Test]
        public void GetTextOfType_ShouldGiveItsOwnTextWhichIsNotInherited()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: a class and an enum. A class that derives from one with a text has none itself.
            LocalizableText.GetTextOfType<Address>()!.ToString().ShouldBe("Adres");
            LocalizableText.GetTextOfType<ParcelStatus>()!.ToString().ShouldBe("Pakketstatus");
            LocalizableText.GetTextOfType<BusinessAddress>().ShouldBeNull();
            LocalizableText.GetTextOfType<Entity>().ShouldBeNull();
            LocalizableText.GetTextOfType<string>().ShouldBeNull();
        }

        [Test]
        public void GetTextOfType_ShouldGiveTheSameTextThroughEveryOverload()
        {
            // Arrange
            var text = LocalizableText.GetTextOfType<Address>();

            // Act & Assert: by a type the code only has at run time, and by the type as a member, which a Type is.
            text.ShouldNotBeNull();
            LocalizableText.GetTextOfType(typeof(Address)).ShouldBeSameAs(text);
            LocalizableText.GetTextByMember(typeof(Address)).ShouldBeSameAs(text);
            LocalizableText.GetTextOfType<Page<int>>().ShouldBeNull();
        }

        [Test]
        public void GetTextByMember_OfEnumMember_ShouldGiveItsTextByNameAndByField()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act
            var text = LocalizableText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Picked))!;

            // Assert: also for the member whose value another has, which a name tells apart.
            text.ToString().ShouldBe("Gepickt");
            LocalizableText.GetTextByMember(typeof(ParcelStatus).GetField(nameof(ParcelStatus.Picked))!).ShouldBeSameAs(text);
            LocalizableText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Collected))!.ToString().ShouldBe("Opgehaald");
            LocalizableText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Lost)).ShouldBeNull();
        }

        [Test]
        public void GetText_OfEnumValue_ShouldGiveTheTextOfItsMember()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act
            var text = ParcelStatus.Picked.GetText();

            // Assert: the text GetTextByMember gives, in the user's language and in another.
            text.ShouldBeSameAs(LocalizableText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Picked)));
            text.ToString().ShouldBe("Gepickt");
            text.ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe("Picked");
            ParcelStatus.Assigned.GetText().ToString().ShouldBe("Toegewezen");
        }

        [Test]
        public void GetText_OfMembersWithOneValue_ShouldGiveTheTextOfTheFirst()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: a value can't tell the two apart.
            ParcelStatus.Collected.GetText().ShouldBeSameAs(ParcelStatus.Picked.GetText());
        }

        [Test]
        public void GetText_OfValueWithoutAText_ShouldShowAsToStringWritesIt()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: a member that isn't marked, a combination of flags, and a number no member has.
            ParcelStatus.Lost.GetText().ToString().ShouldBe("Lost");
            (ParcelStatus.Assigned | ParcelStatus.Lost).GetText().ToString().ShouldBe("5");
            ((ParcelStatus)64).GetText().ToString().ShouldBe("64");
        }

        [Test]
        public void GetText_AndGetTextByMember_ShouldAllocateNothing()
        {
            // Arrange: asked once, so that the text is found before the calls are counted.
            var property = typeof(Address).GetProperty(nameof(Address.City))!;
            LocalizableText.GetTextByMember(property).ShouldNotBeNull();

            // Act: per 10,000 calls.
            var ofValue = GeneratedSetsTests.Allocated(static () => ParcelStatus.Picked.GetText()) / 10_000;
            var byName = GeneratedSetsTests.Allocated(static () => LocalizableText.GetTextByMember<Address>(nameof(Address.City))) / 10_000;
            var byMember = GeneratedSetsTests.Allocated(() => LocalizableText.GetTextByMember(property)) / 10_000;
            var withoutText = GeneratedSetsTests.Allocated(static () => LocalizableText.GetTextByMember<Address>(nameof(Address.Unmarked))) / 10_000;

            // Assert
            ofValue.ShouldBe(0);
            byName.ShouldBe(0);
            byMember.ShouldBe(0);
            withoutText.ShouldBe(0);
        }

        [Test]
        public void TextOfMember_ShouldEqualAnInlineTextOfTheSameWording()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act
            var text = LocalizableText.GetTextByMember<Address>(nameof(Address.City))!;

            // Assert: it carries its own wording, as an inline text does, and belongs to no set.
            (text == "Plaats").ShouldBeTrue();
            text.ShouldBe(LocalizableText.Inline(en: "City", nl: "Plaats"));
            text.Contract.ShouldBeNull();
        }

        [Test]
        public void TextOfMember_ShouldTravelThroughJsonWithItsKeyAndItsWordingInEveryLanguage()
        {
            // Arrange
            var text = LocalizableText.GetTextByMember<Address>(nameof(Address.City))!;

            // Act
            var json = JsonSerializer.Serialize(text);
            var read = JsonSerializer.Deserialize<LocalizableText>(json)!;

            // Assert: the full key travels, as that of a text of a set does.
            json.ShouldBe("""{"key":"CodeChops.Idioma.Tests.Members.Address.City","defaultText":"City","translations":{"en":"City","nl":"Plaats"}}""");
            read.FullKey.ShouldBe("CodeChops.Idioma.Tests.Members.Address.City");
            read.ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe("Plaats");
            read.ToString(CultureInfo.GetCultureInfo("en-US")).ShouldBe("City");
        }

        [Test]
        public void GetTextByMember_WithNull_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Should.Throw<ArgumentNullException>(() => LocalizableText.GetTextByMember<Address>(null!)).ParamName.ShouldBe("memberName");
            Should.Throw<ArgumentNullException>(() => LocalizableText.GetTextByMember(null!, "City")).ParamName.ShouldBe("type");
            Should.Throw<ArgumentNullException>(() => LocalizableText.GetTextByMember(typeof(Address), null!)).ParamName.ShouldBe("memberName");
            Should.Throw<ArgumentNullException>(() => LocalizableText.GetTextByMember((MemberInfo)null!)).ParamName.ShouldBe("member");
            Should.Throw<ArgumentNullException>(() => LocalizableText.GetTextOfType(null!)).ParamName.ShouldBe("type");
        }

        [Test]
        public void RecordMemberTexts_ShouldAlsoServeAMemberAskedForBefore()
        {
            // Arrange: asked for before its assembly registered, as a type of an assembly that loads later is.
            LocalizableText.GetTextByMember<RecordedLater>("Name").ShouldBeNull();
            LocalizableText.GetTextOfType<RecordedLater>().ShouldBeNull();

            var name = GeneratedCode.CreateMemberText("Tests.RecordedLater.Name", "Name", ("en", "Name"), ("nl", "Naam"));
            var own = GeneratedCode.CreateMemberText("Tests.RecordedLater", "RecordedLater", ("en", "Later"), ("nl", "Later"));

            // Act: as an assembly's generated registrar does.
            GeneratedCode.RecordMemberTexts((type, member) => type != typeof(RecordedLater)
                ? null
                : member switch
                {
                    null => own,
                    "Name" => name,
                    _ => null,
                });

            // Assert: the member isn't kept as one without a text.
            LocalizableText.GetTextByMember<RecordedLater>("Name").ShouldBeSameAs(name);
            LocalizableText.GetTextOfType<RecordedLater>().ShouldBeSameAs(own);
            LocalizableText.GetTextByMember<Address>(nameof(Address.City)).ShouldNotBeNull();
        }

        [Test]
        public void CreateMemberText_ShouldTakeItsFirstWordingAsTheDefault()
        {
            // Arrange & Act
            var text = GeneratedCode.CreateMemberText("Shop.Address.City", "City", ("nl", "Plaats"), ("en", "City"));

            // Assert
            text.DefaultText.ShouldBe("Plaats");
            text.Key.ShouldBe("City");
            text.FullKey.ShouldBe("Shop.Address.City");
            text.ToString(CultureInfo.GetCultureInfo("en-US")).ShouldBe("City");
        }

        [Test]
        public void CreateMemberText_WithoutKeyOrWording_ShouldThrow()
        {
            // Arrange & Act & Assert
            Should.Throw<ArgumentException>(() => GeneratedCode.CreateMemberText(" ", "City", ("en", "City"))).ParamName.ShouldBe("fullKey");
            Should.Throw<ArgumentNullException>(() => GeneratedCode.CreateMemberText("Shop.Address.City", null!, ("en", "City"))).ParamName.ShouldBe("key");
            Should.Throw<ArgumentException>(() => GeneratedCode.CreateMemberText("Shop.Address.City", "City")).ParamName.ShouldBe("wordings");
            Should.Throw<ArgumentNullException>(() => GeneratedCode.RecordMemberTexts(null!)).ParamName.ShouldBe("find");
        }
    }
}
