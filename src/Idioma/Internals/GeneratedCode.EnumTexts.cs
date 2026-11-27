using System.Reflection;

namespace CodeChops.Idioma.Internals;

/// <inheritdoc cref="GeneratedCode"/>
public static partial class GeneratedCode
{
    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// The text a set gives a value of an enum, where one registered its texts
    /// (<see cref="RegisterEnumTexts{TEnum, TContract}"/>), or <see langword="null"/>. <c>GetText()</c> of an enum whose
    /// members are marked <c>[Localized]</c> asks this first.
    /// </para>
    /// </summary>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <param name="value">The value to give the text of.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string? GetEnumText<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        // Null for nearly every enum, where no set registered its texts.
        if (EnumTexts<TEnum>.Chosen is not { } chosen)
            return null;

        // Chosen again where a registration came since, or the app registered its languages, which may change which one wins.
        if (!chosen.IsCurrent)
            chosen = EnumTexts<TEnum>.Choose();

        var text = chosen.GetText?.Invoke(value);
        return text;
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// Gives the values of an enum the texts of a set of it, in place of the texts its members are marked
    /// <c>[Localized]</c> with in the enum's own assembly. That assembly's <c>GetText()</c> then gives the set's
    /// wording, in the languages of the set. A set of a library's enum registers this when the assembly that holds
    /// the set starts.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Where the app registers texts of an enum, its texts give them, also when a library registers texts of that enum
    /// later, such as one the app loads lazily. The app is the assembly that registered the languages
    /// (<see cref="RegisterLanguages"/>).
    /// </para>
    /// <para>
    /// Between two libraries, the texts registered last give them.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <typeparam name="TContract">The contract of the set, which tells the assembly that holds it.</typeparam>
    /// <param name="getText">
    /// Gives a value of the enum the set's text, or <see langword="null"/> for a value the set has no text of. Such a
    /// value keeps the text its member is marked <c>[Localized]</c> with, such as a member a newer version of the
    /// enum's assembly adds.
    /// </param>
    /// <exception cref="ArgumentNullException">When <paramref name="getText"/> is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterEnumTexts<TEnum, TContract>(Func<TEnum, string?> getText)
        where TEnum : struct, Enum
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(getText);

        EnumTexts<TEnum>.Add(typeof(TContract).Assembly, getText);
    }

    /// <summary>
    /// Per enum, what gives its values the texts of a set of it (<see cref="RegisterEnumTexts{TEnum, TContract}"/>):
    /// every registration, and the one that wins.
    /// </summary>
    private static class EnumTexts<TEnum>
        where TEnum : struct, Enum
    {
        /// <summary>
        /// The registration that gives the texts, or <see langword="null"/> where none registered. A field per enum,
        /// which a text reads without a lookup: the enum's own <c>GetText()</c> asks on every call.
        /// </summary>
        public static ChosenTexts? Chosen { get; private set; }

        /// <summary>
        /// Every registration, in the order they came.
        /// </summary>
        private static ImmutableArray<Registration> Registered = [];

        public static void Add(Assembly assembly, Func<TEnum, string?> getText)
        {
            ImmutableInterlocked.Update(
                location: ref Registered,
                transformer: static (registered, added) => registered.Add(added),
                transformerArgument: new Registration(assembly, getText));

            Choose();
        }

        /// <summary>
        /// Chooses the registration that gives the texts: the app's last one, or else the last one. The app's wins also
        /// over that of a library that loaded later.
        /// </summary>
        public static ChosenTexts Choose()
        {
            // Read once, so that the choice holds what it was made from. A registration that comes in meanwhile makes the
            // next read choose again (ChosenTexts.IsCurrent).
            var registered = Registered;
            var application = LocalizationRegistry.ApplicationAssembly;

            Registration? last = null;
            Registration? lastOfApplication = null;

            foreach (var registration in registered)
            {
                last = registration;

                if (ReferenceEquals(registration.Assembly, application))
                    lastOfApplication = registration;
            }

            var chosen = new ChosenTexts(registered, application, (lastOfApplication ?? last)?.GetText);
            Chosen = chosen;

            return chosen;
        }

        /// <summary>
        /// A registration: the assembly of the set, and what gives a value its text.
        /// </summary>
        public sealed class Registration(Assembly assembly, Func<TEnum, string?> getText)
        {
            public Assembly Assembly { get; } = assembly;
            public Func<TEnum, string?> GetText { get; } = getText;
        }

        /// <summary>
        /// The registration that wins, with the registrations and the app it was chosen from.
        /// </summary>
        public sealed class ChosenTexts(ImmutableArray<Registration> registered, Assembly? application, Func<TEnum, string?>? getText)
        {
            public Func<TEnum, string?>? GetText { get; } = getText;

            /// <summary>
            /// Whether the choice still holds: no registration came since, and the app is the one it was made for.
            /// Compared by reference, because the registrations are replaced as a whole.
            /// </summary>
            public bool IsCurrent => this.From == Registered && ReferenceEquals(this.Application, LocalizationRegistry.ApplicationAssembly);

            private ImmutableArray<Registration> From { get; } = registered;
            private Assembly? Application { get; } = application;
        }
    }
}
