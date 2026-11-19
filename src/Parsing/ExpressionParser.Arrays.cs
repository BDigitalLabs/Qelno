using System;
using System.Collections.Generic;

public partial class ExpressionParser
{
    public static bool IsSingleIndexAccess(string source)
    {
        string trimmed = source.Trim();
        int openBracket = trimmed.IndexOf('[');
        if (openBracket <= 0 || !trimmed.EndsWith(']'))
            return false;

        string varName = trimmed[..openBracket].Trim();
        if (!SyntaxValidator.IsValidIdentifier(varName) || LanguageNames.Contains(varName))
            return false;

        int depth = 0;
        char inString = '\0';
        for (int i = openBracket; i < trimmed.Length; i++)
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
            else if (c == '[')
            {
                depth++;
            }
            else if (c == ']')
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

    public (InterpreterResult interpreterResult, string? subject)
        EvaluateIndexAccess(string accessStr, out object? result)
    {
        result = null;
        string trimmed = accessStr.Trim();
        int openBracket = trimmed.IndexOf('[');
        if (openBracket <= 0 || !trimmed.EndsWith(']'))
            return (InterpreterResult.InvalidSyntax, accessStr);

        string varName = trimmed[..openBracket].Trim();
        string indexExpr = trimmed.Substring(openBracket + 1, trimmed.Length - openBracket - 2).Trim();

        if (indexExpr.Length == 0)
            return (InterpreterResult.InvalidSyntax, accessStr);

        if (!variables.TryGetVariable(varName, out object? targetObj) || targetObj == null)
            return (InterpreterResult.VariableNotFound, varName);

        var evalIndex = EvaluateExpression(new List<string> { indexExpr }, out object? indexVal);
        if (evalIndex.interpreterResult != InterpreterResult.Success)
            return evalIndex;

        if (!FunctionRegistry.TryGetDouble(indexVal, out double idxD))
            return (InterpreterResult.InvalidValue, indexExpr);

        int index = (int)idxD;

        if (targetObj is List<object> list)
        {
            if (index < 0 || index >= list.Count)
                return (InterpreterResult.InvalidValue, $"Index {index} out of range [0..{list.Count - 1}]");

            result = list[index];
            return (InterpreterResult.Success, null);
        }
        else if (targetObj is string str)
        {
            if (index < 0 || index >= str.Length)
                return (InterpreterResult.InvalidValue, $"Index {index} out of range [0..{str.Length - 1}]");

            result = str[index].ToString();
            return (InterpreterResult.Success, null);
        }

        return (InterpreterResult.InvalidValue, varName);
    }

    public (InterpreterResult interpreterResult, string? subject)
        ParseArrayLiteral(string source, out object? result)
    {
        result = null;
        string trimmed = source.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
            return (InterpreterResult.InvalidSyntax, source);

        string inner = trimmed[1..^1].Trim();
        List<object> list = new();
        if (inner.Length == 0)
        {
            result = list;
            return (InterpreterResult.Success, null);
        }

        List<string> rawItems = SplitArguments(inner);
        foreach (string rawItem in rawItems)
        {
            string item = rawItem.Trim();
            if (item.Length == 0)
                return (InterpreterResult.InvalidSyntax, source);

            // String literal?
            if (item.Length >= 2 &&
                ((item[0] == '"' && item[^1] == '"' && !item[1..^1].Contains('"')) ||
                 (item[0] == '\'' && item[^1] == '\'' && !item[1..^1].Contains('\''))))
            {
                list.Add(item[1..^1]);
                continue;
            }

            // Boolean literal?
            if (item == LanguageNames.True)
            {
                list.Add(true);
                continue;
            }
            if (item == LanguageNames.False)
            {
                list.Add(false);
                continue;
            }

            // Nested array literal?
            if (item.StartsWith('[') && item.EndsWith(']'))
            {
                var nestedRes = ParseArrayLiteral(item, out object? nestedArray);
                if (nestedRes.interpreterResult != InterpreterResult.Success)
                    return nestedRes;

                list.Add(nestedArray!);
                continue;
            }

            // Variable?
            if (variables.TryGetVariable(item, out object? varVal) && !item.Contains('(') && !item.Contains('['))
            {
                list.Add(varVal!);
                continue;
            }

            // Evaluate as an expression
            var evalRes = EvaluateExpression(new List<string> { item }, out object? itemVal);
            if (evalRes.interpreterResult != InterpreterResult.Success)
                return evalRes;

            list.Add(itemVal!);
        }

        result = list;
        return (InterpreterResult.Success, null);
    }
}