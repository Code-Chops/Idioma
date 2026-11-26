using System.Reflection;

namespace CodeChops.Idioma.Internals;

/// <inheritdoc cref="GeneratedCode"/>
public static partial class GeneratedCode
{
    /// <summary>
    /// The set of a contract's default language, which gives the texts of the generated class their default wording.
    /// It's one instance per set, created when a text first needs it.
    /// </summary>
    /// <remarks>
    /// The set isn't created as the generated class is first used. So a set whose constructor throws doesn't fail that
    /// class for good, with an exception that names only the class. The text that needs the set throws, and so does
    /// every text after it.
    /// </remarks>
    /// <typeparam name="TSet">The set of the default language, such as <c>ShopLocalizationEn</c>.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// When the set's constructor throws, with that exception inside. The message names the set.
    /// </exception>
    internal static TSet GetDefaultSet<TSet>()
        where TSet : class, ILocalizationSet, new()
    {
        var set = DefaultSet<TSet>.Instance ?? DefaultSet<TSet>.Create();
        return set;
    }

    /// <summary>
    /// The one instance of a set of a default language (<see cref="GetDefaultSet{TSet}"/>).
    /// </summary>
    private static class DefaultSet<TSet>
        where TSet : class, ILocalizationSet, new()
    {
        internal static TSet? Instance;

        // Not kept where the constructor throws, so the next text tries again and says the same. Two threads may each
        // create the set, and the first one to finish gives every text its instance.
        internal static TSet Create()
        {
            var created = (TSet)LocalizationRegistry.CreateSet(typeof(TSet), static () => New());

            var set = Interlocked.CompareExchange(ref Instance, created, comparand: null) ?? created;
            return set;
        }

        // new TSet() of a type parameter creates the set through the runtime, which puts what the constructor throws
        // inside an exception of its own. Taken out again, so that the exception that names the set holds the set's
        // own, as for a set the registry creates.
        private static TSet New()
        {
            try
            {
                return new TSet();
            }
            catch (TargetInvocationException exception) when (exception.InnerException is { } thrown)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(thrown);
                throw;
            }
        }
    }
}
