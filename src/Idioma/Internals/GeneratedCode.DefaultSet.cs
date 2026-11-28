using System.Reflection;

namespace CodeChops.Idioma.Internals;

/// <inheritdoc cref="GeneratedCode"/>
public static partial class GeneratedCode
{
    /// <summary>
    /// The one instance of a contract's set of the default language, created when a text first needs it. So a
    /// constructor that throws fails each text that needs the set, not the generated class for good.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the set's constructor throws, with that exception inside. The message names the set.
    /// </exception>
    internal static TSet GetDefaultSet<TSet>()
        where TSet : class, ILocalizationSet, new()
    {
        var set = DefaultSet<TSet>.Instance ?? DefaultSet<TSet>.Create();
        return set;
    }

    private static class DefaultSet<TSet>
        where TSet : class, ILocalizationSet, new()
    {
        internal static TSet? Instance;

        // Not kept where the constructor throws, so the next text tries again. Where two threads race, the first to
        // finish wins.
        internal static TSet Create()
        {
            var created = (TSet)LocalizationRegistry.CreateSet(typeof(TSet), static () => New());

            var set = Interlocked.CompareExchange(ref Instance, created, comparand: null) ?? created;
            return set;
        }

        // new TSet() wraps what the constructor throws in a TargetInvocationException, which is unwrapped here.
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
