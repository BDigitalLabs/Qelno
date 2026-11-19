using System;
using System.Collections.Generic;
using System.Globalization;

public interface IFunction
{
    string Name { get; }
    int ParameterCount { get; }
}

public delegate (InterpreterResult result, string? subject, object? value) NativeFunctionInvoker(
    ExecutionContext context,
    List<object> args);

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
            if (!TryGetDouble(args[0], out double val))
                return (InterpreterResult.InvalidValue, args[0]?.ToString(), null);

            return (InterpreterResult.Success, null, Math.Abs(val));
        }));

        RegisterNative(new NativeFunction("Sqrt", 1, (context, args) =>
        {
            if (!TryGetDouble(args[0], out double val))
                return (InterpreterResult.InvalidValue, args[0]?.ToString(), null);

            if (val < 0)
                return (InterpreterResult.InvalidValue, args[0]?.ToString(), null);

            return (InterpreterResult.Success, null, Math.Sqrt(val));
        }));

        RegisterNative(new NativeFunction("Pow", 2, (context, args) =>
        {
            if (!TryGetDouble(args[0], out double val1) || !TryGetDouble(args[1], out double val2))
                return (InterpreterResult.InvalidValue, null, null);

            return (InterpreterResult.Success, null, Math.Pow(val1, val2));
        }));

        RegisterNative(new NativeFunction("Min", 2, (context, args) =>
        {
            if (!TryGetDouble(args[0], out double val1) || !TryGetDouble(args[1], out double val2))
                return (InterpreterResult.InvalidValue, null, null);

            return (InterpreterResult.Success, null, Math.Min(val1, val2));
        }));

        RegisterNative(new NativeFunction("Max", 2, (context, args) =>
        {
            if (!TryGetDouble(args[0], out double val1) || !TryGetDouble(args[1], out double val2))
                return (InterpreterResult.InvalidValue, null, null);

            return (InterpreterResult.Success, null, Math.Max(val1, val2));
        }));

        RegisterNative(new NativeFunction("Int", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg is bool b)
                return (InterpreterResult.Success, null, b ? 1L : 0L);

            if (TryGetDouble(arg, out double d))
                return (InterpreterResult.Success, null, (long)Math.Truncate(d));

            return (InterpreterResult.InvalidValue, arg?.ToString(), null);
        }));

        RegisterNative(new NativeFunction("Float", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg is bool b)
                return (InterpreterResult.Success, null, b ? 1.0 : 0.0);

            if (TryGetDouble(arg, out double d))
                return (InterpreterResult.Success, null, d);

            return (InterpreterResult.InvalidValue, arg?.ToString(), null);
        }));

        RegisterNative(new NativeFunction("Str", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg is bool b)
                return (InterpreterResult.Success, null, b ? "true" : "false");

            if (arg is double d)
                return (InterpreterResult.Success, null, d.ToString(CultureInfo.InvariantCulture));

            if (arg is IFormattable f)
                return (InterpreterResult.Success, null, f.ToString(null, CultureInfo.InvariantCulture));

            return (InterpreterResult.Success, null, arg?.ToString() ?? "");
        }));

        RegisterNative(new NativeFunction("Bool", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg is bool b)
                return (InterpreterResult.Success, null, b);

            if (TryGetDouble(arg, out double d))
                return (InterpreterResult.Success, null, d != 0.0);

            if (arg is string s)
            {
                if (bool.TryParse(s, out bool parsed))
                    return (InterpreterResult.Success, null, parsed);

                return (InterpreterResult.Success, null, s.Length > 0);
            }

            return (InterpreterResult.Success, null, arg != null);
        }));

        RegisterNative(new NativeFunction("TypeOf", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg is List<object>)
                return (InterpreterResult.Success, null, "array");

            if (arg is bool)
                return (InterpreterResult.Success, null, "bool");

            if (arg is double or long or int or float)
                return (InterpreterResult.Success, null, "number");

            if (arg is string)
                return (InterpreterResult.Success, null, "string");

            return (InterpreterResult.Success, null, "unknown");
        }));

        RegisterNative(new NativeFunction("Round", 1, (context, args) =>
        {
            if (!TryGetDouble(args[0], out double d))
                return (InterpreterResult.InvalidValue, args[0]?.ToString(), null);

            return (InterpreterResult.Success, null, Math.Round(d, MidpointRounding.AwayFromZero));
        }));

        RegisterNative(new NativeFunction("Len", 1, (context, args) =>
        {
            var arg = args[0];
            if (arg is List<object> list)
                return (InterpreterResult.Success, null, (long)list.Count);

            if (arg is string s)
                return (InterpreterResult.Success, null, (long)s.Length);

            string str = arg?.ToString() ?? "";
            return (InterpreterResult.Success, null, (long)str.Length);
        }));

        RegisterNative(new NativeFunction("Push", 2, (context, args) =>
        {
            if (args[0] is not List<object> list)
                return (InterpreterResult.InvalidValue, args[0]?.ToString(), null);

            list.Add(args[1]);
            return (InterpreterResult.Success, null, (long)list.Count);
        }));

        RegisterNative(new NativeFunction("Pop", 1, (context, args) =>
        {
            if (args[0] is not List<object> list)
                return (InterpreterResult.InvalidValue, args[0]?.ToString(), null);

            if (list.Count == 0)
                return (InterpreterResult.InvalidValue, "Cannot pop from an empty array.", null);

            object last = list[^1];
            list.RemoveAt(list.Count - 1);
            return (InterpreterResult.Success, null, last);
        }));

        RegisterNative(new NativeFunction("ToUpper", 1, (context, args) =>
        {
            string s = args[0]?.ToString() ?? "";
            return (InterpreterResult.Success, null, s.ToUpperInvariant());
        }));

        RegisterNative(new NativeFunction("ToLower", 1, (context, args) =>
        {
            string s = args[0]?.ToString() ?? "";
            return (InterpreterResult.Success, null, s.ToLowerInvariant());
        }));

        RegisterNative(new NativeFunction("Substr", 3, (context, args) =>
        {
            string s = args[0]?.ToString() ?? "";
            if (!TryGetDouble(args[1], out double startD) || !TryGetDouble(args[2], out double lenD))
                return (InterpreterResult.InvalidValue, null, null);

            int start = (int)startD;
            int len = (int)lenD;

            if (start < 0) start = 0;
            if (start >= s.Length || len <= 0)
                return (InterpreterResult.Success, null, "");

            if (start + len > s.Length)
                len = s.Length - start;

            return (InterpreterResult.Success, null, s.Substring(start, len));
        }));

        RegisterNative(new NativeFunction("Exit", 0, (context, args) =>
        {
            context.Terminated = true;
            context.LastReturnValue = 0L;
            return (InterpreterResult.Success, null, 0L);
        }));
    }

    public static bool TryGetDouble(object? obj, out double val)
    {
        if (obj is double d) { val = d; return true; }
        if (obj is long l) { val = l; return true; }
        if (obj is int i) { val = i; return true; }
        if (obj is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
        {
            val = d;
            return true;
        }
        val = 0;
        return false;
    }

    public void RegisterNative(NativeFunction function)
    {
        functions[function.Name] = function;
    }

    public void RegisterUserFunction(UserFunction function)
    {
        functions[function.Name] = function;
    }

    public bool TryGetFunction(string name, out IFunction? function)
    {
        return functions.TryGetValue(name, out function);
    }
}
