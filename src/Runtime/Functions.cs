namespace Qelno.Runtime;

using Qelno.Core;
using Qelno.Instructions;
using System.Globalization;

public interface IFunction
{
    string Name { get; }
    int ParameterCount { get; }
}

public delegate (InterpreterResult result, string? subject, Value value) NativeFunctionInvoker(
    ExecutionContext context,
    List<Value> args);

public class NativeFunction : IFunction
{
    public string Name { get; }
    public int ParameterCount { get; }
    public NativeFunctionInvoker Invoker { get; }

    public NativeFunction(string name, int parameterCount, NativeFunctionInvoker invoker)
    {
        Name = name;
        ParameterCount = parameterCount;
        Invoker = invoker;
    }
}

public class UserFunction : IFunction
{
    public string Name { get; }
    public List<string> Parameters { get; }
    public int ParameterCount => Parameters.Count;
    public List<Instruction> Body { get; }

    public UserFunction(string name, List<string> parameters, List<Instruction> body)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
    }
}

public class FunctionRegistry
{
    private readonly Dictionary<string, IFunction> functions = new(StringComparer.Ordinal);

    public FunctionRegistry()
    {
        RegisterBuiltinFunctions();
    }

    private void RegisterBuiltinFunctions()
    {
        RegisterNative(new NativeFunction("Abs", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg.IsInteger)
            {
                long l = arg.AsInteger();
                if (l == long.MinValue)
                    return (InterpreterResult.Success, null, Value.FromFloat(-(double)l));
                return (InterpreterResult.Success, null, Value.FromInteger(Math.Abs(l)));
            }

            if (arg.IsFloat)
            {
                return (InterpreterResult.Success, null, Value.FromFloat(Math.Abs(arg.AsFloat())));
            }

            if (arg.IsString)
            {
                string s = arg.AsString();
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                {
                    if (l == long.MinValue)
                        return (InterpreterResult.Success, null, Value.FromFloat(-(double)l));
                    return (InterpreterResult.Success, null, Value.FromInteger(Math.Abs(l)));
                }
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    return (InterpreterResult.Success, null, Value.FromFloat(Math.Abs(d)));
                }
            }

            return (InterpreterResult.InvalidValue, arg.ToString(), Value.Null);
        }));

        RegisterNative(new NativeFunction("Sqrt", 1, (context, args) =>
        {
            if (!TryGetDouble(args[0], out double val))
                return (InterpreterResult.InvalidValue, args[0].ToString(), Value.Null);

            if (val < 0 || double.IsNaN(val))
                return (InterpreterResult.InvalidValue, args[0].ToString(), Value.Null);

            double sqrt = Math.Sqrt(val);
            if (sqrt >= long.MinValue && sqrt <= long.MaxValue && Math.Floor(sqrt) == sqrt && args[0].IsInteger)
            {
                return (InterpreterResult.Success, null, Value.FromInteger((long)sqrt));
            }

            return (InterpreterResult.Success, null, Value.FromFloat(sqrt));
        }));

        RegisterNative(new NativeFunction("Pow", 2, (context, args) =>
        {
            var baseVal = args[0];
            var expVal = args[1];

            if (baseVal.IsString)
            {
                if (long.TryParse(baseVal.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    baseVal = Value.FromInteger(l);
                else if (double.TryParse(baseVal.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    baseVal = Value.FromFloat(d);
                else
                    return (InterpreterResult.InvalidValue, null, Value.Null);
            }
            if (expVal.IsString)
            {
                if (long.TryParse(expVal.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    expVal = Value.FromInteger(l);
                else if (double.TryParse(expVal.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    expVal = Value.FromFloat(d);
                else
                    return (InterpreterResult.InvalidValue, null, Value.Null);
            }

            var res = Value.Power(baseVal, expVal);
            if (res.result != InterpreterResult.Success)
                return (res.result, null, Value.Null);

            return (InterpreterResult.Success, null, res.value);
        }));

        RegisterNative(new NativeFunction("Min", 2, (context, args) =>
        {
            var v1 = args[0];
            var v2 = args[1];

            if (v1.IsString)
            {
                if (long.TryParse(v1.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) v1 = Value.FromInteger(l);
                else if (double.TryParse(v1.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) v1 = Value.FromFloat(d);
                else return (InterpreterResult.InvalidValue, null, Value.Null);
            }
            if (v2.IsString)
            {
                if (long.TryParse(v2.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) v2 = Value.FromInteger(l);
                else if (double.TryParse(v2.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) v2 = Value.FromFloat(d);
                else return (InterpreterResult.InvalidValue, null, Value.Null);
            }

            if (!v1.IsNumber || !v2.IsNumber)
                return (InterpreterResult.InvalidValue, null, Value.Null);

            if (v1.IsInteger && v2.IsInteger)
                return (InterpreterResult.Success, null, Value.FromInteger(Math.Min(v1.AsInteger(), v2.AsInteger())));

            return (InterpreterResult.Success, null, Value.FromFloat(Math.Min(v1.AsFloat(), v2.AsFloat())));
        }));

        RegisterNative(new NativeFunction("Max", 2, (context, args) =>
        {
            var v1 = args[0];
            var v2 = args[1];

            if (v1.IsString)
            {
                if (long.TryParse(v1.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) v1 = Value.FromInteger(l);
                else if (double.TryParse(v1.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) v1 = Value.FromFloat(d);
                else return (InterpreterResult.InvalidValue, null, Value.Null);
            }
            if (v2.IsString)
            {
                if (long.TryParse(v2.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) v2 = Value.FromInteger(l);
                else if (double.TryParse(v2.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) v2 = Value.FromFloat(d);
                else return (InterpreterResult.InvalidValue, null, Value.Null);
            }

            if (!v1.IsNumber || !v2.IsNumber)
                return (InterpreterResult.InvalidValue, null, Value.Null);

            if (v1.IsInteger && v2.IsInteger)
                return (InterpreterResult.Success, null, Value.FromInteger(Math.Max(v1.AsInteger(), v2.AsInteger())));

            return (InterpreterResult.Success, null, Value.FromFloat(Math.Max(v1.AsFloat(), v2.AsFloat())));
        }));

        RegisterNative(new NativeFunction("Int", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg.IsInteger)
                return (InterpreterResult.Success, null, arg);

            if (arg.IsBool)
                return (InterpreterResult.Success, null, Value.FromInteger(arg.AsBool() ? 1L : 0L));

            if (arg.IsFloat)
            {
                double d = arg.AsFloat();
                if (double.IsNaN(d) || double.IsInfinity(d) || d > long.MaxValue || d < long.MinValue)
                    return (InterpreterResult.InvalidValue, arg.ToString(), Value.Null);

                return (InterpreterResult.Success, null, Value.FromInteger((long)Math.Truncate(d)));
            }

            if (arg.IsString)
            {
                string s = arg.AsString();
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    return (InterpreterResult.Success, null, Value.FromInteger(l));

                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    if (double.IsNaN(d) || double.IsInfinity(d) || d > long.MaxValue || d < long.MinValue)
                        return (InterpreterResult.InvalidValue, s, Value.Null);

                    return (InterpreterResult.Success, null, Value.FromInteger((long)Math.Truncate(d)));
                }

                return (InterpreterResult.InvalidValue, s, Value.Null);
            }

            return (InterpreterResult.InvalidValue, arg.ToString(), Value.Null);
        }));

        RegisterNative(new NativeFunction("Float", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg.IsFloat)
                return (InterpreterResult.Success, null, arg);

            if (arg.IsInteger)
                return (InterpreterResult.Success, null, Value.FromFloat((double)arg.AsInteger()));

            if (arg.IsBool)
                return (InterpreterResult.Success, null, Value.FromFloat(arg.AsBool() ? 1.0 : 0.0));

            if (arg.IsString)
            {
                string s = arg.AsString();
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    return (InterpreterResult.Success, null, Value.FromFloat(d));

                return (InterpreterResult.InvalidValue, s, Value.Null);
            }

            return (InterpreterResult.InvalidValue, arg.ToString(), Value.Null);
        }));

        RegisterNative(new NativeFunction("Str", 1, (context, args) =>
        {
            var arg = args[0];
            return (InterpreterResult.Success, null, Value.FromString(arg.ToString()));
        }));

        RegisterNative(new NativeFunction("Bool", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg.IsBool)
                return (InterpreterResult.Success, null, arg);

            if (arg.IsInteger)
                return (InterpreterResult.Success, null, Value.FromBool(arg.AsInteger() != 0));

            if (arg.IsFloat)
                return (InterpreterResult.Success, null, Value.FromBool(arg.AsFloat() != 0.0 && !double.IsNaN(arg.AsFloat())));

            if (arg.IsString)
            {
                string s = arg.AsString();
                if (bool.TryParse(s, out bool parsed))
                    return (InterpreterResult.Success, null, Value.FromBool(parsed));

                return (InterpreterResult.Success, null, Value.FromBool(s.Length > 0));
            }

            if (arg.IsArray)
                return (InterpreterResult.Success, null, Value.FromBool(arg.AsArray().Count > 0));

            return (InterpreterResult.Success, null, Value.FromBool(!arg.IsNull));
        }));

        RegisterNative(new NativeFunction("TypeOf", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg.IsArray)
                return (InterpreterResult.Success, null, Value.FromString("array"));

            if (arg.IsBool)
                return (InterpreterResult.Success, null, Value.FromString("bool"));

            if (arg.IsNumber)
                return (InterpreterResult.Success, null, Value.FromString("number"));

            if (arg.IsString)
                return (InterpreterResult.Success, null, Value.FromString("string"));

            return (InterpreterResult.Success, null, Value.FromString("unknown"));
        }));

        RegisterNative(new NativeFunction("Round", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg.IsInteger)
                return (InterpreterResult.Success, null, arg);

            if (!TryGetDouble(arg, out double d))
                return (InterpreterResult.InvalidValue, arg.ToString(), Value.Null);

            double rounded = Math.Round(d, MidpointRounding.AwayFromZero);
            if (rounded >= long.MinValue && rounded <= long.MaxValue)
                return (InterpreterResult.Success, null, Value.FromInteger((long)rounded));

            return (InterpreterResult.Success, null, Value.FromFloat(rounded));
        }));

        RegisterNative(new NativeFunction("Len", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg.IsArray)
            {
                if (arg.ArrayRef is { } lv) return (InterpreterResult.Success, null, Value.FromInteger(lv.Count));
                return (InterpreterResult.Success, null, Value.FromInteger(0));
            }

            if (arg.IsString)
                return (InterpreterResult.Success, null, Value.FromInteger(arg.AsString().Length));

            string str = arg.ToString();
            return (InterpreterResult.Success, null, Value.FromInteger(str.Length));
        }));

        RegisterNative(new NativeFunction("Push", 2, (context, args) =>
        {
            var target = args[0];
            if (target.IsArray && target.ArrayRef is { } valList)
            {
                valList.Add(args[1]);
                return (InterpreterResult.Success, null, Value.FromInteger(valList.Count));
            }

            return (InterpreterResult.InvalidValue, target.ToString(), Value.Null);
        }));

        RegisterNative(new NativeFunction("Pop", 1, (context, args) =>
        {
            var target = args[0];
            if (target.IsArray && target.ArrayRef is { } valList)
            {
                if (valList.Count == 0)
                    return (InterpreterResult.InvalidValue, "Cannot pop from an empty array.", Value.Null);

                Value last = valList[^1];
                valList.RemoveAt(valList.Count - 1);
                return (InterpreterResult.Success, null, last);
            }

            return (InterpreterResult.InvalidValue, target.ToString(), Value.Null);
        }));

        RegisterNative(new NativeFunction("ToUpper", 1, (context, args) =>
        {
            string s = args[0].AsString();
            return (InterpreterResult.Success, null, Value.FromString(s.ToUpperInvariant()));
        }));

        RegisterNative(new NativeFunction("ToLower", 1, (context, args) =>
        {
            string s = args[0].AsString();
            return (InterpreterResult.Success, null, Value.FromString(s.ToLowerInvariant()));
        }));

        RegisterNative(new NativeFunction("Substr", 3, (context, args) =>
        {
            string s = args[0].AsString();
            if (!TryGetDouble(args[1], out double startD) || !TryGetDouble(args[2], out double lenD))
                return (InterpreterResult.InvalidValue, null, Value.Null);

            int start = (int)startD;
            int len = (int)lenD;

            if (start < 0) start = 0;
            if (start >= s.Length || len <= 0)
                return (InterpreterResult.Success, null, Value.FromString(""));

            if (start + len > s.Length)
                len = s.Length - start;

            return (InterpreterResult.Success, null, Value.FromString(s.Substring(start, len)));
        }));

        RegisterNative(new NativeFunction("Trim", 1, (context, args) =>
        {
            var arg = args[0];
            if (!arg.IsString)
                return (InterpreterResult.InvalidValue, arg.ToString(), Value.Null);

            return (InterpreterResult.Success, null, Value.FromString(arg.AsString().Trim()));
        }));

        RegisterNative(new NativeFunction("Contains", 2, (context, args) =>
        {
            var target = args[0];
            if (target.IsArray)
            {
                var arr = target.AsArray();
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i].Equals(args[1]))
                        return (InterpreterResult.Success, null, Value.True);
                }
                return (InterpreterResult.Success, null, Value.False);
            }

            if (!target.IsString)
                return (InterpreterResult.InvalidValue, target.ToString(), Value.Null);
            if (!args[1].IsString)
                return (InterpreterResult.InvalidValue, args[1].ToString(), Value.Null);

            string s = target.AsString();
            string sub = args[1].AsString();
            return (InterpreterResult.Success, null, Value.FromBool(s.Contains(sub, StringComparison.Ordinal)));
        }));

        RegisterNative(new NativeFunction("Replace", 3, (context, args) =>
        {
            if (!args[0].IsString)
                return (InterpreterResult.InvalidValue, args[0].ToString(), Value.Null);
            if (!args[1].IsString)
                return (InterpreterResult.InvalidValue, args[1].ToString(), Value.Null);
            if (!args[2].IsString)
                return (InterpreterResult.InvalidValue, args[2].ToString(), Value.Null);

            string s = args[0].AsString();
            string oldVal = args[1].AsString();
            string newVal = args[2].AsString();

            if (oldVal.Length == 0)
                return (InterpreterResult.Success, null, args[0]);

            return (InterpreterResult.Success, null, Value.FromString(s.Replace(oldVal, newVal, StringComparison.Ordinal)));
        }));

        RegisterNative(new NativeFunction("Split", 2, (context, args) =>
        {
            if (!args[0].IsString)
                return (InterpreterResult.InvalidValue, args[0].ToString(), Value.Null);
            if (!args[1].IsString)
                return (InterpreterResult.InvalidValue, args[1].ToString(), Value.Null);

            string s = args[0].AsString();
            string delim = args[1].AsString();

            List<Value> items = new();
            if (delim.Length == 0)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    items.Add(Value.FromString(s[i].ToString()));
                }
            }
            else
            {
                string[] parts = s.Split(delim);
                for (int i = 0; i < parts.Length; i++)
                {
                    items.Add(Value.FromString(parts[i]));
                }
            }

            return (InterpreterResult.Success, null, Value.FromArray(items));
        }));

        RegisterNative(new NativeFunction("Clone", 1, (context, args) =>
        {
            var target = args[0];
            if (!target.IsArray)
                return (InterpreterResult.InvalidValue, target.ToString(), Value.Null);

            var orig = target.AsArray();
            var copy = new List<Value>(orig.Count);
            for (int i = 0; i < orig.Count; i++)
            {
                copy.Add(orig[i]);
            }
            return (InterpreterResult.Success, null, Value.FromArray(copy));
        }));

        RegisterNative(new NativeFunction("Exit", 0, (context, args) =>
        {
            context.Terminated = true;
            context.LastReturnValue = Value.FromInteger(0);
            return (InterpreterResult.Success, null, Value.FromInteger(0));
        }));

        RegisterNative(new NativeFunction("Cout", -1, (context, args) =>
        {
            if (args.Count > 1)
                return (InterpreterResult.InvalidParameterCount, "Cout", Value.Null);

            if (args.Count == 0)
            {
                Console.WriteLine();
                return (InterpreterResult.Success, null, Value.Null);
            }

            Console.WriteLine(args[0].ToString());
            return (InterpreterResult.Success, null, Value.Null);
        }));

        RegisterNative(new NativeFunction("Cin", -1, (context, args) =>
        {
            if (args.Count > 1)
                return (InterpreterResult.InvalidParameterCount, "Cin", Value.Null);

            if (args.Count == 1)
            {
                string prompt = args[0].ToString();
                if (!string.IsNullOrEmpty(prompt))
                {
                    Console.Write(prompt);
                }
            }

            string? line = Console.ReadLine();
            if (line == null)
                return (InterpreterResult.InputFailed, "Input failed!", Value.Null);

            return (InterpreterResult.Success, null, Value.FromString(line));
        }));
    }

    public static readonly HashSet<string> BuiltinNames = new(StringComparer.Ordinal)
    {
        "Abs", "Sqrt", "Pow", "Min", "Max", "Exit",
        "Len", "Substr", "ToUpper", "ToLower",
        "Int", "Float", "Str", "Bool", "Round", "TypeOf",
        "Push", "Pop", "Cout", "Cin",
        "Clone", "Contains", "Replace", "Split", "Trim"
    };

    public static bool IsBuiltinFunction(string name)
    {
        return BuiltinNames.Contains(name);
    }

    public static bool TryGetDouble(Value val, out double d)
    {
        if (val.IsFloat) { d = val.AsFloat(); return true; }
        if (val.IsInteger) { d = (double)val.AsInteger(); return true; }
        if (val.IsString && double.TryParse(val.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
        {
            return true;
        }
        d = 0;
        return false;
    }

    public void RegisterNative(NativeFunction function)
    {
        functions[function.Name] = function;
    }

    public (InterpreterResult result, string? subject) RegisterUserFunction(UserFunction function)
    {
        if (IsBuiltinFunction(function.Name))
            return (InterpreterResult.InvalidName, function.Name);

        functions[function.Name] = function;
        return (InterpreterResult.Success, null);
    }

    public bool TryGetFunction(string name, out IFunction? function)
    {
        return functions.TryGetValue(name, out function);
    }
}
