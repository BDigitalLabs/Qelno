using System.Collections.Generic;

internal class WhileBlockInfo : CompileBlock
{
    public int LoopStartIndex { get; set; }
    public JumpIfFalseInstruction JumpIfFalse { get; set; } = null!;
    public List<JumpInstruction> BreakJumps { get; } = new();
    public List<JumpInstruction> ContinueJumps { get; } = new();
}
