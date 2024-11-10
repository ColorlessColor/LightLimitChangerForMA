using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace io.github.azukimochi;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct Ranges
{
    public readonly int Length;
    private readonly Vector2 element0;
    private readonly Vector2 element1;
    private readonly Vector2 element2;
    private readonly Vector2 element3;

    public Ranges(Vector2 value)
    {
        Unsafe.SkipInit(out this);
        Length = 1;
        element0 = value;
    }

    public Ranges(ReadOnlySpan<Vector2> value)
    {
        Unsafe.SkipInit(out this);
        var span = MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in element0), Mathf.Min(4, value.Length));
        value[..span.Length].CopyTo(span);
        Length = span.Length;
    }

    public ref readonly Vector2 this[int index]
    {
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in element0), 1)), Mathf.Min(index, Length - 1));
    }

    public ReadOnlySpan<Vector2> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref  Unsafe.AsRef(in element0), Length);

    //public static implicit operator Vector2(in Ranges ranges) => ranges.element0;
    public static implicit operator Ranges(Vector2 vector) => new Ranges(vector);

    public override string ToString()
    {
        return $"({string.Join(", ", AsSpan().ToArray())})";
    }
}
