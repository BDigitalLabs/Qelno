namespace Qelno.Core;
using System.Globalization;

public enum ValueType : byte
{
    Null = 0,
    Bool,
    Integer,
    Float,
    String,
    Array
}

public readonly struct Value : IEquatable<Value>, IComparable<Value>
{
    public ValueType Type { get; }
    private readonly long _integer;
    private readonly double _float;
    private readonly object? _ref;

    public static readonly Value Null = new(ValueType.Null, 0, 0, null);
    public static readonly Value True = new(ValueType.Bool, 1, 0, null);
    public static readonly Value False = new(ValueType.Bool, 0, 0, null);

    public Value(ValueType type, long integer, double @float, object? @ref)
    {
        Type = type;
        _integer = integer;
        _float = @float;
        _ref = @ref;
    }

    public static Value FromBool(bool b) => b ? True : False;
    public static Value FromInteger(long val) => new(ValueType.Integer, val, 0, null);
    public static Value FromFloat(double val) => new(ValueType.Float, 0, val, null);
    public static Value FromString(string val) => new(ValueType.String, 0, 0, val ?? "");
    public static Value FromArray(List<Value> list) => new(ValueType.Array, 0, 0, list ?? new List<Value>());

    public bool IsNull => Type == ValueType.Null;
    public bool IsBool => Type == ValueType.Bool;
    public bool IsInteger => Type == ValueType.Integer;
    public bool IsFloat => Type == ValueType.Float;
    public bool IsNumber => Type is ValueType.Integer or ValueType.Float;
    public bool IsString => Type == ValueType.String;
    public bool IsArray => Type == ValueType.Array;

    public long AsInteger()
    {
        return Type switch
        {
            ValueType.Integer => _integer,
            ValueType.Float => (long)_float,
            ValueType.Bool => _integer,
            _ => 0L
        };
    }

    public double AsFloat()
    {
        return Type switch
        {
            ValueType.Float => _float,
            ValueType.Integer => _integer,
            ValueType.Bool => _integer,
            _ => 0.0
        };
    }

    public double AsDouble() => AsFloat();

    public bool AsBool()
    {
        return Type switch
        {
            ValueType.Bool => _integer != 0,
            ValueType.Integer => _integer != 0,
            ValueType.Float => _float != 0.0 && !double.IsNaN(_float),
            ValueType.String => !string.IsNullOrEmpty((string)_ref!),
            ValueType.Array => ((List<Value>)_ref!).Count > 0,
            ValueType.Null => false,
            _ => false
        };
    }

    public string AsString()
    {
        if (Type == ValueType.String)
            return (string)_ref!;
        return ToString();
    }

    public List<Value> AsArray()
    {
        if (Type == ValueType.Array && _ref is List<Value> list)
            return list;
        return new List<Value>();
    }

    public List<Value>? ArrayRef => _ref as List<Value>;

    // Mathematical operations with numeric promotion
    public static (InterpreterResult result, Value value) Add(Value left, Value right)
    {
        if (left.IsString || right.IsString)
        {
            return (InterpreterResult.Success, FromString(left.ToString() + right.ToString()));
        }

        if (left.IsInteger && right.IsInteger)
        {
            try
            {
                checked
                {
                    return (InterpreterResult.Success, FromInteger(left._integer + right._integer));
                }
            }
            catch (OverflowException)
            {
                return (InterpreterResult.Success, FromFloat((double)left._integer + (double)right._integer));
            }
        }

        if (left.IsNumber && right.IsNumber)
        {
            double res = left.AsFloat() + right.AsFloat();
            return (InterpreterResult.Success, FromFloat(res));
        }

        return (InterpreterResult.InvalidValue, Null);
    }

    public static (InterpreterResult result, Value value) Subtract(Value left, Value right)
    {
        if (left.IsInteger && right.IsInteger)
        {
            try
            {
                checked
                {
                    return (InterpreterResult.Success, FromInteger(left._integer - right._integer));
                }
            }
            catch (OverflowException)
            {
                return (InterpreterResult.Success, FromFloat((double)left._integer - (double)right._integer));
            }
        }

        if (left.IsNumber && right.IsNumber)
        {
            double res = left.AsFloat() - right.AsFloat();
            return (InterpreterResult.Success, FromFloat(res));
        }

        return (InterpreterResult.InvalidValue, Null);
    }

    public static (InterpreterResult result, Value value) Multiply(Value left, Value right)
    {
        if (left.IsInteger && right.IsInteger)
        {
            try
            {
                checked
                {
                    return (InterpreterResult.Success, FromInteger(left._integer * right._integer));
                }
            }
            catch (OverflowException)
            {
                return (InterpreterResult.Success, FromFloat((double)left._integer * (double)right._integer));
            }
        }

        if (left.IsNumber && right.IsNumber)
        {
            double res = left.AsFloat() * right.AsFloat();
            return (InterpreterResult.Success, FromFloat(res));
        }

        return (InterpreterResult.InvalidValue, Null);
    }

    public static (InterpreterResult result, Value value) Divide(Value left, Value right)
    {
        if (!left.IsNumber || !right.IsNumber)
            return (InterpreterResult.InvalidValue, Null);

        double rightNum = right.AsFloat();
        if (rightNum == 0.0)
            return (InterpreterResult.DivisionByZero, Null);

        if (left.IsInteger && right.IsInteger)
        {
            long l = left._integer;
            long r = right._integer;
            if (l == long.MinValue && r == -1)
            {
                return (InterpreterResult.Success, FromFloat(-(double)l));
            }
            try
            {
                if (l % r == 0)
                    return (InterpreterResult.Success, FromInteger(l / r));
            }
            catch (OverflowException)
            {
                return (InterpreterResult.Success, FromFloat((double)l / (double)r));
            }
        }

        double res = left.AsFloat() / rightNum;
        return (InterpreterResult.Success, FromFloat(res));
    }

    public static (InterpreterResult result, Value value) Modulo(Value left, Value right)
    {
        if (!left.IsNumber || !right.IsNumber)
            return (InterpreterResult.InvalidValue, Null);

        double rightNum = right.AsFloat();
        if (rightNum == 0.0)
            return (InterpreterResult.DivisionByZero, Null);

        if (left.IsInteger && right.IsInteger)
        {
            long l = left._integer;
            long r = right._integer;
            if (l == long.MinValue && r == -1)
            {
                return (InterpreterResult.Success, FromInteger(0));
            }
            try
            {
                return (InterpreterResult.Success, FromInteger(l % r));
            }
            catch (OverflowException)
            {
                return (InterpreterResult.Success, FromFloat(left.AsFloat() % rightNum));
            }
        }

        return (InterpreterResult.Success, FromFloat(left.AsFloat() % rightNum));
    }

    public static (InterpreterResult result, Value value) Power(Value left, Value right)
    {
        if (!left.IsNumber || !right.IsNumber)
            return (InterpreterResult.InvalidValue, Null);

        if (left.IsInteger && right.IsInteger && right._integer >= 0 && right._integer <= 62)
        {
            try
            {
                long baseVal = left._integer;
                long exp = right._integer;
                long result = 1;
                checked
                {
                    for (int i = 0; i < exp; i++)
                    {
                        result *= baseVal;
                    }
                }
                return (InterpreterResult.Success, FromInteger(result));
            }
            catch (OverflowException)
            {
                // Fall back to double
            }
        }

        double res = Math.Pow(left.AsFloat(), right.AsFloat());
        return (InterpreterResult.Success, FromFloat(res));
    }

    public static Value Negate(Value val)
    {
        if (val.IsInteger)
        {
            try
            {
                checked
                {
                    return FromInteger(-val._integer);
                }
            }
            catch (OverflowException)
            {
                return FromFloat(-(double)val._integer);
            }
        }
        if (val.IsFloat)
            return FromFloat(-val._float);

        return Null;
    }

    // Comparison and equality
    public bool Equals(Value other)
    {
        if (Type != other.Type)
        {
            if (IsNumber && other.IsNumber)
            {
                return AsFloat().Equals(other.AsFloat());
            }
            return false;
        }

        return Type switch
        {
            ValueType.Null => true,
            ValueType.Bool => _integer == other._integer,
            ValueType.Integer => _integer == other._integer,
            ValueType.Float => _float.Equals(other._float),
            ValueType.String => string.Equals((string)_ref!, (string)other._ref!, StringComparison.Ordinal),
            ValueType.Array => ArrayEquals(_ref as List<Value>, other._ref as List<Value>),
            _ => false
        };
    }

    private static bool ArrayEquals(List<Value>? a, List<Value>? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null)
            return false;
        if (a.Count != b.Count)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is Value other && Equals(other);

    public override int GetHashCode()
    {
        return Type switch
        {
            ValueType.Null => 0,
            ValueType.Bool => _integer.GetHashCode(),
            ValueType.Integer => _integer.GetHashCode(),
            ValueType.Float => _float.GetHashCode(),
            ValueType.String => _ref?.GetHashCode() ?? 0,
            ValueType.Array => _ref?.GetHashCode() ?? 0,
            _ => 0
        };
    }

    public int CompareTo(Value other)
    {
        if (IsNumber && other.IsNumber)
        {
            if (IsInteger && other.IsInteger)
                return _integer.CompareTo(other._integer);

            return AsFloat().CompareTo(other.AsFloat());
        }

        if (IsString && other.IsString)
            return string.CompareOrdinal((string)_ref!, (string)other._ref!);

        return 0;
    }

    public static bool operator ==(Value left, Value right) => left.Equals(right);
    public static bool operator !=(Value left, Value right) => !left.Equals(right);
    public static bool operator <(Value left, Value right) => left.CompareTo(right) < 0;
    public static bool operator <=(Value left, Value right) => left.CompareTo(right) <= 0;
    public static bool operator >(Value left, Value right) => left.CompareTo(right) > 0;
    public static bool operator >=(Value left, Value right) => left.CompareTo(right) >= 0;

    public override string ToString()
    {
        return Type switch
        {
            ValueType.Null => "null",
            ValueType.Bool => _integer != 0 ? "true" : "false",
            ValueType.Integer => _integer.ToString(CultureInfo.InvariantCulture),
            ValueType.Float => _float.ToString(CultureInfo.InvariantCulture),
            ValueType.String => (string)(_ref ?? ""),
            ValueType.Array => FormatArray(),
            _ => ""
        };
    }

    private string FormatArray()
    {
        if (_ref is List<Value> valList)
        {
            var parts = new List<string>(valList.Count);
            foreach (var item in valList)
            {
                if (item.IsString)
                    parts.Add($"\"{item.AsString()}\"");
                else
                    parts.Add(item.ToString());
            }
            return $"[{string.Join(", ", parts)}]";
        }
        return "[]";
    }
}
