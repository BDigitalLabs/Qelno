using System.Collections.Generic;

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
    public int TargetIndex { get; set; }

    public JumpIfFalseInstruction(List<string> conditionParts, int targetIndex, int lineNumber)
        : base(lineNumber)
    {
        ConditionParts = conditionParts;
        TargetIndex = targetIndex;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        var result = context.ExpressionParser.EvaluateCondition(ConditionParts, out bool condition);
        if (result.interpreterResult != InterpreterResult.Success)
            return result;

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