namespace Qelno.Compilation;

using Qelno.Instructions;

internal class DefBlockInfo : CompileBlock
{
    public string FunctionName { get; set; } = "";
    public List<string> Parameters { get; } = new();
    public List<Instruction> BodyInstructions { get; } = new();
    public List<Instruction> ParentInstructions { get; set; } = null!;
}
