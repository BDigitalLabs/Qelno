namespace Qelno.Instructions;

using Qelno.Core;
using Qelno.Parsing;
using Qelno.Runtime;
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
        var regRes = context.Functions.RegisterUserFunction(Function);
        if (regRes.result != InterpreterResult.Success)
            return regRes;

        context.Ip++;
        return (InterpreterResult.Success, null);
    }
}

public class ReturnInstruction : Instruction
{
    public List<string>? ReturnParts { get; }
    public CompiledExpression? CompiledReturn { get; }

    public ReturnInstruction(List<string>? returnParts, int lineNumber, CompiledExpression? compiledReturn = null)
        : base(lineNumber)
    {
        ReturnParts = returnParts;
        CompiledReturn = compiledReturn;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        if (ReturnParts != null && ReturnParts.Count > 0)
        {
            Value result;

            if (CompiledReturn != null)
            {
                var compiledRes = CompiledExpressionEvaluator.Evaluate(CompiledReturn, context, out result);
                if (compiledRes.interpreterResult != InterpreterResult.Success)
                    return compiledRes;
            }
            else
            {
                var evalResult = context.ExpressionParser.EvaluateExpression(ReturnParts, out result);
                if (evalResult.interpreterResult != InterpreterResult.Success)
                    return evalResult;
            }

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
    public CompiledExpression? CompiledCall { get; }

    public CallInstruction(List<string> callParts, int lineNumber, CompiledExpression? compiledCall = null)
        : base(lineNumber)
    {
        CallParts = callParts;
        CompiledCall = compiledCall;
    }

    public override (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context)
    {
        if (CompiledCall != null)
        {
            var compiledRes = CompiledExpressionEvaluator.Evaluate(CompiledCall, context, out _);
            if (compiledRes.interpreterResult != InterpreterResult.Success)
                return compiledRes;

            context.Ip++;
            return (InterpreterResult.Success, null);
        }

        var evalResult = context.ExpressionParser.EvaluateExpression(CallParts, out _);
        if (evalResult.interpreterResult != InterpreterResult.Success)
            return evalResult;

        context.Ip++;
        return (InterpreterResult.Success, null);
    }
}
