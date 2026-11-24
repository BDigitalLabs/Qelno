namespace Qelno.Parsing;

using Qelno.Core;

public static class ExpressionTokenizer
{
    public static (InterpreterResult interpreterResult, string? subject)
        TokenizeExpression(string source, out List<string> tokens)
    {
        tokens = new();

        int i = 0;
        int depth = 0;
        bool expectingOperand = true;

        while (i < source.Length)
        {
            char current = source[i];

            // Skip whitespace.
            if (char.IsWhiteSpace(current))
            {
                i++;
                continue;
            }

            // Opening parenthesis.
            if (current == '(')
            {
                // Implicit multiplication like 2(3 + 4) is not yet supported.
                if (!expectingOperand)
                    return (InterpreterResult.InvalidSyntax, source);

                tokens.Add("(");
                depth++;
                i++;
                expectingOperand = true;
                continue;
            }

            // Closing parenthesis.
            if (current == ')')
            {
                if (depth == 0 || expectingOperand)
                    return (InterpreterResult.InvalidSyntax, source);

                tokens.Add(")");
                depth--;
                i++;
                expectingOperand = false;
                continue;
            }

            // Recognize operators.
            if (current == '+' || current == '-' ||
                current == '*' || current == '/' ||
                current == '%')
            {
                string op;
                int length = 1;

                // Exponentiation: **
                if (current == '*' &&
                    i + 1 < source.Length &&
                    source[i + 1] == '*')
                {
                    op = "**";
                    length = 2;
                }
                else
                {
                    op = current.ToString();
                }

                if (expectingOperand)
                {
                    // Unary + and -.
                    if (current != '+' && current != '-')
                        return (InterpreterResult.InvalidSyntax, source);

                    tokens.Add(current == '+' ? "u+" : "u-");
                }
                else
                {
                    tokens.Add(op);
                    expectingOperand = true;
                }

                i += length;
                continue;
            }

            // Comparison operators.
            if (current is '>' or '<' or '=' or '!')
            {
                string op = current.ToString();
                int length = 1;

                if (i + 1 < source.Length &&
                    source[i + 1] == '=')
                {
                    op += "=";
                    length = 2;
                }
                else if (current is '=' or '!')
                {
                    return (InterpreterResult.InvalidSyntax, source);
                }

                if (expectingOperand)
                    return (InterpreterResult.InvalidSyntax, source);

                tokens.Add(op);
                i += length;
                expectingOperand = true;
                continue;
            }

            // Logical operators
            if (current is 'a' or 'o' or 'n')
            {
                string finalString = "";
                int wordStart = i;

                while (i < source.Length &&
                       (char.IsLetterOrDigit(source[i]) ||
                        source[i] == '_'))
                {
                    finalString += source[i];
                    i++;
                }

                if (finalString == LanguageNames.And ||
                    finalString == LanguageNames.Or ||
                    finalString == LanguageNames.Not)
                {
                    if (finalString == LanguageNames.And || finalString == LanguageNames.Or)
                    {
                        if (expectingOperand)
                            return (InterpreterResult.InvalidSyntax, source);

                        expectingOperand = true;
                    }
                    else
                    {
                        if (!expectingOperand)
                            return (InterpreterResult.InvalidSyntax, source);

                        expectingOperand = true;
                    }

                    tokens.Add(finalString);
                    continue;
                }
                else
                {
                    i = wordStart;
                }
            }

            // A second operand is invalid if an operand is not expected.
            if (!expectingOperand)
                return (InterpreterResult.InvalidSyntax, source);

            int tokenStart = i;

            // Number.
            if (char.IsDigit(current) || current == '.')
            {
                i = ConsumeNumber(source, i);
            }
            // String literal: "..." or '...'
            else if (current == '"' || current == '\'')
            {
                char quoteChar = current;
                i++;
                while (i < source.Length && source[i] != quoteChar)
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        i++;
                    }
                    i++;
                }
                if (i >= source.Length)
                    return (InterpreterResult.InvalidSyntax, source);

                i++; // Skip closing quote
            }
            // Variable name or Function Call
            else if (char.IsLetter(current) || current == '_')
            {
                i++;

                while (i < source.Length &&
                       (char.IsLetterOrDigit(source[i]) ||
                        source[i] == '_'))
                {
                    i++;
                }

                // Function call? (e.g.: topla(1, 2) or Abs(-5))
                string ident = source[tokenStart..i];
                int peekIndex = i;
                while (peekIndex < source.Length && char.IsWhiteSpace(source[peekIndex]))
                    peekIndex++;

                if (!LanguageNames.Contains(ident) && peekIndex < source.Length && source[peekIndex] == '(')
                {
                    int callDepth = 1;
                    int j = peekIndex + 1;
                    char inString = '\0';

                    while (j < source.Length && callDepth > 0)
                    {
                        char c = source[j];
                        if (inString != '\0')
                        {
                            if (c == '\\' && j + 1 < source.Length)
                            {
                                j++;
                            }
                            else if (c == inString) inString = '\0';
                        }
                        else if (c == '"' || c == '\'')
                        {
                            inString = c;
                        }
                        else if (c == '(')
                        {
                            callDepth++;
                        }
                        else if (c == ')')
                        {
                            callDepth--;
                        }
                        j++;
                    }

                    if (callDepth != 0)
                        return (InterpreterResult.InvalidSyntax, source);

                    tokens.Add(source[tokenStart..j]);
                    i = j;
                    expectingOperand = false;
                    continue;
                }

                // Index access? (e.g.: arr[0] or arr[i + 1])
                if (!LanguageNames.Contains(ident) && peekIndex < source.Length && source[peekIndex] == '[')
                {
                    int indexDepth = 1;
                    int j = peekIndex + 1;
                    char inString = '\0';

                    while (j < source.Length && indexDepth > 0)
                    {
                        char c = source[j];
                        if (inString != '\0')
                        {
                            if (c == '\\' && j + 1 < source.Length)
                            {
                                j++;
                            }
                            else if (c == inString) inString = '\0';
                        }
                        else if (c == '"' || c == '\'')
                        {
                            inString = c;
                        }
                        else if (c == '[')
                        {
                            indexDepth++;
                        }
                        else if (c == ']')
                        {
                            indexDepth--;
                        }
                        j++;
                    }

                    if (indexDepth != 0)
                        return (InterpreterResult.InvalidSyntax, source);

                    tokens.Add(source[tokenStart..j]);
                    i = j;
                    expectingOperand = false;
                    continue;
                }
            }
            // Array literal: [...]
            else if (current == '[')
            {
                int bracketDepth = 1;
                int j = i + 1;
                char inString = '\0';

                while (j < source.Length && bracketDepth > 0)
                {
                    char c = source[j];
                    if (inString != '\0')
                    {
                        if (c == '\\' && j + 1 < source.Length)
                        {
                            j++;
                        }
                        else if (c == inString) inString = '\0';
                    }
                    else if (c == '"' || c == '\'')
                    {
                        inString = c;
                    }
                    else if (c == '[')
                    {
                        bracketDepth++;
                    }
                    else if (c == ']')
                    {
                        bracketDepth--;
                    }
                    j++;
                }

                if (bracketDepth != 0)
                    return (InterpreterResult.InvalidSyntax, source);

                tokens.Add(source[tokenStart..j]);
                i = j;
                expectingOperand = false;
                continue;
            }
            else
            {
                return (
                    InterpreterResult.InvalidSyntax,
                    current.ToString()
                );
            }

            if (i == tokenStart)
                return (InterpreterResult.InvalidSyntax, source);

            tokens.Add(source[tokenStart..i]);
            expectingOperand = false;
        }

        // Empty expression, missing operand, or unclosed parenthesis.
        if (tokens.Count == 0)
            return (InterpreterResult.PartsAreEmptyOrNull, null);

        if (expectingOperand || depth != 0)
            return (InterpreterResult.InvalidSyntax, source);

        return (InterpreterResult.Success, null);
    }

    private static int ConsumeNumber(string source, int index)
    {
        while (index < source.Length &&
               char.IsDigit(source[index]))
        {
            index++;
        }

        // Decimal part.
        if (index < source.Length && source[index] == '.')
        {
            index++;

            while (index < source.Length &&
                   char.IsDigit(source[index]))
            {
                index++;
            }
        }

        // Scientific notation: 1e-3, 2.5E+4
        if (index < source.Length &&
            (source[index] == 'e' || source[index] == 'E'))
        {
            index++;

            if (index < source.Length &&
                (source[index] == '+' || source[index] == '-'))
            {
                index++;
            }

            while (index < source.Length &&
                   char.IsDigit(source[index]))
            {
                index++;
            }
        }

        return index;
    }
}
