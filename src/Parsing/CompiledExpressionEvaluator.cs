namespace Qelno.Parsing;

using Qelno.Core;
using Qelno.Runtime;
using System.Buffers;

/// <summary>
/// Executes compiled expressions at runtime directly on the <see cref="Value"/> stack
/// without string conversions.
/// </summary>
public static class CompiledExpressionEvaluator
{
    public static (InterpreterResult interpreterResult, string? subject) Evaluate(
        CompiledExpression expression,
        ExecutionContext context,
        out Value result)
    {
        if (expression.IsLogical)
            return EvaluateLogical(expression, context, out result);

        result = Value.Null;

        IReadOnlyList<RpnToken> tokens = expression.Tokens;
        if (tokens.Count == 0)
            return (InterpreterResult.PartsAreEmptyOrNull, null);

        Value[] stack = ArrayPool<Value>.Shared.Rent(tokens.Count);
        int sp = 0;

        try
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                RpnToken token = tokens[i];

                switch (token.Type)
                {
                    case RpnOpType.Literal:
                        stack[sp++] = token.LiteralValue;
                        break;

                    case RpnOpType.Variable:
                        if (!context.Variables.TryGetVariable(token.Name, out Value variableValue))
                            return (InterpreterResult.VariableNotFound, token.Name);

                        stack[sp++] = variableValue;
                        break;

                    case RpnOpType.FunctionCall:
                        {
                            var evalResult = EvaluateFunctionCall(token, expression, context, out Value functionValue);
                            if (evalResult.interpreterResult != InterpreterResult.Success)
                                return evalResult;

                            stack[sp++] = functionValue;
                            break;
                        }

                    case RpnOpType.IndexAccess:
                        {
                            var indexResult = EvaluateIndexAccess(token, context, out Value indexedValue);
                            if (indexResult.interpreterResult != InterpreterResult.Success)
                                return indexResult;

                            stack[sp++] = indexedValue;
                            break;
                        }

                    case RpnOpType.ArrayLiteral:
                        {
                            var arrayResult = EvaluateArrayLiteral(token, context, out Value arrayValue);
                            if (arrayResult.interpreterResult != InterpreterResult.Success)
                                return arrayResult;

                            stack[sp++] = arrayValue;
                            break;
                        }

                    case RpnOpType.UnaryPlus:
                        if (sp < 1)
                            return (InterpreterResult.InvalidSyntax, "u+");
                        if (!stack[sp - 1].IsNumber)
                            return (InterpreterResult.InvalidValue, "u+");
                        break;

                    case RpnOpType.Negate:
                        {
                            if (sp < 1)
                                return (InterpreterResult.InvalidSyntax, "u-");

                            Value unaryValue = stack[--sp];
                            if (!unaryValue.IsNumber)
                                return (InterpreterResult.InvalidValue, "u-");

                            stack[sp++] = Value.Negate(unaryValue);
                            break;
                        }

                    case RpnOpType.Not:
                        {
                            if (sp < 1)
                                return (InterpreterResult.InvalidSyntax, "not");

                            Value notValue = stack[--sp];
                            if (!notValue.IsBool)
                                return (InterpreterResult.InvalidValue, "not");

                            stack[sp++] = Value.FromBool(!notValue.AsBool());
                            break;
                        }

                    case RpnOpType.And:
                    case RpnOpType.Or:
                        {
                            string symbol = token.Type == RpnOpType.And ? "and" : "or";
                            if (sp < 2)
                                return (InterpreterResult.InvalidSyntax, symbol);

                            Value right = stack[--sp];
                            Value left = stack[--sp];

                            if (!left.IsBool || !right.IsBool)
                                return (InterpreterResult.InvalidValue, symbol);

                            bool logical = token.Type == RpnOpType.And
                                ? left.AsBool() && right.AsBool()
                                : left.AsBool() || right.AsBool();

                            stack[sp++] = Value.FromBool(logical);
                            break;
                        }

                    case RpnOpType.Add:
                    case RpnOpType.Subtract:
                    case RpnOpType.Multiply:
                    case RpnOpType.Divide:
                    case RpnOpType.Modulo:
                    case RpnOpType.Power:
                        {
                            string symbol = ArithmeticSymbol(token.Type);
                            if (sp < 2)
                                return (InterpreterResult.InvalidSyntax, symbol);

                            Value right = stack[--sp];
                            Value left = stack[--sp];

                            (InterpreterResult result, Value value) opResult = token.Type switch
                            {
                                RpnOpType.Add => Value.Add(left, right),
                                RpnOpType.Subtract => Value.Subtract(left, right),
                                RpnOpType.Multiply => Value.Multiply(left, right),
                                RpnOpType.Divide => Value.Divide(left, right),
                                RpnOpType.Modulo => Value.Modulo(left, right),
                                _ => Value.Power(left, right)
                            };

                            if (opResult.result == InterpreterResult.DivisionByZero)
                                return (InterpreterResult.DivisionByZero, $"{left} {symbol} {right}");

                            if (opResult.result != InterpreterResult.Success)
                                return (opResult.result, symbol);

                            stack[sp++] = opResult.value;
                            break;
                        }

                    case RpnOpType.Equal:
                    case RpnOpType.NotEqual:
                        {
                            string symbol = token.Type == RpnOpType.Equal ? "==" : "!=";
                            if (sp < 2)
                                return (InterpreterResult.InvalidSyntax, symbol);

                            Value right = stack[--sp];
                            Value left = stack[--sp];

                            bool equal = left.Equals(right);
                            stack[sp++] = Value.FromBool(token.Type == RpnOpType.Equal ? equal : !equal);
                            break;
                        }

                    case RpnOpType.LessThan:
                    case RpnOpType.LessOrEqual:
                    case RpnOpType.GreaterThan:
                    case RpnOpType.GreaterOrEqual:
                        {
                            string symbol = token.Type switch
                            {
                                RpnOpType.LessThan => "<",
                                RpnOpType.LessOrEqual => "<=",
                                RpnOpType.GreaterThan => ">",
                                _ => ">="
                            };

                            if (sp < 2)
                                return (InterpreterResult.InvalidSyntax, symbol);

                            Value right = stack[--sp];
                            Value left = stack[--sp];

                            if ((!left.IsNumber || !right.IsNumber) && (!left.IsString || !right.IsString))
                                return (InterpreterResult.InvalidValue, symbol);

                            bool comparison = token.Type switch
                            {
                                RpnOpType.LessThan => left < right,
                                RpnOpType.LessOrEqual => left <= right,
                                RpnOpType.GreaterThan => left > right,
                                _ => left >= right
                            };

                            stack[sp++] = Value.FromBool(comparison);
                            break;
                        }

                    default:
                        return (InterpreterResult.InvalidSyntax, expression.DisplayText);
                }
            }

            if (sp != 1)
                return (InterpreterResult.InvalidSyntax, expression.DisplayText);

            result = stack[0];
            return (InterpreterResult.Success, null);
        }
        finally
        {
            Array.Clear(stack, 0, sp);
            ArrayPool<Value>.Shared.Return(stack);
        }
    }

    private static (InterpreterResult interpreterResult, string? subject) EvaluateLogical(
        CompiledExpression expression,
        ExecutionContext context,
        out Value result)
    {
        result = Value.Null;

        IReadOnlyList<CompiledExpression> operands = expression.LogicalOperands!;

        if (expression.LogicalOp == LogicalOpType.Not)
        {
            var notResult = Evaluate(operands[0], context, out Value notValue);
            if (notResult.interpreterResult != InterpreterResult.Success)
                return notResult;

            if (!notValue.IsBool)
                return (InterpreterResult.InvalidValue, "not");

            result = Value.FromBool(!notValue.AsBool());
            return (InterpreterResult.Success, null);
        }

        string symbol = expression.LogicalOp == LogicalOpType.And ? "and" : "or";

        for (int i = 0; i < operands.Count; i++)
        {
            var operandResult = Evaluate(operands[i], context, out Value operandValue);
            if (operandResult.interpreterResult != InterpreterResult.Success)
                return operandResult;

            if (!operandValue.IsBool)
                return (InterpreterResult.InvalidValue, symbol);

            bool value = operandValue.AsBool();

            if (expression.LogicalOp == LogicalOpType.Or && value)
            {
                result = Value.True;
                return (InterpreterResult.Success, null);
            }

            if (expression.LogicalOp == LogicalOpType.And && !value)
            {
                result = Value.False;
                return (InterpreterResult.Success, null);
            }

            if (i == operands.Count - 1)
            {
                result = Value.FromBool(value);
                return (InterpreterResult.Success, null);
            }
        }

        return (InterpreterResult.InvalidSyntax, symbol);
    }

    private static (InterpreterResult interpreterResult, string? subject) EvaluateFunctionCall(
        RpnToken token,
        CompiledExpression owner,
        ExecutionContext context,
        out Value result)
    {
        result = Value.Null;

        List<Value> arguments = new(token.ArgumentCount);

        if (token.Arguments != null)
        {
            foreach (CompiledExpression argumentExpression in token.Arguments)
            {
                var argumentResult = Evaluate(argumentExpression, context, out Value argumentValue);
                if (argumentResult.interpreterResult != InterpreterResult.Success)
                    return argumentResult;

                arguments.Add(argumentValue);
            }
        }

        var invocation = context.InvokeFunction(token.Name, arguments);
        if (invocation.interpreterResult != InterpreterResult.Success)
            return (invocation.interpreterResult, invocation.subject);

        if (!invocation.returnValue.HasValue || invocation.returnValue.Value.IsNull)
        {
            bool allowNull = owner.AllowNullResult && owner.Tokens.Count == 1;
            if (!allowNull)
                return (InterpreterResult.InvalidValue, token.Text);

            result = Value.Null;
            return (InterpreterResult.Success, null);
        }

        result = invocation.returnValue.Value;
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult interpreterResult, string? subject) EvaluateIndexAccess(
        RpnToken token,
        ExecutionContext context,
        out Value result)
    {
        result = Value.Null;

        if (!context.Variables.TryGetVariable(token.Name, out Value targetValue))
            return (InterpreterResult.VariableNotFound, token.Name);

        var indexResult = Evaluate(token.Operand!, context, out Value indexValue);
        if (indexResult.interpreterResult != InterpreterResult.Success)
            return indexResult;

        if (!FunctionRegistry.TryGetDouble(indexValue, out double indexDouble))
            return (InterpreterResult.InvalidValue, token.Text);

        if (double.IsNaN(indexDouble) || double.IsInfinity(indexDouble) || indexDouble % 1 != 0)
            return (InterpreterResult.InvalidValue, token.Text);

        int index = (int)indexDouble;

        if (targetValue.IsArray && targetValue.ArrayRef is { } values)
        {
            if (index < 0 || index >= values.Count)
                return (InterpreterResult.InvalidValue, $"Index {index} out of range [0..{values.Count - 1}]");

            result = values[index];
            return (InterpreterResult.Success, null);
        }

        if (targetValue.IsString)
        {
            string text = targetValue.AsString();
            if (index < 0 || index >= text.Length)
                return (InterpreterResult.InvalidValue, $"Index {index} out of range [0..{text.Length - 1}]");

            result = Value.FromString(text[index].ToString());
            return (InterpreterResult.Success, null);
        }

        return (InterpreterResult.InvalidValue, token.Name);
    }

    private static (InterpreterResult interpreterResult, string? subject) EvaluateArrayLiteral(
        RpnToken token,
        ExecutionContext context,
        out Value result)
    {
        result = Value.Null;

        List<Value> elements = new(token.ArgumentCount);

        if (token.Arguments != null)
        {
            foreach (CompiledExpression elementExpression in token.Arguments)
            {
                var elementResult = Evaluate(elementExpression, context, out Value elementValue);
                if (elementResult.interpreterResult != InterpreterResult.Success)
                    return elementResult;

                elements.Add(elementValue);
            }
        }

        result = Value.FromArray(elements);
        return (InterpreterResult.Success, null);
    }

    private static string ArithmeticSymbol(RpnOpType type)
    {
        return type switch
        {
            RpnOpType.Add => "+",
            RpnOpType.Subtract => "-",
            RpnOpType.Multiply => "*",
            RpnOpType.Divide => "/",
            RpnOpType.Modulo => "%",
            _ => "**"
        };
    }
}
