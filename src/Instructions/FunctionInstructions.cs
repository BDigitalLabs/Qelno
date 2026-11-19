using System.Collections.Generic;

public class DefineFunctionInstruction : Instruction
{
    public UserFunction Function { get; }

    public DefineFunctionInstruction(UserFunction function, int lineNumber)
        : base(lineNumber)
    {
        Function = function;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        context.Functions.RegisterUserFunction(Function);
        context.Ip++;
        return (InterpreterResult.Success, null);
    }
}

public class ReturnInstruction : Instruction
{
    public List<string>? ReturnParts { get; }

    public ReturnInstruction(List<string>? returnParts, int lineNumber)
        : base(lineNumber)
    {
        ReturnParts = returnParts;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        if (ReturnParts != null && ReturnParts.Count > 0)
        {
            var evalResult = context.ExpressionParser.EvaluateExpression(ReturnParts, out object? result);
            if (evalResult.interpreterResult != InterpreterResult.Success)
                return evalResult;

            context.LastReturnValue = result;
        }
        else
        {
            context.LastReturnValue = null;
        }

        context.IsReturning = true;
        context.Ip++;
        return (InterpreterResult.Success, null);
    }
}

public class CallInstruction : Instruction
{
    public List<string> CallParts { get; }

    public CallInstruction(List<string> callParts, int lineNumber)
        : base(lineNumber)
    {
        CallParts = callParts;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        var evalResult = context.ExpressionParser.EvaluateExpression(CallParts, out _);
        if (evalResult.interpreterResult != InterpreterResult.Success)
            return evalResult;

        context.Ip++;
        return (InterpreterResult.Success, null);
    }
}