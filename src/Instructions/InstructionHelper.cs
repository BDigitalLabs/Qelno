using System;
using System.Buffers;
using System.Collections.Generic;

public static class InstructionHelper
{
    private static readonly SearchValues<char> ExpressionSymbols =
        SearchValues.Create("+-*/%()><=![]");

    public static bool HasExpressionSymbols(string text, List<string>? parts = null)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (text.AsSpan().IndexOfAny(ExpressionSymbols) >= 0)
            return true;

        if (parts != null && (
            parts.Contains(LanguageNames.And) ||
            parts.Contains(LanguageNames.Or) ||
            parts.Contains(LanguageNames.Not)))
        {
            return true;
        }

        return false;
    }

    public static bool IsValidStringLiteral(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 2)
            return false;

        char quoteChar = text[0];
        if (quoteChar != '"' && quoteChar != '\'')
            return false;

        return text[^1] == quoteChar && !text[1..^1].Contains(quoteChar);
    }

    public static bool HasInvalidStringSyntax(string text, bool hasExpressionSymbols)
    {
        if (hasExpressionSymbols)
            return false;

        if (text.StartsWith('"') || text.StartsWith('\''))
        {
            return !IsValidStringLiteral(text);
        }

        if ((text.Contains('"') || text.Contains('\'')) && !ExpressionParser.IsSingleFunctionCall(text))
        {
            return true;
        }

        return false;
    }
}
