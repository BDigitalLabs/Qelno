namespace Qelno.Runtime;

using Qelno.Core;
using Qelno.Instructions;
using Qelno.Parsing;
using Qelno.Compilation;
public class Parser
{
    private readonly Variables variables;
    private readonly ExpressionParser expressionParser;
    private readonly ExecutionContext context;

    public Variables Variables => variables;
    public ExecutionContext Context => context;

    public Parser()
    {
        variables = new Variables();
        expressionParser = new ExpressionParser(variables);
        context = new ExecutionContext(variables, expressionParser);
        expressionParser.SetContext(context);
    }

    public (InterpreterResult interpreterResult, string? subject, int? lineNumber, List<Instruction>? instructions)
        Compile(IEnumerable<string> lines)
    {
        return Compiler.Compile(lines);
    }

    public (InterpreterResult interpreterResult, string? subject, int? lineNumber)
        Execute(List<Instruction> instructions)
    {
        context.Ip = 0;
        context.Terminated = false;
        context.CurrentErrorLine = null;

        while (context.Ip >= 0 && context.Ip < instructions.Count && !context.Terminated)
        {
            Instruction instruction = instructions[context.Ip];
            var result = instruction.Execute(context);
            if (result.interpreterResult != InterpreterResult.Success)
            {
                int errorLine = context.CurrentErrorLine ?? instruction.LineNumber;
                return (result.interpreterResult, result.subject, errorLine);
            }
        }

        if (context.Ip < 0 && !context.Terminated)
        {
            return (InterpreterResult.InvalidSyntax, "Invalid instruction pointer", null);
        }

        return (InterpreterResult.Success, null, null);
    }
}
