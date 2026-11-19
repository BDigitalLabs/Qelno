using System.Collections.Generic;

internal class IfBlockInfo : CompileBlock
{
    public JumpIfFalseInstruction? CurrentBranchJumpIfFalse { get; set; }
    public List<JumpInstruction> BranchEndJumps { get; } = new();
}
