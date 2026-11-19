using System;
using System.Collections.Generic;
using System.Globalization;

public partial class ExpressionParser
{
    private readonly Variables variables;
    private readonly Builtins builtins;
    private readonly NumericArgumentResolver numericArgumentResolver;
    private ExecutionContext? context;

    public ExpressionParser(
        Variables variables,
        Builtins builtins,
        ExecutionContext? context = null)
    {
        this.variables = variables;
        this.builtins = builtins;
        this.context = context;
        numericArgumentResolver = new NumericArgumentResolver(
            variables,
            builtins);
    }

    public void SetContext(ExecutionContext ctx)
    {
        context = ctx;
    }

    private (InterpreterResult interpreterResult, string? subject)
        ResolveTokens(
            List<string> tokens,
            out List<string> resolved)
    {
        resolved = new(tokens.Count);

        foreach (string token in tokens)
        {
            if (ExpressionConverter.IsExpressionOperator(token))
            {
                resolved.Add(token);
                continue;
            }

            if (token is "true" or "false")
            {
                resolved.Add(token);
                continue;
            }

            // String literal?
            if (token.Length >= 2 &&
                ((token[0] == '"' && token[^1] == '"') ||
                 (token[0] == '\'' && token[^1] == '\'')))
            {
                resolved.Add($"\"{token[1..^1]}\"");
                continue;
            }

            // Function call?
            int openParen = token.IndexOf('(');
            if (openParen > 0 && token.EndsWith(')'))
            {
                var funcEvalResult = EvaluateFunctionCall(token, out object? funcResult);
                if (funcEvalResult.interpreterResult != InterpreterResult.Success)
                    return funcEvalResult;

                if (funcResult is bool b)
                {
                    resolved.Add(b ? "true" : "false");
                }
                else if (funcResult is string s)
                {
                    resolved.Add($"\"{s}\"");
                }
                else if (FunctionRegistry.TryGetDouble(funcResult, out double num))
                {
                    resolved.Add(num.ToString("R", CultureInfo.InvariantCulture));
                }
                else if (funcResult != null)
                {
                    resolved.Add(funcResult.ToString()!);
                }
                else
                {
                    resolved.Add("0");
                }
                continue;
            }

            // Index access? (e.g.: arr[0], arr[i + 1])
            if (IsSingleIndexAccess(token))
            {
                var indexEvalResult = EvaluateIndexAccess(token, out object? indexResult);
                if (indexEvalResult.interpreterResult != InterpreterResult.Success)
                    return indexEvalResult;

                if (indexResult is bool b)
                {
                    resolved.Add(b ? "true" : "false");
                }
                else if (indexResult is string s)
                {
                    resolved.Add($"\"{s}\"");
                }
                else if (FunctionRegistry.TryGetDouble(indexResult, out double num))
                {
                    resolved.Add(num.ToString("R", CultureInfo.InvariantCulture));
                }
                else if (indexResult != null)
                {
                    resolved.Add(indexResult.ToString()!);
                }
                else
                {
                    resolved.Add("0");
                }
                continue;
            }

            if (variables.TryGetVariable(token, out object? value))
            {
                if (value is bool booleanValue)
                {
                    resolved.Add(booleanValue ? "true" : "false");
                    continue;
                }

                if (value is string stringValue)
                {
                    resolved.Add($"\"{stringValue}\"");
                    continue;
                }
            }

            var result =
                numericArgumentResolver.ResolveNumericArgument(
                    token,
                    out double number);

            if (result.interpreterResult != InterpreterResult.Success)
                return result;

            resolved.Add(
                number.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
        }

        return (InterpreterResult.Success, null);
    }

    public (InterpreterResult interpreterResult, string? subject)
        EvaluateExpression(List<string>? parts, out object? result)
    {
        result = null;
        if (parts == null || parts.Count == 0)
            return (InterpreterResult.PartsAreEmptyOrNull, null);

        string source = string.Join(" ", parts).Trim();

        // Array literal?
        if (source.StartsWith('[') && source.EndsWith(']'))
        {
            return ParseArrayLiteral(source, out result);
        }

        // Single quoted string literal?
        if (source.Length >= 2 &&
            ((source[0] == '"' && source[^1] == '"' && !source[1..^1].Contains('"')) ||
             (source[0] == '\'' && source[^1] == '\'' && !source[1..^1].Contains('\''))))
        {
            result = source[1..^1];
            return (InterpreterResult.Success, null);
        }

        // Single index access? (e.g.: arr[0])
        if (IsSingleIndexAccess(source))
        {
            return EvaluateIndexAccess(source, out result);
        }

        // Single variable?
        if (parts.Count == 1 && variables.TryGetVariable(source, out object? varVal))
        {
            result = varVal;
            return (InterpreterResult.Success, null);
        }

        // Single function call?
        if (IsSingleFunctionCall(source))
        {
            return EvaluateFunctionCall(source, out result);
        }

        return EvaluateExpressionTokens(parts, out result);
    }

    private (InterpreterResult interpreterResult, string? subject)
    EvaluateExpressionTokens(
        List<string>? parts,
        out object? result)
    {
        result = null;

        if (parts == null || parts.Count == 0)
            return (InterpreterResult.PartsAreEmptyOrNull, null);

        string source = string.Join(" ", parts);

        var tokenizationResult = ExpressionTokenizer.TokenizeExpression(
            source,
            out List<string> tokens);

        if (tokenizationResult.interpreterResult != InterpreterResult.Success)
            return tokenizationResult;

        var resolutionResult = ResolveTokens(
            tokens,
            out List<string> resolved);

        if (resolutionResult.interpreterResult != InterpreterResult.Success)
            return resolutionResult;

        if (!ExpressionConverter.TryConvertToPostfix(
            resolved,
            out List<string> postfix))
        {
            return (InterpreterResult.InvalidSyntax, source);
        }

        return ExpressionEvaluator.EvaluateExpression(postfix, out result);
    }

    public (InterpreterResult interpreterResult, string? subject)
        ParseExpression(string name, List<string>? parts, bool requirePreexisting = false)
    {
        var evaluationResult = EvaluateExpression(
            parts,
            out object? result);

        if (evaluationResult.interpreterResult != InterpreterResult.Success)
            return evaluationResult;

        if (result == null)
            return (InterpreterResult.InvalidValue, string.Join(" ", parts ?? new List<string>()));

        variables.SetVariable(name, result!, requirePreexisting);

        return (InterpreterResult.Success, null);
    }

    public (InterpreterResult interpreterResult, string? subject)
        EvaluateCondition(List<string>? parts, out bool condition)
    {
        condition = false;

        var evaluationResult = EvaluateExpression(
            parts,
            out object? result);

        if (evaluationResult.interpreterResult != InterpreterResult.Success)
            return evaluationResult;

        if (result is not bool boolResult)
            return (
                InterpreterResult.InvalidValue,
                string.Join(" ", parts!)
            );

        condition = boolResult;

        return (InterpreterResult.Success, null);
    }
}
