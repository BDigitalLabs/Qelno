using System;
using System.Collections.Generic;
using System.Globalization;

public class CoutInstruction : Instruction
{
    public List<string> ArgParts { get; }
    public string ArgText { get; }

    public CoutInstruction(List<string> argParts, string argText, int lineNumber)
        : base(lineNumber)
    {
        ArgParts = argParts;
        ArgText = argText;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        if (ArgParts.Count == 0)
            return (InterpreterResult.MissingVariableName, null);

        bool hasExpressionSymbols = InstructionHelper.HasExpressionSymbols(ArgText, ArgParts);

        // 1. Quoted string literal
        if (InstructionHelper.IsValidStringLiteral(ArgText))
        {
            context.Builtins.Cout(ArgText[1..^1]);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        if (InstructionHelper.HasInvalidStringSyntax(ArgText, hasExpressionSymbols))
        {
            return (InterpreterResult.InvalidSyntax, ArgText);
        }

        // 2. User input (cin)
        if (ArgText == LanguageNames.Cin)
        {
            var (cinResult, subject, line) = context.Builtins.ReadInput();
            if (cinResult != InterpreterResult.Success)
                return (cinResult, subject);

            context.Builtins.Cout(line ?? string.Empty);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        // 3. Boolean constants (true / false)
        if (ArgText == LanguageNames.True)
        {
            context.Builtins.Cout(true);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        if (ArgText == LanguageNames.False)
        {
            context.Builtins.Cout(false);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        // 4. Numeric constants (integer / floating-point)
        if (long.TryParse(
            ArgText,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long longResult))
        {
            context.Builtins.Cout(longResult);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        if (double.TryParse(
            ArgText,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double doubleResult))
        {
            context.Builtins.Cout(doubleResult);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        // 5. Variable value
        if (context.Variables.TryGetVariable(ArgText, out object? value))
        {
            context.Builtins.Cout(value!);
            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        // 6. Function or expression call (e.g.: cout topla(2, 3), cout "Result: " + 42 or cout "a" == "a")
        if (hasExpressionSymbols)
        {
            var evalResult = context.ExpressionParser.EvaluateExpression(ArgParts, out object? exprValue);
            if (evalResult.interpreterResult == InterpreterResult.Success)
            {
                if (exprValue != null)
                {
                    context.Builtins.Cout(exprValue);
                }
                context.Ip++;
                return (InterpreterResult.Success, null);
            }
            return evalResult;
        }

        // Multiple parts that are not quoted and not a function are invalid syntax.
        if (ArgParts.Count > 1)
            return (InterpreterResult.InvalidSyntax, ArgText);

        return (InterpreterResult.VariableNotFound, ArgText);
    }
}