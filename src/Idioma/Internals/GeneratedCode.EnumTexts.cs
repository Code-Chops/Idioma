using System.Reflection;
using System.Runtime.Loader;

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

        // Chosen again where a registration came or went since, or the app registered its languages, which may change
        // which one wins.
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
    /// <param name="getText">Gives a value of the enum its text, as <c>GetTextByValue</c> of the set's generated class does.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="getText"/> is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterEnumTexts<TEnum, TContract>(Func<TEnum, string> getText)
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

        /// <summary>
        /// 1 once <see cref="Forget"/> is added to <see cref="CollectibleAssemblies"/>, and 0 before. An <see cref="int"/>,
        /// because <see cref="Interlocked.Exchange(ref int, int)"/> sets it, so that it's added once per enum.
        /// </summary>
        private static int IsForgetterAdded;

        public static void Add(Assembly assembly, Func<TEnum, string> getText)
        {
            ImmutableInterlocked.Update(
                location: ref Registered,
                transformer: static (registered, added) => registered.Add(added),
                transformerArgument: new Registration(assembly, getText));

            // Forgotten when a plugin that gave a lasting enum its texts unloads. An enum of the plugin itself goes with
            // it, and a function kept for it would hold the plugin.
            CollectibleAssemblies.Watch(assembly);

            if (assembly.IsCollectible && !typeof(TEnum).IsCollectible && Interlocked.Exchange(ref IsForgetterAdded, value: 1) is 0)
                CollectibleAssemblies.AddForgetter(static context => Forget(context));

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

        private static void Forget(AssemblyLoadContext context)
        {
            ImmutableInterlocked.Update(
                ref Registered,
                static (registered, context) => registered.RemoveAll(registration => CollectibleAssemblies.IsIn(registration.Assembly, context)),
                context);

            Choose();
        }

        /// <summary>
        /// A registration: the assembly of the set, and what gives a value its text.
        /// </summary>
        public sealed class Registration(Assembly assembly, Func<TEnum, string> getText)
        {
            public Assembly Assembly { get; } = assembly;
            public Func<TEnum, string> GetText { get; } = getText;
        }

        /// <summary>
        /// The registration that wins, with the registrations and the app it was chosen from.
        /// </summary>
        public sealed class ChosenTexts(ImmutableArray<Registration> registered, Assembly? application, Func<TEnum, string>? getText)
        {
            public Func<TEnum, string>? GetText { get; } = getText;

            /// <summary>
            /// Whether the choice still holds: no registration came or went since, and the app is the one it was made
            /// for. Compared by reference, because the registrations are replaced as a whole.
            /// </summary>
            public bool IsCurrent => this.From == Registered && ReferenceEquals(this.Application, LocalizationRegistry.ApplicationAssembly);

            private ImmutableArray<Registration> From { get; } = registered;
            private Assembly? Application { get; } = application;
        }
    }
}
