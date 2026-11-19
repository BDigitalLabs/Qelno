using System;
using System.Collections.Generic;
using System.Globalization;

public static class ExpressionEvaluator
{
    private static (InterpreterResult interpreterResult, string? subject) ConvertToDouble(
        string value,
        out double number)
    {
        bool status = double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number
        );

        if (!status)
            return (InterpreterResult.InvalidValue, value);

        return (InterpreterResult.Success, null);
    }

    private static string FormatForConcat(object? val)
    {
        if (val is List<object> list) return Builtins.FormatValue(list);
        if (val is bool b) return b ? "true" : "false";
        if (val is double d) return d.ToString(CultureInfo.InvariantCulture);
        if (val is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
        return val?.ToString() ?? string.Empty;
    }

    public static (InterpreterResult interpreterResult, string? subject)
        EvaluateExpression(List<string>? parts, out object? objectResult)
    {
        objectResult = null;
        if (parts == null || parts.Count == 0)
            return (InterpreterResult.PartsAreEmptyOrNull, null);

        Stack<object> values = new();

        foreach (string token in parts)
        {
            // Unary plus.
            if (token == "u+")
            {
                if (values.Count < 1)
                    return (InterpreterResult.InvalidSyntax, token);
                if (values.Peek() is not double)
                    return (InterpreterResult.InvalidValue, token);

                continue;
            }

            // Unary minus.
            if (token == "u-")
            {
                if (values.Count < 1)
                    return (InterpreterResult.InvalidSyntax, token);

                if (values.Peek() is not double unaryNumber)
                    return (InterpreterResult.InvalidValue, token);

                values.Pop();

                values.Push(-unaryNumber);
                continue;
            }

            if (token == "not")
            {
                if (values.Count < 1)
                    return (InterpreterResult.InvalidSyntax, token);

                if (values.Peek() is not bool boolValue)
                    return (InterpreterResult.InvalidValue, token);

                values.Pop();

                values.Push(!boolValue);
                continue;
            }

            if (token == "and" || token == "or")
            {
                if (values.Count < 2)
                    return (InterpreterResult.InvalidSyntax, token);

                object rightValue = values.Pop();
                object leftValue = values.Pop();

                if (rightValue is not bool right ||
                    leftValue is not bool left)
                {
                    return (InterpreterResult.InvalidValue, token);
                }

                values.Push(token == "and" ? left && right : left || right);
                continue;
            }

            // Binary operators.
            if (token is "+" or "-" or "*" or "/" or "%" or "**")
            {
                if (values.Count < 2)
                    return (InterpreterResult.InvalidSyntax, token);

                object rightValue = values.Pop();
                object leftValue = values.Pop();

                if (token == "+")
                {
                    if (leftValue is string || rightValue is string)
                    {
                        string leftStr = FormatForConcat(leftValue);
                        string rightStr = FormatForConcat(rightValue);
                        values.Push(leftStr + rightStr);
                        continue;
                    }
                }

                if (rightValue is not double right ||
                    leftValue is not double left)
                {
                    return (InterpreterResult.InvalidValue, token);
                }

                // Division by zero in double type can produce Infinity or NaN;
                // this needs to be handled separately.
                if ((token == "/" || token == "%") && right == 0)
                    return (InterpreterResult.DivisionByZero, $"{left} {token} {right}");

                double result = token switch
                {
                    "+" => left + right,
                    "-" => left - right,
                    "*" => left * right,
                    "/" => left / right,
                    "%" => left % right,
                    "**" => Math.Pow(left, right),
                    _ => throw new InvalidOperationException()
                };

                values.Push(result);
                continue;
            }

            // Comparison operators.
            if (token is ">" or "<" or ">=" or "<=" or "==" or "!=")
            {
                if (values.Count < 2)
                    return (InterpreterResult.InvalidSyntax, token);

                object rightValue = values.Pop();
                object leftValue = values.Pop();

                if (token is "==" or "!=")
                {
                    if (leftValue is string leftStr && rightValue is string rightStr)
                    {
                        bool eq = string.Equals(leftStr, rightStr, StringComparison.Ordinal);
                        values.Push(token == "==" ? eq : !eq);
                        continue;
                    }
                    else if (leftValue is string || rightValue is string)
                    {
                        values.Push(token == "!=");
                        continue;
                    }

                    if (leftValue is bool leftBool && rightValue is bool rightBool)
                    {
                        bool eq = leftBool == rightBool;
                        values.Push(token == "==" ? eq : !eq);
                        continue;
                    }
                    else if (leftValue is bool || rightValue is bool)
                    {
                        values.Push(token == "!=");
                        continue;
                    }
                }

                if (rightValue is not double right ||
                    leftValue is not double left)
                {
                    return (InterpreterResult.InvalidValue, token);
                }

                bool result = token switch
                {
                    ">" => left > right,
                    "<" => left < right,
                    ">=" => left >= right,
                    "<=" => left <= right,
                    "==" => left == right,
                    "!=" => left != right,
                    _ => throw new InvalidOperationException()
                };

                values.Push(result);
                continue;
            }

            // String literal.
            if (token.Length >= 2 &&
                ((token[0] == '"' && token[^1] == '"') ||
                 (token[0] == '\'' && token[^1] == '\'')))
            {
                values.Push(token[1..^1]);
                continue;
            }

            if (token == "true" || token == "false")
            {
                values.Push(bool.Parse(token));
                continue;
            }

            // Remaining tokens must be numeric values.
            var conversion = ConvertToDouble(
                token,
                out double number
            );

            if (conversion.interpreterResult != InterpreterResult.Success)
                return conversion;

            values.Push(number);
        }

        // A valid postfix expression must leave a single value at the end.
        if (values.Count != 1)
            return (
                InterpreterResult.InvalidSyntax,
                string.Join(" ", parts)
            );

        objectResult = values.Pop();

        return (InterpreterResult.Success, null);
    }
}
