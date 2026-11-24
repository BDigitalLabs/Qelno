namespace Qelno.Compilation;

using Qelno.Instructions;

internal class WhileBlockInfo : CompileBlock
{
    public int LoopStartIndex { get; set; }
    public JumpIfFalseInstruction JumpIfFalse { get; set; } = null!;
    public List<JumpInstruction> BreakJumps { get; } = new();
    public List<JumpInstruction> ContinueJumps { get; } = new();
}
