namespace Qelno.Instructions;

using Qelno.Core;
using Qelno.Parsing;
using Qelno.Runtime;
public class SetInstruction : Instruction
{
    public string VariableName { get; }
    public List<string> ValueParts { get; }
    public string ValueText { get; }
    public bool RequirePreexisting { get; }
    public CompiledExpression? CompiledValue { get; }

    private readonly bool isArrayLiteral;
    private readonly bool isStringLiteral;
    private readonly bool hasExpressionSymbols;
    private readonly bool hasInvalidStringSyntax;

    public SetInstruction(
        string variableName,
        List<string> valueParts,
        string valueText,
        int lineNumber,
        bool requirePreexisting = false,
        CompiledExpression? compiledValue = null)
        : base(lineNumber)
    {
        VariableName = variableName;
        ValueParts = valueParts;
        ValueText = valueText;
        RequirePreexisting = requirePreexisting;
        CompiledValue = compiledValue;

        // These checks are computed once at compile time
        // so they don't have to be repeated on every run.
        hasExpressionSymbols = InstructionHelper.HasExpressionSymbols(ValueText, ValueParts);
        isStringLiteral = InstructionHelper.IsValidStringLiteral(ValueText);
        isArrayLiteral = ExpressionParser.IsSingleArrayLiteral(ValueText.Trim());
        hasInvalidStringSyntax = InstructionHelper.HasInvalidStringSyntax(ValueText, hasExpressionSymbols);
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        if (RequirePreexisting && !context.Variables.ContainsVariable(VariableName))
            return (InterpreterResult.VariableNotFound, VariableName);

        if (!context.Variables.IsNameAvailable(VariableName))
            return (InterpreterResult.InvalidName, VariableName);

        // 0. Array literal check: [ ... ]
        if (isArrayLiteral)
        {
            Value arrayVal;

            if (CompiledValue != null)
            {
                var compiledRes = CompiledExpressionEvaluator.Evaluate(CompiledValue, context, out arrayVal);
                if (compiledRes.interpreterResult != InterpreterResult.Success)
                    return compiledRes;
            }
            else
            {
                var parseArrayRes = context.ExpressionParser.ParseArrayLiteral(ValueText.Trim(), out arrayVal);
                if (parseArrayRes.interpreterResult != InterpreterResult.Success)
                    return parseArrayRes;
            }

            context.Variables.SetVariable(VariableName, arrayVal, RequirePreexisting);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        // 1. String literal check
        if (isStringLiteral)
        {
            context.Variables.SetVariableFromText(VariableName, ValueText, RequirePreexisting);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        if (hasInvalidStringSyntax)
        {
            return (InterpreterResult.InvalidSyntax, ValueText);
        }

        // 3. Expression
        if (ValueParts.Count > 1 ||
            hasExpressionSymbols)
        {
            if (CompiledValue != null)
            {
                var compiledRes = CompiledExpressionEvaluator.Evaluate(CompiledValue, context, out Value result);
                if (compiledRes.interpreterResult != InterpreterResult.Success)
                    return compiledRes;

                if (result.IsNull)
                    return (InterpreterResult.InvalidValue, string.Join(" ", ValueParts));

                context.Variables.SetVariable(VariableName, result, RequirePreexisting);
                context.Ip++;
                return (InterpreterResult.Success, null);
            }

            var res = context.ExpressionParser.ParseExpression(VariableName, ValueParts, RequirePreexisting);
            if (res.interpreterResult != InterpreterResult.Success)
                return res;

            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        // 4. Variable value copy
        if (context.Variables.TryGetVariable(ValueText, out Value existingVarValue))
        {
            context.Variables.SetVariable(VariableName, existingVarValue, RequirePreexisting);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        // 5. Plain value or literal
        context.Variables.SetVariableFromText(VariableName, ValueText, RequirePreexisting);
        context.Ip++;
        return (InterpreterResult.Success, null);
    }
}

public class SetIndexInstruction : Instruction
{
    public string ArrayName { get; }
    public string IndexText { get; }
    public List<string> ValueParts { get; }
    public CompiledExpression? CompiledIndex { get; }
    public CompiledExpression? CompiledValue { get; }

    public SetIndexInstruction(
        string arrayName,
        string indexText,
        List<string> valueParts,
        int lineNumber,
        CompiledExpression? compiledIndex = null,
        CompiledExpression? compiledValue = null)
        : base(lineNumber)
    {
        ArrayName = arrayName;
        IndexText = indexText;
        ValueParts = valueParts;
        CompiledIndex = compiledIndex;
        CompiledValue = compiledValue;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        if (!context.Variables.TryGetVariable(ArrayName, out Value arrVal))
            return (InterpreterResult.VariableNotFound, ArrayName);

        Value indexVal;

        if (CompiledIndex != null)
        {
            var idxResult = CompiledExpressionEvaluator.Evaluate(CompiledIndex, context, out indexVal);
            if (idxResult.interpreterResult != InterpreterResult.Success)
                return idxResult;
        }
        else
        {
            var idxRes = context.ExpressionParser.EvaluateExpression(new List<string> { IndexText }, out indexVal);
            if (idxRes.interpreterResult != InterpreterResult.Success)
                return idxRes;
        }

        if (!FunctionRegistry.TryGetDouble(indexVal, out double idxD))
            return (InterpreterResult.InvalidValue, IndexText);

        if (double.IsNaN(idxD) || double.IsInfinity(idxD) || idxD % 1 != 0)
            return (InterpreterResult.InvalidValue, IndexText);

        int index = (int)idxD;

        Value evaluatedValue;

        if (CompiledValue != null)
        {
            var valResult = CompiledExpressionEvaluator.Evaluate(CompiledValue, context, out evaluatedValue);
            if (valResult.interpreterResult != InterpreterResult.Success)
                return valResult;
        }
        else
        {
            var valRes = context.ExpressionParser.EvaluateExpression(ValueParts, out evaluatedValue);
            if (valRes.interpreterResult != InterpreterResult.Success)
                return valRes;
        }

        if (arrVal.IsArray && arrVal.ArrayRef is { } listV)
        {
            if (index < 0 || index >= listV.Count)
                return (InterpreterResult.InvalidValue, $"Index {index} out of range [0..{listV.Count - 1}]");

            listV[index] = evaluatedValue;
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        return (InterpreterResult.InvalidValue, ArrayName);
    }
}
