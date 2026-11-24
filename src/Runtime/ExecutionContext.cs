namespace Qelno.Runtime;

using Qelno.Core;
using Qelno.Instructions;
using Qelno.Parsing;

public class ExecutionContext
{
    public int Ip { get; set; }
    public bool Terminated { get; set; }
    public bool IsReturning { get; set; }
    public Value? LastReturnValue { get; set; }
    public int CallDepth { get; set; }
    public const int MaxCallDepth = 500;
    public int? CurrentErrorLine { get; set; }

    public Variables Variables { get; }
    public ExpressionParser ExpressionParser { get; }
    public FunctionRegistry Functions { get; }

    public ExecutionContext(
        Variables variables,
        ExpressionParser expressionParser,
        FunctionRegistry? functions = null)
    {
        Variables = variables;
        ExpressionParser = expressionParser;
        Functions = functions ?? new FunctionRegistry();
    }

    public (InterpreterResult interpreterResult, string? subject, Value? returnValue)
        InvokeFunction(string functionName, List<Value> args)
    {
        if (CallDepth >= MaxCallDepth)
            return (InterpreterResult.CallDepthExceeded, functionName, null);

        if (!Functions.TryGetFunction(functionName, out var function))
            return (InterpreterResult.FunctionNotFound, functionName, null);

        if (function is NativeFunction nativeFn)
        {
            if (nativeFn.ParameterCount >= 0 && args.Count != nativeFn.ParameterCount)
                return (InterpreterResult.InvalidParameterCount, functionName, null);

            var (res, subj, val) = nativeFn.Invoker(this, args);
            return (res, subj, val);
        }

        if (function is UserFunction userFn)
        {
            if (args.Count != userFn.Parameters.Count)
                return (InterpreterResult.InvalidParameterCount, functionName, null);

            var localScope = new Dictionary<string, Value>(StringComparer.Ordinal);
            for (int i = 0; i < userFn.Parameters.Count; i++)
            {
                localScope[userFn.Parameters[i]] = args[i];
            }

            Variables.PushScope(localScope);
            int prevIp = Ip;
            bool prevReturning = IsReturning;
            Value? prevReturnValue = LastReturnValue;
            IsReturning = false;
            LastReturnValue = null;
            CallDepth++;

            try
            {
                int localIp = 0;
                while (localIp >= 0 && localIp < userFn.Body.Count && !Terminated && !IsReturning)
                {
                    var instruction = userFn.Body[localIp];
                    Ip = localIp;
                    var result = instruction.Execute(this);
                    if (result.interpreterResult != InterpreterResult.Success)
                    {
                        CurrentErrorLine ??= instruction.LineNumber;
                        return (result.interpreterResult, result.subject, null);
                    }

                    localIp = Ip;
                }

                Value? userResult = LastReturnValue;
                return (InterpreterResult.Success, null, userResult);
            }
            finally
            {
                CallDepth--;
                Variables.PopScope();
                Ip = prevIp;
                IsReturning = prevReturning;
                LastReturnValue = prevReturnValue;
            }
        }

        return (InterpreterResult.UnknownCommand, functionName, null);
    }
}
