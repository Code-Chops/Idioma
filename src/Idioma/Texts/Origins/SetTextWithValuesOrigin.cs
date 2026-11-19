using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CodeChops.Idioma;

/// <summary>
/// A text of a set that takes values, such as <c>ShopLocalization.Greeting(customer.Name)</c>: the values it was
/// created with, and how a set words the text with them.
/// </summary>
// Abstract here, so a text holds its values whatever their types. The generic class below keeps each value as the type
// it was passed as, so a number isn't boxed until something reads LocalizableText.Arguments.
internal abstract class SetTextWithValuesOrigin : TextOrigin
{
    public override ILocalizationSet DefaultSet { get; }

    public override Type Contract { get; }

    // Asked of the set only when it's read: such a text is created on every call, and one that finds its wording in
    // the user's language never reads this.
    public override string? DefaultLanguage => field ??= this.DefaultSet.LocalizationLanguageCode;

    // The name of the text, as a new string on every read: such a text is created on every call, and holds no field
    // for a key that a text shown to a user never reads.
    public override string Key => this.Name.ToString();

    // The arguments are checked for null here, and the key in the base, because this class isn't generic. Under
    // WebAssembly AOT, each check in the generic code below added about 25 ns to every text that takes values (measured
    // 2026-11-18). The two functions are passed in only to be checked.
    // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local - The two functions are passed in only to be checked.
    private protected SetTextWithValuesOrigin(string key, Type contract, ILocalizationSet defaultSet, Delegate selector,
        Delegate callSelector)
        : base(key)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(defaultSet);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(callSelector);

        // A blank key and a set of another contract are checked in a Debug build only, because such a text is created
        // on every call. The generated class passes the key it wrote and the set of the default language it created.
        Debug.Assert(!String.IsNullOrWhiteSpace(key), "A text that takes values has a blank key.");
        Debug.Assert(contract.IsInstanceOfType(defaultSet), $"The set of the default language doesn't implement {contract}.");

        this.Contract = contract;
        this.DefaultSet = defaultSet;
    }
    // ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local

    /// <summary>
    /// The values in the order of the parameters, each as an <see cref="object"/>.
    /// </summary>
    public abstract ImmutableArray<object?> ToArguments();

    /// <summary>
    /// The origin of a text that a set of <paramref name="contract"/> words with <paramref name="values"/>.
    /// <paramref name="callSelector"/> calls <paramref name="selector"/> on the set with the values.
    /// </summary>
    // Both functions are static lambdas of generic code, so the runtime creates each once: a call allocates only the
    // object that holds them and the values. The constructors check the arguments for null, once.
    public static SetTextWithValuesOrigin<TSelector, TValues> Of<TSelector, TValues>(string key, Type contract,
        TSelector selector, Func<TSelector, ILocalizationSet, TValues, string> callSelector, ILocalizationSet defaultSet,
        TValues values)
        where TSelector : Delegate
        where TValues : struct, ITuple
    {
        var origin = new SetTextWithValuesOrigin<TSelector, TValues>(key, contract, selector, callSelector, defaultSet,
            values);
        return origin;
    }
}

/// <summary>
/// The values of a text as a tuple of their own types, with the method of the generated class that picks the text from
/// a set (<typeparamref name="TSelector"/>).
/// </summary>
internal sealed class SetTextWithValuesOrigin<TSelector, TValues> : SetTextWithValuesOrigin
    where TSelector : Delegate
    where TValues : struct, ITuple
{
    private TSelector Selector { get; }
    private Func<TSelector, ILocalizationSet, TValues, string> CallSelector { get; }

    // The constraints let the calls on the tuple go without boxing it.
    private TValues Values { get; }

    public SetTextWithValuesOrigin(string key, Type contract, TSelector selector,
        Func<TSelector, ILocalizationSet, TValues, string> callSelector, ILocalizationSet defaultSet, TValues values)
        : base(key, contract, defaultSet, selector, callSelector)
    {
        // The base constructors check the arguments because they aren't generic (see there).
        this.Selector = selector;
        this.CallSelector = callSelector;
        this.Values = values;
    }

    // Not checked, because only the library calls this, with a set it has. A text that takes values is worded on every
    // call, and a check here costs about 25 ns under WebAssembly AOT.
    public override string Select(ILocalizationSet set)
    {
        var wording = this.CallSelector(this.Selector, set, this.Values);
        return wording;
    }

    public override ImmutableArray<object?> ToArguments()
    {
        // A local, because each call on the readonly field would copy the tuple first.
        var values = this.Values;

        // ITuple flattens a tuple of more than seven values, which holds the rest in a tuple of its own.
        var arguments = ImmutableArray.CreateBuilder<object?>(values.Length);

        for (var i = 0; i < values.Length; i++)
            arguments.Add(values[i]);

        return arguments.MoveToImmutable();
    }
}
