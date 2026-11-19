using System.Collections.Generic;

internal class ForBlockInfo : CompileBlock
{
    public string VariableName { get; set; } = "";
    public string StepText { get; set; } = "1";
    public int LoopStartIndex { get; set; }
    public JumpIfFalseInstruction JumpIfFalse { get; set; } = null!;
    public List<JumpInstruction> BreakJumps { get; } = new();
    public List<JumpInstruction> ContinueJumps { get; } = new();
}
