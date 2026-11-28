namespace CodeChops.Idioma.Generators;

/// <summary>
/// An immutable array that compares element by element. Use it for any sequence in a generator pipeline, because a step
/// is reused from cache only when its input compares equal.
/// </summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> array) : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    public int Count => this.AsImmutableArray().Length;
    public int Length => this.AsImmutableArray().Length;
    public T this[int index] => this.AsImmutableArray()[index];

    /// <summary>
    /// The wrapped array, with a <see langword="default"/> value normalized to empty.
    /// </summary>
    public ImmutableArray<T> AsImmutableArray() => array.IsDefault ? ImmutableArray<T>.Empty : array;

    public bool Equals(EquatableArray<T> other) => this.AsImmutableArray().SequenceEqual(other.AsImmutableArray());
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && this.Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in this.AsImmutableArray())
            hash = (hash * 31) + (item?.GetHashCode() ?? 0);

        return hash;
    }

    /// <summary>
    /// Whether an item matches, without the boxing of LINQ's <c>Any</c>.
    /// </summary>
    public bool Any(Func<T, bool> predicate)
    {
        foreach (var item in this.AsImmutableArray())
            if (predicate(item))
                return true;

        return false;
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => this.AsImmutableArray().GetEnumerator();
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)this.AsImmutableArray()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.AsImmutableArray()).GetEnumerator();
}
