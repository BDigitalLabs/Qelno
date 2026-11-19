using System;
using System.Collections.Generic;

public class SetInstruction : Instruction
{
    public string VariableName { get; }
    public List<string> ValueParts { get; }
    public string ValueText { get; }
    public bool RequirePreexisting { get; }

    public SetInstruction(string variableName, List<string> valueParts, string valueText, int lineNumber, bool requirePreexisting = false)
        : base(lineNumber)
    {
        VariableName = variableName;
        ValueParts = valueParts;
        ValueText = valueText;
        RequirePreexisting = requirePreexisting;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        if (RequirePreexisting && !context.Variables.ContainsVariable(VariableName))
            return (InterpreterResult.VariableNotFound, VariableName);

        if (!context.Variables.IsNameAvailable(VariableName))
            return (InterpreterResult.InvalidName, VariableName);

        // 0. Array literal check: [ ... ]
        string trimmedValue = ValueText.Trim();
        if (trimmedValue.StartsWith('[') && trimmedValue.EndsWith(']'))
        {
            var parseArrayRes = context.ExpressionParser.ParseArrayLiteral(trimmedValue, out object? arrayObj);
            if (parseArrayRes.interpreterResult != InterpreterResult.Success)
                return parseArrayRes;

            context.Variables.SetVariable(VariableName, arrayObj!, RequirePreexisting);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        bool hasExpressionSymbols = InstructionHelper.HasExpressionSymbols(ValueText, ValueParts);

        // 1. String literal check
        if (InstructionHelper.IsValidStringLiteral(ValueText))
        {
            context.Variables.SetVariableFromText(VariableName, ValueText, RequirePreexisting);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        if (InstructionHelper.HasInvalidStringSyntax(ValueText, hasExpressionSymbols))
        {
            return (InterpreterResult.InvalidSyntax, ValueText);
        }

        // 2. User input (cin)
        if (ValueText == LanguageNames.Cin)
        {
            var cinResult = context.Builtins.Cin(VariableName);
            if (cinResult.interpreterResult != InterpreterResult.Success)
                return cinResult;

            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        // 3. Expression
        if (ValueParts.Count > 1 ||
            hasExpressionSymbols)
        {
            var res = context.ExpressionParser.ParseExpression(VariableName, ValueParts, RequirePreexisting);
            if (res.interpreterResult != InterpreterResult.Success)
                return res;

            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        // 4. Variable value copy
        if (context.Variables.TryGetVariable(ValueText, out object? existingVarValue))
        {
            context.Variables.SetVariable(VariableName, existingVarValue!, RequirePreexisting);
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

    public SetIndexInstruction(string arrayName, string indexText, List<string> valueParts, int lineNumber)
        : base(lineNumber)
    {
        ArrayName = arrayName;
        IndexText = indexText;
        ValueParts = valueParts;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        if (!context.Variables.TryGetVariable(ArrayName, out object? arrObj) || arrObj == null)
            return (InterpreterResult.VariableNotFound, ArrayName);

        if (arrObj is not List<object> list)
            return (InterpreterResult.InvalidValue, ArrayName);

        var idxRes = context.ExpressionParser.EvaluateExpression(new List<string> { IndexText }, out object? indexVal);
        if (idxRes.interpreterResult != InterpreterResult.Success)
            return idxRes;

        if (!FunctionRegistry.TryGetDouble(indexVal, out double idxD))
            return (InterpreterResult.InvalidValue, IndexText);

        int index = (int)idxD;
        if (index < 0 || index >= list.Count)
            return (InterpreterResult.InvalidValue, $"Index {index} out of range [0..{list.Count - 1}]");

        var valRes = context.ExpressionParser.EvaluateExpression(ValueParts, out object? evaluatedValue);
        if (valRes.interpreterResult != InterpreterResult.Success)
            return valRes;

        list[index] = evaluatedValue!;
        context.Ip++;
        return (InterpreterResult.Success, null);
    }
}