using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace CodeChops.Idioma;

/// <summary>
/// Lets an assembly that can be unloaded go when its <see cref="AssemblyLoadContext"/> unloads, such as a plugin's.
/// What such an assembly registers is forgotten then: its sets, how the build described them, the texts of its
/// members, and the texts it gave the values of an enum.
/// </summary>
/// <remarks>
/// <para>
/// A context unloads only once nothing holds its types, their objects, or delegates to their code. The registry holds
/// each of those for what the assembly registered, for the whole process. So the context is watched from the first
/// registration of one of its assemblies, and its <see cref="AssemblyLoadContext.Unloading"/> forgets them.
/// </para>
/// <para>
/// A registry that holds such a set isn't kept in what a thread keeps of its last lookup, because no other thread can
/// clear that (<see cref="LocalizationRegistry.HoldsCollectibleSets"/>). Under NativeAOT, no assembly can be unloaded,
/// so nothing here runs.
/// </para>
/// <para>
/// The runtime also raises <see cref="AssemblyLoadContext.Unloading"/> when the process exits, for every context that
/// can unload and is still alive. It does so before the handlers of <see cref="AppDomain.ProcessExit"/> run, so a
/// plugin's registrations are forgotten by then. A text of the plugin that such a handler shows gets the wording of its
/// default language. Examples are a logger that flushes, or a host that stops after <see cref="Environment.Exit"/>.
/// This can't tell an exit from an unload, because .NET has no flag for an exit that started.
/// <see cref="Environment.HasShutdownStarted"/> is always <see langword="false"/>, and the default context's own
/// <see cref="AssemblyLoadContext.Unloading"/> comes after those of the others.
/// </para>
/// </remarks>
internal static class CollectibleAssemblies
{
    /// <summary>
    /// The contexts that are watched. Weak, so that a context that unloaded isn't held here.
    /// </summary>
    private static ConditionalWeakTable<AssemblyLoadContext, object> Watched { get; } = new();

    /// <summary>
    /// The value of each context in <see cref="Watched"/>, which only tells that the context is watched.
    /// </summary>
    private static readonly object Marker = new();

    /// <summary>
    /// What forgets the registrations of a context in a place of its own, such as those of an enum
    /// (<see cref="Internals.GeneratedCode.RegisterEnumTexts{TEnum, TContract}"/>).
    /// </summary>
    private static ImmutableArray<Action<AssemblyLoadContext>> Forgetters = [];

    /// <summary>
    /// Watches the context of an assembly where it can unload.
    /// </summary>
    public static void Watch(Assembly assembly)
    {
        if (!assembly.IsCollectible || AssemblyLoadContext.GetLoadContext(assembly) is not { } context)
            return;

        if (Watched.TryAdd(context, Marker))
            context.Unloading += Forget;
    }

    /// <summary>
    /// Watches each context a type belongs to where it can unload: that of its assembly, and those of the types it's
    /// closed over.
    /// </summary>
    /// <remarks>
    /// A generic type closed over a plugin's type, such as a set <c>SetOf&lt;PluginType&gt;</c>, lies in the assembly
    /// of its definition. That assembly may stay, so the plugin's context is found through the type arguments.
    /// </remarks>
    public static void Watch(Type type)
    {
        if (!type.IsCollectible)
            return;

        Watch(type.Assembly);

        if (type.HasElementType)
            Watch(type.GetElementType()!);
        else if (type.IsConstructedGenericType)
            foreach (var argument in type.GenericTypeArguments)
                Watch(argument);
    }

    /// <summary>
    /// Adds what forgets the registrations of a context in a place of its own, when that context unloads.
    /// </summary>
    /// <remarks>
    /// The function is kept for the process, so it must not hold anything of an assembly that can be unloaded.
    /// </remarks>
    public static void AddForgetter(Action<AssemblyLoadContext> forget)
    {
        ImmutableInterlocked.Update(ref Forgetters, static (forgetters, forget) => forgetters.Add(forget), forget);
    }

    /// <summary>
    /// Whether a type belongs to a context: its assembly lies in it, or a type it's closed over does. Such a type goes
    /// when the context unloads (<see cref="Watch(Type)"/>).
    /// </summary>
    public static bool IsIn(Type type, AssemblyLoadContext context)
    {
        if (!type.IsCollectible)
            return false;

        if (IsIn(type.Assembly, context))
            return true;

        if (type.HasElementType)
            return IsIn(type.GetElementType()!, context);

        if (!type.IsConstructedGenericType)
            return false;

        foreach (var argument in type.GenericTypeArguments)
            if (IsIn(argument, context))
                return true;

        return false;
    }

    /// <summary>
    /// Whether an assembly lies in a context.
    /// </summary>
    public static bool IsIn(Assembly assembly, AssemblyLoadContext context)
    {
        var isIn = assembly.IsCollectible && ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), context);
        return isIn;
    }

    private static void Forget(AssemblyLoadContext context)
    {
        LocalizationRegistry.Forget(context);
        MemberTexts.Forget(context);

        foreach (var forget in Forgetters)
            forget(context);
    }
}
