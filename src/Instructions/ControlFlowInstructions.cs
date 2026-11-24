namespace Qelno.Instructions;

using Qelno.Core;
using Qelno.Parsing;
using Qelno.Runtime;
public class JumpInstruction : Instruction
{
    public int TargetIndex { get; set; }

    public JumpInstruction(int targetIndex, int lineNumber)
        : base(lineNumber)
    {
        TargetIndex = targetIndex;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        context.Ip = TargetIndex;
        return (InterpreterResult.Success, null);
    }
}

public class JumpIfFalseInstruction : Instruction
{
    public List<string> ConditionParts { get; }
    public CompiledExpression? Condition { get; }
    public int TargetIndex { get; set; }

    public JumpIfFalseInstruction(
        List<string> conditionParts,
        int targetIndex,
        int lineNumber,
        CompiledExpression? condition = null)
        : base(lineNumber)
    {
        ConditionParts = conditionParts;
        TargetIndex = targetIndex;
        Condition = condition;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        bool condition;

        if (Condition != null)
        {
            var compiledResult = CompiledExpressionEvaluator.Evaluate(Condition, context, out Value conditionValue);
            if (compiledResult.interpreterResult != InterpreterResult.Success)
                return compiledResult;

            if (!conditionValue.IsBool)
                return (InterpreterResult.InvalidValue, string.Join(" ", ConditionParts));

            condition = conditionValue.AsBool();
        }
        else
        {
            var result = context.ExpressionParser.EvaluateCondition(ConditionParts, out condition);
            if (result.interpreterResult != InterpreterResult.Success)
                return result;
        }

        if (!condition)
        {
            context.Ip = TargetIndex;
        }
        else
        {
            context.Ip++;
        }

        return (InterpreterResult.Success, null);
    }
}

public class ExitInstruction : Instruction
{
    public ExitInstruction(int lineNumber)
        : base(lineNumber)
    {
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        context.Terminated = true;
        return (InterpreterResult.Success, null);
    }
}

public class ForConditionInstruction : Instruction
{
    public string VariableName { get; }
    public List<string> EndParts { get; }
    public string EndText { get; }
    public List<string> StepParts { get; }
    public string StepText { get; }
    public int TargetIndex { get; set; }
    public double? LiteralStep { get; }
    public double? LiteralEnd { get; }
    public CompiledExpression? CompiledStep { get; }
    public CompiledExpression? CompiledEnd { get; }

    public ForConditionInstruction(
        string variableName,
        List<string> endParts,
        string endText,
        List<string> stepParts,
        string stepText,
        int targetIndex,
        int lineNumber,
        double? literalStep = null,
        double? literalEnd = null,
        CompiledExpression? compiledStep = null,
        CompiledExpression? compiledEnd = null)
        : base(lineNumber)
    {
        VariableName = variableName;
        EndParts = endParts;
        EndText = endText;
        StepParts = stepParts;
        StepText = stepText;
        TargetIndex = targetIndex;
        LiteralStep = literalStep;
        LiteralEnd = literalEnd;
        CompiledStep = compiledStep;
        CompiledEnd = compiledEnd;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        double stepD;
        if (LiteralStep.HasValue)
        {
            stepD = LiteralStep.Value;
        }
        else
        {
            // 1. Evaluate step expression
            Value stepVal;

            if (CompiledStep != null)
            {
                var compiledStepRes = CompiledExpressionEvaluator.Evaluate(CompiledStep, context, out stepVal);
                if (compiledStepRes.interpreterResult != InterpreterResult.Success)
                    return compiledStepRes;
            }
            else
            {
                var stepRes = context.ExpressionParser.EvaluateExpression(StepParts, out stepVal);
                if (stepRes.interpreterResult != InterpreterResult.Success)
                    return stepRes;
            }

            if (!FunctionRegistry.TryGetDouble(stepVal, out stepD))
                return (InterpreterResult.InvalidValue, StepText);

            if (double.IsNaN(stepD) || double.IsInfinity(stepD) || stepD == 0.0)
                return (InterpreterResult.InvalidValue, StepText);
        }

        double endD;
        if (LiteralEnd.HasValue)
        {
            endD = LiteralEnd.Value;
        }
        else
        {
            // 2. Evaluate end bound expression
            Value endVal;

            if (CompiledEnd != null)
            {
                var compiledEndRes = CompiledExpressionEvaluator.Evaluate(CompiledEnd, context, out endVal);
                if (compiledEndRes.interpreterResult != InterpreterResult.Success)
                    return compiledEndRes;
            }
            else
            {
                var endRes = context.ExpressionParser.EvaluateExpression(EndParts, out endVal);
                if (endRes.interpreterResult != InterpreterResult.Success)
                    return endRes;
            }

            if (!FunctionRegistry.TryGetDouble(endVal, out endD))
                return (InterpreterResult.InvalidValue, EndText);
        }

        // 3. Read loop variable
        if (!context.Variables.TryGetVariable(VariableName, out Value curVal))
            return (InterpreterResult.VariableNotFound, VariableName);

        if (!FunctionRegistry.TryGetDouble(curVal, out double curD))
            return (InterpreterResult.InvalidValue, VariableName);

        // 4. Direction check based on dynamic step sign
        bool continueLoop = stepD > 0 ? (curD <= endD) : (curD >= endD);

        if (continueLoop)
        {
            context.Ip++;
        }
        else
        {
            context.Ip = TargetIndex;
        }

        return (InterpreterResult.Success, null);
    }
}

public class ForStepInstruction : Instruction
{
    public string VariableName { get; }
    public double Step { get; }
    private readonly Value _stepValue;

    public ForStepInstruction(string variableName, double step, int lineNumber)
        : base(lineNumber)
    {
        VariableName = variableName;
        Step = step;
        if (Math.Floor(step) == step && step >= long.MinValue && step <= long.MaxValue)
        {
            _stepValue = Value.FromInteger((long)step);
        }
        else
        {
            _stepValue = Value.FromFloat(step);
        }
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        if (!context.Variables.TryGetVariable(VariableName, out Value curVal))
            return (InterpreterResult.VariableNotFound, VariableName);

        var (result, newVal) = Value.Add(curVal, _stepValue);
        if (result != InterpreterResult.Success)
            return (result, VariableName);

        context.Variables.SetVariable(VariableName, newVal, requirePreexisting: true);
        context.Ip++;
        return (InterpreterResult.Success, null);
    }
}
