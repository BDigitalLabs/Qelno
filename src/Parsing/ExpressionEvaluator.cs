namespace Qelno.Parsing;

using Qelno.Core;
using Qelno.Instructions;
using System.Globalization;

public static class ExpressionEvaluator
{
    public static (InterpreterResult interpreterResult, string? subject)
        EvaluateExpression(
            List<string>? parts,
            out Value valueResult,
            Func<string, (InterpreterResult, Value)>? arrayEvaluator = null)
    {
        valueResult = Value.Null;
        if (parts == null || parts.Count == 0)
            return (InterpreterResult.PartsAreEmptyOrNull, null);

        Stack<Value> values = new();

        foreach (string token in parts)
        {
            // Unary plus.
            if (token == "u+")
            {
                if (values.Count < 1)
                    return (InterpreterResult.InvalidSyntax, token);
                if (!values.Peek().IsNumber)
                    return (InterpreterResult.InvalidValue, token);

                continue;
            }

            // Unary minus.
            if (token == "u-")
            {
                if (values.Count < 1)
                    return (InterpreterResult.InvalidSyntax, token);

                Value unaryVal = values.Pop();
                if (!unaryVal.IsNumber)
                    return (InterpreterResult.InvalidValue, token);

                values.Push(Value.Negate(unaryVal));
                continue;
            }

            if (token == "not")
            {
                if (values.Count < 1)
                    return (InterpreterResult.InvalidSyntax, token);

                Value val = values.Pop();
                if (!val.IsBool)
                    return (InterpreterResult.InvalidValue, token);

                values.Push(Value.FromBool(!val.AsBool()));
                continue;
            }

            if (token == "and" || token == "or")
            {
                if (values.Count < 2)
                    return (InterpreterResult.InvalidSyntax, token);

                Value right = values.Pop();
                Value left = values.Pop();

                if (!left.IsBool || !right.IsBool)
                {
                    return (InterpreterResult.InvalidValue, token);
                }

                values.Push(Value.FromBool(token == "and" ? left.AsBool() && right.AsBool() : left.AsBool() || right.AsBool()));
                continue;
            }

            // Binary arithmetic operators.
            if (token is "+" or "-" or "*" or "/" or "%" or "**")
            {
                if (values.Count < 2)
                    return (InterpreterResult.InvalidSyntax, token);

                Value right = values.Pop();
                Value left = values.Pop();

                (InterpreterResult result, Value value) opRes = token switch
                {
                    "+" => Value.Add(left, right),
                    "-" => Value.Subtract(left, right),
                    "*" => Value.Multiply(left, right),
                    "/" => Value.Divide(left, right),
                    "%" => Value.Modulo(left, right),
                    "**" => Value.Power(left, right),
                    _ => throw new InvalidOperationException()
                };

                if (opRes.result == InterpreterResult.DivisionByZero)
                    return (InterpreterResult.DivisionByZero, $"{left} {token} {right}");

                if (opRes.result != InterpreterResult.Success)
                    return (opRes.result, token);

                values.Push(opRes.value);
                continue;
            }

            // Comparison operators.
            if (token is ">" or "<" or ">=" or "<=" or "==" or "!=")
            {
                if (values.Count < 2)
                    return (InterpreterResult.InvalidSyntax, token);

                Value right = values.Pop();
                Value left = values.Pop();

                if (token is "==" or "!=")
                {
                    bool eq = left.Equals(right);
                    values.Push(Value.FromBool(token == "==" ? eq : !eq));
                    continue;
                }

                if ((!left.IsNumber || !right.IsNumber) && (!left.IsString || !right.IsString))
                {
                    return (InterpreterResult.InvalidValue, token);
                }

                bool result = token switch
                {
                    ">" => left > right,
                    "<" => left < right,
                    ">=" => left >= right,
                    "<=" => left <= right,
                    _ => throw new InvalidOperationException()
                };

                values.Push(Value.FromBool(result));
                continue;
            }

            // String literal.
            if (InstructionHelper.IsValidStringLiteral(token))
            {
                values.Push(Value.FromString(InstructionHelper.Unescape(token[1..^1])));
                continue;
            }

            // Array literal.
            if (ExpressionParser.IsSingleArrayLiteral(token))
            {
                if (arrayEvaluator != null)
                {
                    var (arrRes, arrVal) = arrayEvaluator(token);
                    if (arrRes != InterpreterResult.Success)
                        return (arrRes, token);
                    values.Push(arrVal);
                    continue;
                }
                else
                {
                    var (arrRes, arrVal) = ParseSimpleArrayLiteral(token);
                    if (arrRes != InterpreterResult.Success)
                        return (arrRes, token);
                    values.Push(arrVal);
                    continue;
                }
            }

            if (token == "true")
            {
                values.Push(Value.True);
                continue;
            }

            if (token == "false")
            {
                values.Push(Value.False);
                continue;
            }

            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intNumber))
            {
                values.Push(Value.FromInteger(intNumber));
                continue;
            }

            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatNumber))
            {
                values.Push(Value.FromFloat(floatNumber));
                continue;
            }

            return (InterpreterResult.InvalidValue, token);
        }

        // A valid postfix expression must leave a single value at the end.
        if (values.Count != 1)
            return (
                InterpreterResult.InvalidSyntax,
                string.Join(" ", parts)
            );

        valueResult = values.Pop();

        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult, Value) ParseSimpleArrayLiteral(string source)
    {
        string inner = source.Trim()[1..^1].Trim();
        if (inner.Length == 0)
            return (InterpreterResult.Success, Value.FromArray(new List<Value>()));

        List<string> rawItems = ExpressionParser.SplitArguments(inner);
        List<Value> list = new(rawItems.Count);
        foreach (string rawItem in rawItems)
        {
            string item = rawItem.Trim();
            if (InstructionHelper.IsValidStringLiteral(item))
                list.Add(Value.FromString(InstructionHelper.Unescape(item[1..^1])));
            else if (item == "true")
                list.Add(Value.True);
            else if (item == "false")
                list.Add(Value.False);
            else if (long.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                list.Add(Value.FromInteger(l));
            else if (double.TryParse(item, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                list.Add(Value.FromFloat(d));
            else
                return (InterpreterResult.InvalidValue, Value.Null);
        }
        return (InterpreterResult.Success, Value.FromArray(list));
    }
}
