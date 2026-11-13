// ReSharper disable once CheckNamespace
namespace System;

// Lets list patterns compile on netstandard2.0, where the runtime does not declare this type: the compiler asks for it
// for a pattern such as [var only], even though it then reads the list by a plain number.
// ReSharper disable UnusedMember.Global
internal readonly struct Index : IEquatable<Index>
{
    // The position, with a position counted from the end stored as its bitwise complement, as the runtime's own does.
    private readonly int _value;

    public Index(int value, bool fromEnd = false)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "An index cannot be negative.");

        this._value = fromEnd ? ~value : value;
    }

    public static Index Start => new(0);

    public static Index End => new(0, fromEnd: true);

    public int Value => this._value < 0 ? ~this._value : this._value;

    public bool IsFromEnd => this._value < 0;

    public static Index FromStart(int value) => new(value);

    public static Index FromEnd(int value) => new(value, fromEnd: true);

    public static implicit operator Index(int value) => FromStart(value);

    public int GetOffset(int length) => this.IsFromEnd ? length - this.Value : this.Value;

    public bool Equals(Index other) => this._value == other._value;

    public override bool Equals(object? obj) => obj is Index other && this.Equals(other);

    public override int GetHashCode() => this._value;

    public override string ToString() => this.IsFromEnd ? "^" + this.Value : this.Value.ToString(Globalization.CultureInfo.InvariantCulture);
}
