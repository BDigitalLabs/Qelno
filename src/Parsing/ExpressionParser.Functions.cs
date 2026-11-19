using System;
using System.Collections.Generic;

public partial class ExpressionParser
{
    public (InterpreterResult interpreterResult, string? subject)
        EvaluateFunctionCall(string callString, out object? result)
    {
        result = null;
        string trimmed = callString.Trim();
        int openParen = trimmed.IndexOf('(');
        if (openParen <= 0 || !trimmed.EndsWith(')'))
            return (InterpreterResult.InvalidSyntax, callString);

        string funcName = trimmed[..openParen].Trim();
        string argsContent = trimmed.Substring(openParen + 1, trimmed.Length - openParen - 2).Trim();

        List<object> evaluatedArgs = new();
        if (argsContent.Length > 0)
        {
            List<string> rawArgs = SplitArguments(argsContent);
            foreach (var rawArg in rawArgs)
            {
                string arg = rawArg.Trim();
                if (arg.Length == 0)
                    return (InterpreterResult.InvalidSyntax, callString);

                // String literal?
                if (arg.Length >= 2 &&
                    ((arg[0] == '"' && arg[^1] == '"' && !arg[1..^1].Contains('"')) ||
                     (arg[0] == '\'' && arg[^1] == '\'' && !arg[1..^1].Contains('\''))))
                {
                    evaluatedArgs.Add(arg[1..^1]);
                    continue;
                }

                // Boolean literal?
                if (arg == LanguageNames.True)
                {
                    evaluatedArgs.Add(true);
                    continue;
                }
                if (arg == LanguageNames.False)
                {
                    evaluatedArgs.Add(false);
                    continue;
                }

                // Array literal?
                if (arg.StartsWith('[') && arg.EndsWith(']'))
                {
                    var arrayRes = ParseArrayLiteral(arg, out object? arrVal);
                    if (arrayRes.interpreterResult != InterpreterResult.Success)
                        return arrayRes;

                    evaluatedArgs.Add(arrVal!);
                    continue;
                }

                // Variable?
                if (variables.TryGetVariable(arg, out object? varVal) && !arg.Contains('(') && !arg.Contains('['))
                {
                    evaluatedArgs.Add(varVal!);
                    continue;
                }

                // Evaluate as an expression
                var evalRes = EvaluateExpression(new List<string> { arg }, out object? argVal);
                if (evalRes.interpreterResult != InterpreterResult.Success)
                    return evalRes;

                evaluatedArgs.Add(argVal!);
            }
        }

        if (context == null)
            return (InterpreterResult.UnknownCommand, funcName);

        var invokeResult = context.InvokeFunction(funcName, evaluatedArgs);
        if (invokeResult.interpreterResult != InterpreterResult.Success)
            return (invokeResult.interpreterResult, invokeResult.subject);

        result = invokeResult.returnValue;
        return (InterpreterResult.Success, null);
    }

    public static List<string> SplitArguments(string source)
    {
        List<string> args = new();
        int depth = 0;
        char inString = '\0';
        int start = 0;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (inString != '\0')
            {
                if (c == inString) inString = '\0';
            }
            else if (c == '"' || c == '\'')
            {
                inString = c;
            }
            else if (c == '(' || c == '[')
            {
                depth++;
            }
            else if (c == ')' || c == ']')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                args.Add(source[start..i]);
                start = i + 1;
            }
        }

        if (start < source.Length)
        {
            args.Add(source[start..]);
        }

        return args;
    }

    public static bool IsSingleFunctionCall(string source)
    {
        string trimmed = source.Trim();
        int openParen = trimmed.IndexOf('(');
        if (openParen <= 0 || !trimmed.EndsWith(')'))
            return false;

        string funcName = trimmed[..openParen].Trim();
        if (!SyntaxValidator.IsValidIdentifier(funcName) || LanguageNames.Contains(funcName))
            return false;

        int depth = 0;
        char inString = '\0';
        for (int i = openParen; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (inString != '\0')
            {
                if (c == inString) inString = '\0';
            }
            else if (c == '"' || c == '\'')
            {
                inString = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i == trimmed.Length - 1;
                }
            }
        }

        return false;
    }
}