namespace Qelno.Compilation;

using Qelno.Instructions;

internal class IfBlockInfo : CompileBlock
{
    public JumpIfFalseInstruction? CurrentBranchJumpIfFalse { get; set; }
    public List<JumpInstruction> BranchEndJumps { get; } = new();
    public bool ElseSeen { get; set; }
}
