using System;
using System.Collections.Generic;

public static class Compiler
{
    public static (InterpreterResult result, string? subject, int? lineNumber, List<Instruction>? instructions) Compile(
        IEnumerable<string> lines)
    {
        List<Instruction> mainInstructions = new();
        List<Instruction> currentInstructions = mainInstructions;
        Stack<CompileBlock> blocks = new();
        int lineNumber = 0;

        foreach (string rawLine in lines)
        {
            lineNumber++;

            List<string> parts = new List<string>(
                rawLine.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries
                )
            );

            if (parts.Count == 0 || parts[0].StartsWith(LanguageNames.OneLineComment))
                continue;

            (InterpreterResult result, string? subject) stepResult = parts[0] switch
            {
                LanguageNames.Def => CompileDef(rawLine, lineNumber, blocks, ref currentInstructions),
                LanguageNames.Return => CompileReturn(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.Break or LanguageNames.Continue => CompileBreakContinue(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.If => CompileIf(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.While => CompileWhile(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.For => CompileFor(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.Elif => CompileElif(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.Else => CompileElse(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.End => CompileEnd(parts, lineNumber, blocks, ref currentInstructions),
                LanguageNames.Set => CompileSet(parts, lineNumber, currentInstructions),
                LanguageNames.Cout => CompileCout(parts, lineNumber, currentInstructions),
                _ => CompileDefault(parts, lineNumber, currentInstructions)
            };

            if (stepResult.result != InterpreterResult.Success)
            {
                return (stepResult.result, stepResult.subject, lineNumber, null);
            }
        }

        if (blocks.Count > 0)
        {
            return (InterpreterResult.InvalidSyntax, LanguageNames.End, lineNumber, null);
        }

        return (InterpreterResult.Success, null, null, mainInstructions);
    }

    private static (InterpreterResult result, string? subject) CompileDef(
        string rawLine,
        int lineNumber,
        Stack<CompileBlock> blocks,
        ref List<Instruction> currentInstructions)
    {
        if (!SyntaxValidator.TryParseDefHeader(rawLine, out string funcName, out List<string> parameters))
            return (InterpreterResult.InvalidSyntax, LanguageNames.Def);

        var defBlock = new DefBlockInfo
        {
            FunctionName = funcName,
            LineNumber = lineNumber,
            ParentInstructions = currentInstructions
        };
        defBlock.Parameters.AddRange(parameters);
        blocks.Push(defBlock);
        currentInstructions = defBlock.BodyInstructions;
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileReturn(
        List<string> parts,
        int lineNumber,
        Stack<CompileBlock> blocks,
        List<Instruction> currentInstructions)
    {
        bool inDef = false;
        foreach (var b in blocks)
        {
            if (b is DefBlockInfo)
            {
                inDef = true;
                break;
            }
        }

        if (!inDef)
            return (InterpreterResult.InvalidSyntax, LanguageNames.Return);

        List<string> returnParts = parts.Count > 1 ? parts.GetRange(1, parts.Count - 1) : new List<string>();

        currentInstructions.Add(new ReturnInstruction(returnParts, lineNumber));
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileBreakContinue(
        List<string> parts,
        int lineNumber,
        Stack<CompileBlock> blocks,
        List<Instruction> currentInstructions)
    {
        if (parts.Count != 1)
            return (InterpreterResult.InvalidSyntax, parts[0]);

        CompileBlock? targetLoop = null;
        foreach (var b in blocks)
        {
            if (b is WhileBlockInfo or ForBlockInfo)
            {
                targetLoop = b;
                break;
            }
            if (b is DefBlockInfo)
            {
                break;
            }
        }

        if (targetLoop == null)
            return (InterpreterResult.InvalidSyntax, parts[0]);

        var jump = new JumpInstruction(targetIndex: -1, lineNumber);
        currentInstructions.Add(jump);

        if (parts[0] == LanguageNames.Break)
        {
            if (targetLoop is WhileBlockInfo wb) wb.BreakJumps.Add(jump);
            else if (targetLoop is ForBlockInfo fb) fb.BreakJumps.Add(jump);
        }
        else
        {
            if (targetLoop is WhileBlockInfo wb) wb.ContinueJumps.Add(jump);
            else if (targetLoop is ForBlockInfo fb) fb.ContinueJumps.Add(jump);
        }

        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileIf(
        List<string> parts,
        int lineNumber,
        Stack<CompileBlock> blocks,
        List<Instruction> currentInstructions)
    {
        if (parts.Count < 2)
            return (InterpreterResult.InvalidSyntax, LanguageNames.If);

        List<string> conditionParts = parts.GetRange(1, parts.Count - 1);
        var jif = new JumpIfFalseInstruction(conditionParts, targetIndex: -1, lineNumber);
        currentInstructions.Add(jif);

        blocks.Push(new IfBlockInfo
        {
            CurrentBranchJumpIfFalse = jif,
            LineNumber = lineNumber
        });
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileWhile(
        List<string> parts,
        int lineNumber,
        Stack<CompileBlock> blocks,
        List<Instruction> currentInstructions)
    {
        if (parts.Count < 2)
            return (InterpreterResult.InvalidSyntax, LanguageNames.While);

        int loopStartIndex = currentInstructions.Count;
        List<string> conditionParts = parts.GetRange(1, parts.Count - 1);
        var jif = new JumpIfFalseInstruction(conditionParts, targetIndex: -1, lineNumber);
        currentInstructions.Add(jif);

        blocks.Push(new WhileBlockInfo
        {
            LoopStartIndex = loopStartIndex,
            JumpIfFalse = jif,
            LineNumber = lineNumber
        });
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileFor(
        List<string> parts,
        int lineNumber,
        Stack<CompileBlock> blocks,
        List<Instruction> currentInstructions)
    {
        int toIndex = parts.IndexOf(LanguageNames.To);
        if (parts.Count < 6 || parts[2] != "=" || toIndex <= 3 || toIndex >= parts.Count - 1)
            return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));

        string varName = parts[1];
        List<string> startParts = parts.GetRange(3, toIndex - 3);
        string startText = string.Join(" ", startParts);

        int stepIndex = parts.IndexOf(LanguageNames.Step);
        List<string> endParts;
        string stepText = "1";

        if (stepIndex != -1)
        {
            if (stepIndex <= toIndex + 1 || stepIndex >= parts.Count - 1)
                return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));

            endParts = parts.GetRange(toIndex + 1, stepIndex - (toIndex + 1));
            List<string> stepParts = parts.GetRange(stepIndex + 1, parts.Count - (stepIndex + 1));
            stepText = string.Join(" ", stepParts);
        }
        else
        {
            endParts = parts.GetRange(toIndex + 1, parts.Count - (toIndex + 1));
        }

        currentInstructions.Add(new SetInstruction(varName, startParts, startText, lineNumber, requirePreexisting: false));

        int loopStartIndex = currentInstructions.Count;
        bool isNegativeStep = stepText.Trim().StartsWith('-');
        string comparisonOp = isNegativeStep ? ">=" : "<=";

        List<string> conditionParts = new() { varName, comparisonOp };
        conditionParts.AddRange(endParts);

        var jif = new JumpIfFalseInstruction(conditionParts, targetIndex: -1, lineNumber);
        currentInstructions.Add(jif);

        blocks.Push(new ForBlockInfo
        {
            VariableName = varName,
            StepText = stepText,
            LoopStartIndex = loopStartIndex,
            JumpIfFalse = jif,
            LineNumber = lineNumber
        });
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileElif(
        List<string> parts,
        int lineNumber,
        Stack<CompileBlock> blocks,
        List<Instruction> currentInstructions)
    {
        if (blocks.Count == 0 || !(blocks.Peek() is IfBlockInfo currentBlock) || parts.Count < 2)
            return (InterpreterResult.InvalidSyntax, LanguageNames.Elif);

        var jumpToEnd = new JumpInstruction(targetIndex: -1, lineNumber);
        currentInstructions.Add(jumpToEnd);
        currentBlock.BranchEndJumps.Add(jumpToEnd);

        if (currentBlock.CurrentBranchJumpIfFalse != null)
        {
            currentBlock.CurrentBranchJumpIfFalse.TargetIndex = currentInstructions.Count;
        }

        List<string> conditionParts = parts.GetRange(1, parts.Count - 1);
        var jif = new JumpIfFalseInstruction(conditionParts, targetIndex: -1, lineNumber);
        currentInstructions.Add(jif);
        currentBlock.CurrentBranchJumpIfFalse = jif;
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileElse(
        List<string> parts,
        int lineNumber,
        Stack<CompileBlock> blocks,
        List<Instruction> currentInstructions)
    {
        if (blocks.Count == 0 || !(blocks.Peek() is IfBlockInfo currentBlock) || parts.Count != 1)
            return (InterpreterResult.InvalidSyntax, LanguageNames.Else);

        var jumpToEnd = new JumpInstruction(targetIndex: -1, lineNumber);
        currentInstructions.Add(jumpToEnd);
        currentBlock.BranchEndJumps.Add(jumpToEnd);

        if (currentBlock.CurrentBranchJumpIfFalse != null)
        {
            currentBlock.CurrentBranchJumpIfFalse.TargetIndex = currentInstructions.Count;
            currentBlock.CurrentBranchJumpIfFalse = null;
        }
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileEnd(
        List<string> parts,
        int lineNumber,
        Stack<CompileBlock> blocks,
        ref List<Instruction> currentInstructions)
    {
        if (blocks.Count == 0 || parts.Count != 1)
            return (InterpreterResult.InvalidSyntax, LanguageNames.End);

        var block = blocks.Pop();

        if (block is DefBlockInfo defBlock)
        {
            if (defBlock.BodyInstructions.Count == 0 ||
                defBlock.BodyInstructions[^1] is not ReturnInstruction)
            {
                defBlock.BodyInstructions.Add(new ReturnInstruction(new List<string>(), lineNumber));
            }

            currentInstructions = defBlock.ParentInstructions;
            var userFn = new UserFunction(defBlock.FunctionName, defBlock.Parameters, defBlock.BodyInstructions);
            currentInstructions.Add(new DefineFunctionInstruction(userFn, defBlock.LineNumber));
        }
        else if (block is WhileBlockInfo whileBlock)
        {
            foreach (var cj in whileBlock.ContinueJumps)
            {
                cj.TargetIndex = whileBlock.LoopStartIndex;
            }

            currentInstructions.Add(new JumpInstruction(whileBlock.LoopStartIndex, lineNumber));
            int exitIndex = currentInstructions.Count;
            whileBlock.JumpIfFalse.TargetIndex = exitIndex;

            foreach (var bj in whileBlock.BreakJumps)
            {
                bj.TargetIndex = exitIndex;
            }
        }
        else if (block is ForBlockInfo forBlock)
        {
            int stepIndex = currentInstructions.Count;
            foreach (var cj in forBlock.ContinueJumps)
            {
                cj.TargetIndex = stepIndex;
            }

            List<string> incParts = new() { forBlock.VariableName, "+", forBlock.StepText };
            string incText = $"{forBlock.VariableName} + {forBlock.StepText}";
            currentInstructions.Add(new SetInstruction(forBlock.VariableName, incParts, incText, lineNumber, requirePreexisting: false));

            currentInstructions.Add(new JumpInstruction(forBlock.LoopStartIndex, lineNumber));
            int exitIndex = currentInstructions.Count;
            forBlock.JumpIfFalse.TargetIndex = exitIndex;

            foreach (var bj in forBlock.BreakJumps)
            {
                bj.TargetIndex = exitIndex;
            }
        }
        else if (block is IfBlockInfo ifBlock)
        {
            if (ifBlock.CurrentBranchJumpIfFalse != null)
            {
                ifBlock.CurrentBranchJumpIfFalse.TargetIndex = currentInstructions.Count;
            }

            foreach (var jumpToEnd in ifBlock.BranchEndJumps)
            {
                jumpToEnd.TargetIndex = currentInstructions.Count;
            }
        }
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileSet(
        List<string> parts,
        int lineNumber,
        List<Instruction> currentInstructions)
    {
        if (parts.Count < 4 || parts[2] != "=")
            return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));

        string name = parts[1];
        List<string> valueParts = parts.GetRange(3, parts.Count - 3);
        string valueText = string.Join(" ", valueParts);

        currentInstructions.Add(new SetInstruction(name, valueParts, valueText, lineNumber, requirePreexisting: false));
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileCout(
        List<string> parts,
        int lineNumber,
        List<Instruction> currentInstructions)
    {
        if (parts.Count < 2)
        {
            currentInstructions.Add(new CoutInstruction(new List<string>(), string.Empty, lineNumber));
            return (InterpreterResult.Success, null);
        }

        List<string> argParts = parts.GetRange(1, parts.Count - 1);
        string argText = string.Join(" ", argParts);

        currentInstructions.Add(new CoutInstruction(argParts, argText, lineNumber));
        return (InterpreterResult.Success, null);
    }

    private static (InterpreterResult result, string? subject) CompileDefault(
        List<string> parts,
        int lineNumber,
        List<Instruction> currentInstructions)
    {
        // Direct index assignment: <array>[<index>] = <expression>
        int eqIndex = parts.IndexOf("=");
        if (eqIndex > 0)
        {
            string leftExpr = string.Join("", parts.GetRange(0, eqIndex));
            if (ExpressionParser.IsSingleIndexAccess(leftExpr))
            {
                int bracketOpen = leftExpr.IndexOf('[');
                string arrName = leftExpr[..bracketOpen].Trim();
                string indexExpr = leftExpr.Substring(bracketOpen + 1, leftExpr.Length - bracketOpen - 2).Trim();

                List<string> valParts = parts.GetRange(eqIndex + 1, parts.Count - eqIndex - 1);
                if (valParts.Count == 0)
                    return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));

                currentInstructions.Add(new SetIndexInstruction(arrName, indexExpr, valParts, lineNumber));
                return (InterpreterResult.Success, null);
            }
        }

        // Direct variable assignment: <name> = <expression>
        if (parts.Count >= 2 && parts[1] == "=")
        {
            if (parts.Count < 3)
                return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));

            string name = parts[0];
            List<string> valueParts = parts.GetRange(2, parts.Count - 2);
            string valueText = string.Join(" ", valueParts);

            currentInstructions.Add(new SetInstruction(name, valueParts, valueText, lineNumber, requirePreexisting: true));
            return (InterpreterResult.Success, null);
        }

        // Direct function call: <function>(...)
        string fullLine = string.Join(" ", parts);
        int openParen = fullLine.IndexOf('(');
        if (openParen > 0 && fullLine.EndsWith(')'))
        {
            currentInstructions.Add(new CallInstruction(parts, lineNumber));
            return (InterpreterResult.Success, null);
        }

        return (InterpreterResult.UnknownCommand, parts[0]);
    }
}