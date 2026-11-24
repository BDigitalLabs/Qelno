namespace Qelno.Instructions;

using Qelno.Core;
using Qelno.Parsing;
using System.Buffers;

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

        if (text[^1] != quoteChar)
            return false;

        for (int i = 1; i < text.Length - 1; i++)
        {
            if (text[i] == '\\')
            {
                i++; // Skip escaped character
            }
            else if (text[i] == quoteChar)
            {
                return false;
            }
        }

        return true;
    }

    public static string Unescape(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('\\'))
            return text;

        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++;
                switch (text[i])
                {
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case '"':
                        sb.Append('"');
                        break;
                    case '\'':
                        sb.Append('\'');
                        break;
                    case '\\':
                        sb.Append('\\');
                        break;
                    case '0':
                        sb.Append('\0');
                        break;
                    default:
                        sb.Append('\\');
                        sb.Append(text[i]);
                        break;
                }
            }
            else
            {
                sb.Append(text[i]);
            }
        }
        return sb.ToString();
    }

    public static string StripComment(string line)
    {
        if (string.IsNullOrEmpty(line) || !line.Contains('#'))
            return line;

        char inQuote = '\0';
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuote != '\0')
            {
                if (c == '\\' && i + 1 < line.Length)
                {
                    i++; // Skip escaped character
                }
                else if (c == inQuote)
                {
                    inQuote = '\0';
                }
            }
            else if (c == '"' || c == '\'')
            {
                inQuote = c;
            }
            else if (c == '#')
            {
                return line[..i];
            }
        }

        return line;
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

    public static List<string> SplitLinePreservingQuotes(string line)
    {
        List<string> parts = new();
        int i = 0;
        int len = line.Length;

        while (i < len)
        {
            while (i < len && char.IsWhiteSpace(line[i]))
                i++;

            if (i >= len)
                break;

            int start = i;
            char inQuote = '\0';
            int parenDepth = 0;
            int bracketDepth = 0;

            while (i < len)
            {
                char c = line[i];
                if (inQuote != '\0')
                {
                    if (c == '\\' && i + 1 < len)
                    {
                        i++; // Skip escaped character
                    }
                    else if (c == inQuote)
                    {
                        inQuote = '\0';
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    inQuote = c;
                }
                else if (c == '(')
                {
                    parenDepth++;
                }
                else if (c == ')')
                {
                    if (parenDepth > 0) parenDepth--;
                }
                else if (c == '[')
                {
                    bracketDepth++;
                }
                else if (c == ']')
                {
                    if (bracketDepth > 0) bracketDepth--;
                }
                else if (char.IsWhiteSpace(c) && parenDepth == 0 && bracketDepth == 0)
                {
                    break;
                }
                i++;
            }

            parts.Add(line[start..i]);
        }

        return parts;
    }

    public static bool TryParseLiteralNumber(string text, out double value)
    {
        value = 0.0;
        string compact = text.Replace(" ", "");
        if (string.IsNullOrEmpty(compact))
            return false;

        for (int i = 0; i < compact.Length; i++)
        {
            char c = compact[i];
            if (c == '+' || c == '-')
            {
                if (i != 0 && compact[i - 1] != 'e' && compact[i - 1] != 'E')
                    return false;
            }
            else if (!char.IsDigit(c) && c != '.' && c != 'e' && c != 'E')
            {
                return false;
            }
        }

        if (double.TryParse(compact, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
        {
            if (!double.IsNaN(parsed) && !double.IsInfinity(parsed))
            {
                value = parsed;
                return true;
            }
        }

        return false;
    }
}
