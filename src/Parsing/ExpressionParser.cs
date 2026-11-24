namespace Qelno.Parsing;

using Qelno.Core;
using Qelno.Instructions;
using Qelno.Runtime;
using System.Globalization;

public partial class ExpressionParser
{
    private readonly Variables variables;
    private ExecutionContext? context;

    public ExpressionParser(
        Variables variables,
        ExecutionContext? context = null)
    {
        this.variables = variables;
        this.context = context;
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

            // Array literal?
            if (IsSingleArrayLiteral(token))
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
                var funcEvalResult = EvaluateFunctionCall(token, out Value? funcResult);
                if (funcEvalResult.interpreterResult != InterpreterResult.Success)
                    return funcEvalResult;

                if (!funcResult.HasValue || funcResult.Value.IsNull)
                {
                    return (InterpreterResult.InvalidValue, token);
                }

                Value v = funcResult.Value;
                if (v.IsBool)
                {
                    resolved.Add(v.AsBool() ? "true" : "false");
                }
                else if (v.IsString)
                {
                    resolved.Add($"\"{v.AsString()}\"");
                }
                else if (v.IsInteger)
                {
                    resolved.Add(v.AsInteger().ToString(CultureInfo.InvariantCulture));
                }
                else if (v.IsFloat)
                {
                    resolved.Add(v.AsFloat().ToString("R", CultureInfo.InvariantCulture));
                }
                else
                {
                    resolved.Add(v.ToString());
                }
                continue;
            }

            // Index access? (e.g.: arr[0], arr[i + 1])
            if (IsSingleIndexAccess(token))
            {
                var indexEvalResult = EvaluateIndexAccess(token, out Value indexResult);
                if (indexEvalResult.interpreterResult != InterpreterResult.Success)
                    return indexEvalResult;

                if (indexResult.IsBool) resolved.Add(indexResult.AsBool() ? "true" : "false");
                else if (indexResult.IsString) resolved.Add($"\"{indexResult.AsString()}\"");
                else if (indexResult.IsInteger) resolved.Add(indexResult.AsInteger().ToString(CultureInfo.InvariantCulture));
                else if (indexResult.IsFloat) resolved.Add(indexResult.AsFloat().ToString("R", CultureInfo.InvariantCulture));
                else resolved.Add(indexResult.ToString());
                continue;
            }

            if (variables.TryGetVariable(token, out Value val))
            {
                if (val.IsBool) { resolved.Add(val.AsBool() ? "true" : "false"); continue; }
                if (val.IsString) { resolved.Add($"\"{val.AsString()}\""); continue; }
                if (val.IsInteger) { resolved.Add(val.AsInteger().ToString(CultureInfo.InvariantCulture)); continue; }
                if (val.IsFloat) { resolved.Add(val.AsFloat().ToString("R", CultureInfo.InvariantCulture)); continue; }
                resolved.Add(val.ToString());
                continue;
            }

            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                resolved.Add(token);
                continue;
            }

            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                resolved.Add(number.ToString("R", CultureInfo.InvariantCulture));
                continue;
            }

            return (InterpreterResult.VariableNotFound, token);
        }

        return (InterpreterResult.Success, null);
    }

    public (InterpreterResult interpreterResult, string? subject)
        EvaluateExpression(List<string>? parts, out Value result)
    {
        result = Value.Null;
        if (parts == null || parts.Count == 0)
            return (InterpreterResult.PartsAreEmptyOrNull, null);

        // Compiled expressions are executed directly on the Value stack.
        if (context != null &&
            ExpressionCompiler.TryCompile(parts, out CompiledExpression compiled))
        {
            return CompiledExpressionEvaluator.Evaluate(compiled, context, out result);
        }

        return EvaluateExpressionLegacy(parts, out result);
    }

    private (InterpreterResult interpreterResult, string? subject)
        EvaluateExpressionLegacy(List<string> parts, out Value result)
    {
        result = Value.Null;

        string source = string.Join(" ", parts).Trim();

        // Array literal?
        if (IsSingleArrayLiteral(source))
        {
            return ParseArrayLiteral(source, out result);
        }

        // Single quoted string literal?
        if (InstructionHelper.IsValidStringLiteral(source))
        {
            result = Value.FromString(InstructionHelper.Unescape(source[1..^1]));
            return (InterpreterResult.Success, null);
        }

        // Single index access? (e.g.: arr[0])
        if (IsSingleIndexAccess(source))
        {
            return EvaluateIndexAccess(source, out result);
        }

        // Single variable?
        if (parts.Count == 1 && variables.TryGetVariable(source, out Value varVal))
        {
            result = varVal;
            return (InterpreterResult.Success, null);
        }

        // Single function call?
        if (IsSingleFunctionCall(source))
        {
            var fRes = EvaluateFunctionCall(source, out Value? fVal);
            result = fVal ?? Value.Null;
            return fRes;
        }

        return EvaluateExpressionTokens(parts, out result);
    }

    private (InterpreterResult interpreterResult, string? subject)
    EvaluateExpressionTokens(
        List<string>? parts,
        out Value result)
    {
        result = Value.Null;

        if (parts == null || parts.Count == 0)
            return (InterpreterResult.PartsAreEmptyOrNull, null);

        string source = string.Join(" ", parts);

        var tokenizationResult = ExpressionTokenizer.TokenizeExpression(
            source,
            out List<string> tokens);

        if (tokenizationResult.interpreterResult != InterpreterResult.Success)
            return tokenizationResult;

        return EvaluateTokensWithShortCircuit(tokens, out result);
    }

    private (InterpreterResult interpreterResult, string? subject)
        EvaluateTokensWithShortCircuit(List<string> tokens, out Value result)
    {
        result = Value.Null;
        if (tokens.Count == 0)
            return (InterpreterResult.PartsAreEmptyOrNull, null);

        // 1. Check for "or" at depth 0
        int depth = 0;
        List<int> orIndices = new();
        for (int i = 0; i < tokens.Count; i++)
        {
            string t = tokens[i];
            if (t == "(") depth++;
            else if (t == ")") depth--;
            else if (depth == 0 && t == "or")
                orIndices.Add(i);
        }

        if (orIndices.Count > 0)
        {
            int start = 0;
            foreach (int orIdx in orIndices)
            {
                if (orIdx == start)
                    return (InterpreterResult.InvalidSyntax, "or");

                var slice = tokens.GetRange(start, orIdx - start);
                var evalRes = EvaluateTokensWithShortCircuit(slice, out Value sliceVal);
                if (evalRes.interpreterResult != InterpreterResult.Success)
                    return evalRes;

                if (!sliceVal.IsBool)
                    return (InterpreterResult.InvalidValue, "or");

                if (sliceVal.AsBool())
                {
                    result = Value.True;
                    return (InterpreterResult.Success, null);
                }

                start = orIdx + 1;
            }

            if (start >= tokens.Count)
                return (InterpreterResult.InvalidSyntax, "or");

            var lastSlice = tokens.GetRange(start, tokens.Count - start);
            var lastRes = EvaluateTokensWithShortCircuit(lastSlice, out Value lastVal);
            if (lastRes.interpreterResult != InterpreterResult.Success)
                return lastRes;

            if (!lastVal.IsBool)
                return (InterpreterResult.InvalidValue, "or");

            result = Value.FromBool(lastVal.AsBool());
            return (InterpreterResult.Success, null);
        }

        // 2. Check for "and" at depth 0
        depth = 0;
        List<int> andIndices = new();
        for (int i = 0; i < tokens.Count; i++)
        {
            string t = tokens[i];
            if (t == "(") depth++;
            else if (t == ")") depth--;
            else if (depth == 0 && t == "and")
                andIndices.Add(i);
        }

        if (andIndices.Count > 0)
        {
            int start = 0;
            foreach (int andIdx in andIndices)
            {
                if (andIdx == start)
                    return (InterpreterResult.InvalidSyntax, "and");

                var slice = tokens.GetRange(start, andIdx - start);
                var evalRes = EvaluateTokensWithShortCircuit(slice, out Value sliceVal);
                if (evalRes.interpreterResult != InterpreterResult.Success)
                    return evalRes;

                if (!sliceVal.IsBool)
                    return (InterpreterResult.InvalidValue, "and");

                if (!sliceVal.AsBool())
                {
                    result = Value.False;
                    return (InterpreterResult.Success, null);
                }

                start = andIdx + 1;
            }

            if (start >= tokens.Count)
                return (InterpreterResult.InvalidSyntax, "and");

            var lastSlice = tokens.GetRange(start, tokens.Count - start);
            var lastRes = EvaluateTokensWithShortCircuit(lastSlice, out Value lastVal);
            if (lastRes.interpreterResult != InterpreterResult.Success)
                return lastRes;

            if (!lastVal.IsBool)
                return (InterpreterResult.InvalidValue, "and");

            result = Value.FromBool(lastVal.AsBool());
            return (InterpreterResult.Success, null);
        }

        // 3. Strip redundant outer parentheses if matching
        if (tokens.Count >= 2 && tokens[0] == "(" && tokens[^1] == ")")
        {
            int parenDepth = 0;
            bool wrapsAll = true;
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                if (tokens[i] == "(") parenDepth++;
                else if (tokens[i] == ")") parenDepth--;
                if (parenDepth == 0)
                {
                    wrapsAll = false;
                    break;
                }
            }

            if (wrapsAll)
            {
                return EvaluateTokensWithShortCircuit(tokens.GetRange(1, tokens.Count - 2), out result);
            }
        }

        // 4. Check for leading "not" at depth 0
        if (tokens.Count > 1 && tokens[0] == "not")
        {
            var notSlice = tokens.GetRange(1, tokens.Count - 1);
            var evalRes = EvaluateTokensWithShortCircuit(notSlice, out Value notVal);
            if (evalRes.interpreterResult != InterpreterResult.Success)
                return evalRes;

            if (!notVal.IsBool)
                return (InterpreterResult.InvalidValue, "not");

            result = Value.FromBool(!notVal.AsBool());
            return (InterpreterResult.Success, null);
        }

        // 5. Base case: evaluate tokens using postfix evaluator
        var resolutionResult = ResolveTokens(
            tokens,
            out List<string> resolved);

        if (resolutionResult.interpreterResult != InterpreterResult.Success)
            return resolutionResult;

        if (!ExpressionConverter.TryConvertToPostfix(
            resolved,
            out List<string> postfix))
        {
            return (InterpreterResult.InvalidSyntax, string.Join(" ", tokens));
        }

        return ExpressionEvaluator.EvaluateExpression(postfix, out result, str =>
        {
            var res = ParseArrayLiteral(str, out Value arrVal);
            return (res.interpreterResult, arrVal);
        });
    }

    public (InterpreterResult interpreterResult, string? subject)
        ParseExpression(string name, List<string>? parts, bool requirePreexisting = false)
    {
        var evaluationResult = EvaluateExpression(
            parts,
            out Value result);

        if (evaluationResult.interpreterResult != InterpreterResult.Success)
            return evaluationResult;

        if (result.IsNull)
            return (InterpreterResult.InvalidValue, string.Join(" ", parts ?? new List<string>()));

        variables.SetVariable(name, result, requirePreexisting);

        return (InterpreterResult.Success, null);
    }

    public (InterpreterResult interpreterResult, string? subject)
        EvaluateCondition(List<string>? parts, out bool condition)
    {
        condition = false;

        var evaluationResult = EvaluateExpression(
            parts,
            out Value result);

        if (evaluationResult.interpreterResult != InterpreterResult.Success)
            return evaluationResult;

        if (!result.IsBool)
            return (
                InterpreterResult.InvalidValue,
                string.Join(" ", parts!)
            );

        condition = result.AsBool();

        return (InterpreterResult.Success, null);
    }
}
