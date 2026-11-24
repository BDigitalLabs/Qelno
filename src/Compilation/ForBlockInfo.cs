namespace Qelno.Compilation;

using Qelno.Instructions;

internal class ForBlockInfo : CompileBlock
{
    public string VariableName { get; set; } = "";
    public List<string> StepParts { get; set; } = new();
    public string StepText { get; set; } = "1";
    public double? LiteralStep { get; set; }
    public int LoopStartIndex { get; set; }
    public ForConditionInstruction ForCondition { get; set; } = null!;
    public List<JumpInstruction> BreakJumps { get; } = new();
    public List<JumpInstruction> ContinueJumps { get; } = new();
}
