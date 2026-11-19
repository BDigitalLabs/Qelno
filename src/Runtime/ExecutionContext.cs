using System;
using System.Collections.Generic;

public class ExecutionContext
{
    public int Ip { get; set; }
    public bool Terminated { get; set; }
    public bool IsReturning { get; set; }
    public object? LastReturnValue { get; set; }
    public int CallDepth { get; set; }
    public const int MaxCallDepth = 500;

    public Variables Variables { get; }
    public Builtins Builtins { get; }
    public ExpressionParser ExpressionParser { get; }
    public FunctionRegistry Functions { get; }

    public ExecutionContext(
        Variables variables,
        Builtins builtins,
        ExpressionParser expressionParser,
        FunctionRegistry? functions = null)
    {
        Variables = variables;
        Builtins = builtins;
        ExpressionParser = expressionParser;
        Functions = functions ?? new FunctionRegistry();
    }

    public (InterpreterResult interpreterResult, string? subject, object? returnValue)
        InvokeFunction(string functionName, List<object> args)
    {
        if (CallDepth >= MaxCallDepth)
            return (InterpreterResult.InvalidValue, "Stack overflow: maximum call depth exceeded.", null);

        if (!Functions.TryGetFunction(functionName, out var function))
            return (InterpreterResult.FunctionNotFound, functionName, null);

        if (function is NativeFunction nativeFn)
        {
            if (nativeFn.ParameterCount >= 0 && args.Count != nativeFn.ParameterCount)
                return (InterpreterResult.InvalidParameterCount, functionName, null);

            return nativeFn.Invoker(this, args);
        }

        if (function is UserFunction userFn)
        {
            if (args.Count != userFn.Parameters.Count)
                return (InterpreterResult.InvalidParameterCount, functionName, null);

            var localScope = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < userFn.Parameters.Count; i++)
            {
                localScope[userFn.Parameters[i]] = args[i];
            }

            Variables.PushScope(localScope);
            int prevIp = Ip;
            bool prevReturning = IsReturning;
            object? prevReturnValue = LastReturnValue;
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
                        return (result.interpreterResult, result.subject, null);

                    localIp = Ip;
                }

                object? userResult = LastReturnValue;
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
