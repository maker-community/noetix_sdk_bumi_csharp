using System.Runtime.CompilerServices;

namespace Noetix.Sdk.Bumi;

// Fixed-size arrays embedded in the blittable structs below. Indexable and
// convertible to Span<T> / ReadOnlySpan<T>.

[InlineArray(2)]
public struct DoubleArray2 { private double _element; }

[InlineArray(3)]
public struct DoubleArray3 { private double _element; }

[InlineArray(4)]
public struct DoubleArray4 { private double _element; }

[InlineArray(9)]
public struct DoubleArray9 { private double _element; }

[InlineArray(14)]
public struct Int32Array14 { private int _element; }

/// <summary>Exactly <see cref="RobotHardwareStatus.MotorCount"/> motor states.</summary>
[InlineArray(21)]
public struct MotorStateArray { private MotorState _element; }
