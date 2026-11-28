// ReSharper disable once CheckNamespace
namespace System;

// Lets list patterns compile on netstandard2.0, which lacks this type.
// ReSharper disable UnusedMember.Global
internal readonly struct Index : IEquatable<Index>
{
    public override string ToString() => this.IsFromEnd ? "^" + this.Value : this.Value.ToString(Globalization.CultureInfo.InvariantCulture);

    // A position from the end is stored as its bitwise complement, as in the runtime.
    private readonly int _value;

    public Index(int value, bool fromEnd = false)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "An index can't be negative.");

        this._value = fromEnd ? ~value : value;
    }

    public static Index Start => new(value: 0);

    public static Index End => new(value: 0, fromEnd: true);

    public int Value => this._value < 0 ? ~this._value : this._value;

    public bool IsFromEnd => this._value < 0;

    public static Index FromStart(int value) => new(value);

    public static Index FromEnd(int value) => new(value, fromEnd: true);

    public static implicit operator Index(int value) => FromStart(value);

    public int GetOffset(int length) => this.IsFromEnd ? length - this.Value : this.Value;

    public bool Equals(Index other) => this._value == other._value;

    public override bool Equals(object? obj) => obj is Index other && this.Equals(other);

    public override int GetHashCode() => this._value;
}
