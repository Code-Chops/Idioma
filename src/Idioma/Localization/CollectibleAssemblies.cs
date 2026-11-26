using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace CodeChops.Idioma;

/// <summary>
/// Lets an assembly that can be unloaded go when its <see cref="AssemblyLoadContext"/> unloads, such as a plugin's. What
/// such an assembly registers is forgotten then: its sets, what the build recorded about them, the texts of its
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
/// </remarks>
internal static class CollectibleAssemblies
{
    /// <summary>
    /// The contexts that are watched. Weak, so that a context that unloaded isn't held here.
    /// </summary>
    private static ConditionalWeakTable<AssemblyLoadContext, object> Watched { get; } = new();

    private static readonly object Watching = new();

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

        if (Watched.TryAdd(context, Watching))
            context.Unloading += Forget;
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
    /// Whether a type lies in an assembly of a context that unloads.
    /// </summary>
    public static bool IsIn(Type type, AssemblyLoadContext context)
    {
        return IsIn(type.Assembly, context);
    }

    /// <summary>
    /// Whether an assembly is one of a context that unloads.
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
