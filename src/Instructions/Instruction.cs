namespace Qelno.Instructions;

using Qelno.Core;
using Qelno.Runtime;

public abstract class Instruction
{
    public int LineNumber { get; set; }

    protected Instruction(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    public abstract (InterpreterResult interpreterResult, string? subject) Execute(ExecutionContext context);
}
