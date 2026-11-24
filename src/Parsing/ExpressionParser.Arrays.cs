namespace Qelno.Parsing;

using Qelno.Core;
using Qelno.Instructions;
using Qelno.Compilation;
using Qelno.Runtime;

public partial class ExpressionParser
{
    public static bool IsSingleArrayLiteral(string source)
    {
        string trimmed = source.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '[' || !trimmed.EndsWith(']'))
            return false;

        int depth = 0;
        char inString = '\0';
        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (inString != '\0')
            {
                if (c == '\\' && i + 1 < trimmed.Length)
                {
                    i++;
                }
                else if (c == inString) inString = '\0';
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
                if (c == '\\' && i + 1 < trimmed.Length)
                {
                    i++;
                }
                else if (c == inString) inString = '\0';
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
        EvaluateIndexAccess(string accessStr, out Value result)
    {
        result = Value.Null;
        string trimmed = accessStr.Trim();
        int openBracket = trimmed.IndexOf('[');
        if (openBracket <= 0 || !trimmed.EndsWith(']'))
            return (InterpreterResult.InvalidSyntax, accessStr);

        string varName = trimmed[..openBracket].Trim();
        string indexExpr = trimmed.Substring(openBracket + 1, trimmed.Length - openBracket - 2).Trim();

        if (indexExpr.Length == 0)
            return (InterpreterResult.InvalidSyntax, accessStr);

        if (!variables.TryGetVariable(varName, out Value targetVal))
            return (InterpreterResult.VariableNotFound, varName);

        var evalIndex = EvaluateExpression(new List<string> { indexExpr }, out Value indexVal);
        if (evalIndex.interpreterResult != InterpreterResult.Success)
            return evalIndex;

        if (!FunctionRegistry.TryGetDouble(indexVal, out double idxD))
            return (InterpreterResult.InvalidValue, indexExpr);

        if (double.IsNaN(idxD) || double.IsInfinity(idxD) || idxD % 1 != 0)
            return (InterpreterResult.InvalidValue, indexExpr);

        int index = (int)idxD;

        if (targetVal.IsArray && targetVal.ArrayRef is { } valList)
        {
            if (index < 0 || index >= valList.Count)
                return (InterpreterResult.InvalidValue, $"Index {index} out of range [0..{valList.Count - 1}]");

            result = valList[index];
            return (InterpreterResult.Success, null);
        }
        else if (targetVal.IsString)
        {
            string str = targetVal.AsString();
            if (index < 0 || index >= str.Length)
                return (InterpreterResult.InvalidValue, $"Index {index} out of range [0..{str.Length - 1}]");

            result = Value.FromString(str[index].ToString());
            return (InterpreterResult.Success, null);
        }

        return (InterpreterResult.InvalidValue, varName);
    }

    public (InterpreterResult interpreterResult, string? subject)
        ParseArrayLiteral(string source, out Value result)
    {
        result = Value.Null;
        string trimmed = source.Trim();
        if (!IsSingleArrayLiteral(trimmed))
            return (InterpreterResult.InvalidSyntax, source);

        string inner = trimmed[1..^1].Trim();
        List<Value> list = new();
        if (inner.Length == 0)
        {
            result = Value.FromArray(list);
            return (InterpreterResult.Success, null);
        }

        List<string> rawItems = SplitArguments(inner);
        foreach (string rawItem in rawItems)
        {
            string item = rawItem.Trim();
            if (item.Length == 0)
                return (InterpreterResult.InvalidSyntax, source);

            // String literal?
            if (InstructionHelper.IsValidStringLiteral(item))
            {
                list.Add(Value.FromString(InstructionHelper.Unescape(item[1..^1])));
                continue;
            }

            // Boolean literal?
            if (item == LanguageNames.True)
            {
                list.Add(Value.True);
                continue;
            }
            if (item == LanguageNames.False)
            {
                list.Add(Value.False);
                continue;
            }

            // Nested array literal?
            if (IsSingleArrayLiteral(item))
            {
                var nestedRes = ParseArrayLiteral(item, out Value nestedArray);
                if (nestedRes.interpreterResult != InterpreterResult.Success)
                    return nestedRes;

                list.Add(nestedArray);
                continue;
            }

            // Variable?
            if (variables.TryGetVariable(item, out Value varVal) && !item.Contains('(') && !item.Contains('['))
            {
                list.Add(varVal);
                continue;
            }

            // Evaluate as an expression
            var evalRes = EvaluateExpression(new List<string> { item }, out Value itemVal);
            if (evalRes.interpreterResult != InterpreterResult.Success)
                return evalRes;

            list.Add(itemVal);
        }

        result = Value.FromArray(list);
        return (InterpreterResult.Success, null);
    }
}
