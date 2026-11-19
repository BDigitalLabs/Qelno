
using System.Collections.Generic;

public class Parser
{
    private readonly Variables variables;
    private readonly ExpressionParser expressionParser;
    private readonly Builtins builtins;
    private readonly ExecutionContext context;

    public Variables Variables => variables;
    public ExecutionContext Context => context;

    public Parser()
    {
        variables = new Variables();
        builtins = new Builtins(variables);
        expressionParser = new ExpressionParser(variables, builtins);
        context = new ExecutionContext(variables, builtins, expressionParser);
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

        while (context.Ip >= 0 && context.Ip < instructions.Count && !context.Terminated)
        {
            Instruction instruction = instructions[context.Ip];
            var result = instruction.Execute(context);
            if (result.interpreterResult != InterpreterResult.Success)
            {
                return (result.interpreterResult, result.subject, instruction.LineNumber);
            }
        }

        return (InterpreterResult.Success, null, null);
    }
}
