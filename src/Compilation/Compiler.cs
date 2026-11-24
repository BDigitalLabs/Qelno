namespace Qelno.Compilation;

using Qelno.Core;
using Qelno.Instructions;
using Qelno.Runtime;
using Qelno.Parsing;
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

            string line = InstructionHelper.StripComment(rawLine);
            List<string> parts = InstructionHelper.SplitLinePreservingQuotes(line);

            if (parts.Count == 0 || parts[0].StartsWith(LanguageNames.OneLineComment))
                continue;

            (InterpreterResult result, string? subject) stepResult = parts[0] switch
            {
                LanguageNames.Def => CompileDef(line, lineNumber, blocks, ref currentInstructions),
                LanguageNames.Return => CompileReturn(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.Break or LanguageNames.Continue => CompileBreakContinue(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.If => CompileIf(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.While => CompileWhile(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.For => CompileFor(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.Elif => CompileElif(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.Else => CompileElse(parts, lineNumber, blocks, currentInstructions),
                LanguageNames.End => CompileEnd(parts, lineNumber, blocks, ref currentInstructions),
                LanguageNames.Set => CompileSet(parts, lineNumber, currentInstructions),
                _ => CompileDefault(parts, lineNumber, currentInstructions)
            };

            if (stepResult.result != InterpreterResult.Success)
            {
                return (stepResult.result, stepResult.subject, lineNumber, null);
            }
        }

        if (blocks.Count > 0)
        {
            return (InterpreterResult.InvalidSyntax, LanguageNames.End, blocks.Peek().LineNumber, null);
        }

        foreach (var instr in mainInstructions)
        {
            if (instr is JumpInstruction jump && jump.TargetIndex < 0)
                return (InterpreterResult.InvalidSyntax, "Unpatched jump", lineNumber, null);
            if (instr is JumpIfFalseInstruction jif && jif.TargetIndex < 0)
                return (InterpreterResult.InvalidSyntax, "Unpatched jump", lineNumber, null);
            if (instr is DefineFunctionInstruction defInstr)
            {
                foreach (var bodyInstr in defInstr.Function.Body)
                {
                    if (bodyInstr is JumpInstruction bj && bj.TargetIndex < 0)
                        return (InterpreterResult.InvalidSyntax, "Unpatched jump", lineNumber, null);
                    if (bodyInstr is JumpIfFalseInstruction bjif && bjif.TargetIndex < 0)
                        return (InterpreterResult.InvalidSyntax, "Unpatched jump", lineNumber, null);
                }
            }
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

        currentInstructions.Add(new ReturnInstruction(
            returnParts,
            lineNumber,
            TryCompileExpression(returnParts)));
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
        var jif = new JumpIfFalseInstruction(
            conditionParts,
            targetIndex: -1,
            lineNumber,
            TryCompileExpression(conditionParts));
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
        var jif = new JumpIfFalseInstruction(
            conditionParts,
            targetIndex: -1,
            lineNumber,
            TryCompileExpression(conditionParts));
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
        if (!SyntaxValidator.IsValidIdentifier(varName) || LanguageNames.Contains(varName))
            return (InterpreterResult.InvalidSyntax, varName);

        List<string> startParts = parts.GetRange(3, toIndex - 3);
        string startText = string.Join(" ", startParts);

        int stepIndex = parts.IndexOf(LanguageNames.Step);
        List<string> endParts;
        List<string> stepParts = new() { "1" };
        string stepText = "1";

        if (stepIndex != -1)
        {
            if (stepIndex <= toIndex + 1 || stepIndex >= parts.Count - 1)
                return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));

            endParts = parts.GetRange(toIndex + 1, stepIndex - (toIndex + 1));
            stepParts = parts.GetRange(stepIndex + 1, parts.Count - (stepIndex + 1));
            stepText = string.Join(" ", stepParts);

            if (double.TryParse(stepText.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedStep) && parsedStep == 0.0)
            {
                return (InterpreterResult.InvalidValue, stepText.Trim());
            }
        }
        else
        {
            endParts = parts.GetRange(toIndex + 1, parts.Count - (toIndex + 1));
        }

        double? literalStep = null;
        if (InstructionHelper.TryParseLiteralNumber(stepText, out double constStep))
        {
            if (constStep == 0.0)
            {
                return (InterpreterResult.InvalidValue, stepText.Trim());
            }
            literalStep = constStep;
        }

        string endText = string.Join(" ", endParts);
        double? literalEnd = null;
        if (InstructionHelper.TryParseLiteralNumber(endText, out double constEnd))
        {
            literalEnd = constEnd;
        }

        currentInstructions.Add(new SetInstruction(
            varName,
            startParts,
            startText,
            lineNumber,
            requirePreexisting: false,
            TryCompileSetValue(startParts, startText)));

        int loopStartIndex = currentInstructions.Count;
        var forCond = new ForConditionInstruction(
            varName,
            endParts,
            endText,
            stepParts,
            stepText,
            targetIndex: -1,
            lineNumber,
            literalStep,
            literalEnd,
            TryCompileExpression(stepParts),
            TryCompileExpression(endParts));
        currentInstructions.Add(forCond);

        blocks.Push(new ForBlockInfo
        {
            VariableName = varName,
            StepParts = stepParts,
            StepText = stepText,
            LiteralStep = literalStep,
            LoopStartIndex = loopStartIndex,
            ForCondition = forCond,
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
        if (blocks.Count == 0 || !(blocks.Peek() is IfBlockInfo currentBlock) ||
            currentBlock.ElseSeen || parts.Count < 2)
            return (InterpreterResult.InvalidSyntax, LanguageNames.Elif);

        var jumpToEnd = new JumpInstruction(targetIndex: -1, lineNumber);
        currentInstructions.Add(jumpToEnd);
        currentBlock.BranchEndJumps.Add(jumpToEnd);

        if (currentBlock.CurrentBranchJumpIfFalse != null)
        {
            currentBlock.CurrentBranchJumpIfFalse.TargetIndex = currentInstructions.Count;
        }

        List<string> conditionParts = parts.GetRange(1, parts.Count - 1);
        var jif = new JumpIfFalseInstruction(
            conditionParts,
            targetIndex: -1,
            lineNumber,
            TryCompileExpression(conditionParts));
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
        if (blocks.Count == 0 || !(blocks.Peek() is IfBlockInfo currentBlock) ||
            currentBlock.ElseSeen || parts.Count != 1)
            return (InterpreterResult.InvalidSyntax, LanguageNames.Else);

        currentBlock.ElseSeen = true;

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

            if (forBlock.LiteralStep.HasValue)
            {
                currentInstructions.Add(new ForStepInstruction(forBlock.VariableName, forBlock.LiteralStep.Value, lineNumber));
            }
            else
            {
                List<string> incParts = new() { forBlock.VariableName, "+" };
                if (forBlock.StepParts.Count > 1)
                {
                    incParts.Add("(");
                    incParts.AddRange(forBlock.StepParts);
                    incParts.Add(")");
                }
                else
                {
                    incParts.AddRange(forBlock.StepParts);
                }
                string incText = $"{forBlock.VariableName} + {forBlock.StepText}";
                currentInstructions.Add(new SetInstruction(
                    forBlock.VariableName,
                    incParts,
                    incText,
                    lineNumber,
                    requirePreexisting: false,
                    TryCompileSetValue(incParts, incText)));
            }

            currentInstructions.Add(new JumpInstruction(forBlock.LoopStartIndex, lineNumber));
            int exitIndex = currentInstructions.Count;
            forBlock.ForCondition.TargetIndex = exitIndex;

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
        if (parts.Count >= 2 && parts[1] == "=")
            return (InterpreterResult.MissingVariableName, null);

        string name;
        List<string> valueParts;
        string valueText;

        if (parts.Count >= 2 && parts[1].Contains('='))
        {
            int eqPos = parts[1].IndexOf('=');
            name = parts[1][..eqPos].Trim();
            string afterEq = parts[1][(eqPos + 1)..].Trim();
            valueParts = new List<string>();
            if (!string.IsNullOrEmpty(afterEq))
                valueParts.Add(afterEq);
            if (parts.Count > 2)
                valueParts.AddRange(parts.GetRange(2, parts.Count - 2));
            valueText = string.Join(" ", valueParts);
        }
        else if (parts.Count >= 4 && parts[2] == "=")
        {
            name = parts[1];
            valueParts = parts.GetRange(3, parts.Count - 3);
            valueText = string.Join(" ", valueParts);
        }
        else
        {
            return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));
        }

        if (string.IsNullOrEmpty(name))
            return (InterpreterResult.MissingVariableName, null);

        if (!SyntaxValidator.IsValidIdentifier(name) || LanguageNames.Contains(name))
            return (InterpreterResult.InvalidSyntax, name);

        currentInstructions.Add(new SetInstruction(
            name,
            valueParts,
            valueText,
            lineNumber,
            requirePreexisting: false,
            TryCompileSetValue(valueParts, valueText)));
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

                currentInstructions.Add(new SetIndexInstruction(
                    arrName,
                    indexExpr,
                    valParts,
                    lineNumber,
                    TryCompileExpression(new List<string>(1) { indexExpr }),
                    TryCompileExpression(valParts)));
                return (InterpreterResult.Success, null);
            }
        }

        // Direct variable assignment: <name> = <expression>
        if (parts.Count >= 2 && parts[1] == "=")
        {
            if (parts.Count < 3)
                return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));

            string name = parts[0];
            if (!SyntaxValidator.IsValidIdentifier(name) || LanguageNames.Contains(name))
                return (InterpreterResult.InvalidSyntax, name);

            List<string> valueParts = parts.GetRange(2, parts.Count - 2);
            string valueText = string.Join(" ", valueParts);

            currentInstructions.Add(new SetInstruction(
                name,
                valueParts,
                valueText,
                lineNumber,
                requirePreexisting: true,
                TryCompileSetValue(valueParts, valueText)));
            return (InterpreterResult.Success, null);
        }

        // Direct variable or index assignment without space around =: x=5 or arr[0]=9
        if (parts.Count >= 1 && parts[0].Contains('=') && !parts[0].Contains("==") && !parts[0].Contains("!=") && !parts[0].Contains("<=") && !parts[0].Contains(">="))
        {
            int eqPos = parts[0].IndexOf('=');
            string leftExpr = parts[0][..eqPos].Trim();
            if (ExpressionParser.IsSingleIndexAccess(leftExpr))
            {
                int bracketOpen = leftExpr.IndexOf('[');
                string arrName = leftExpr[..bracketOpen].Trim();
                string indexExpr = leftExpr.Substring(bracketOpen + 1, leftExpr.Length - bracketOpen - 2).Trim();

                string afterEq = parts[0][(eqPos + 1)..].Trim();
                List<string> valParts = new();
                if (!string.IsNullOrEmpty(afterEq))
                    valParts.Add(afterEq);
                if (parts.Count > 1)
                    valParts.AddRange(parts.GetRange(1, parts.Count - 1));

                if (valParts.Count == 0)
                    return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));

                currentInstructions.Add(new SetIndexInstruction(
                    arrName,
                    indexExpr,
                    valParts,
                    lineNumber,
                    TryCompileExpression(new List<string>(1) { indexExpr }),
                    TryCompileExpression(valParts)));
                return (InterpreterResult.Success, null);
            }
            else if (SyntaxValidator.IsValidIdentifier(leftExpr) && !LanguageNames.Contains(leftExpr))
            {
                string name = leftExpr;
                string afterEq = parts[0][(eqPos + 1)..].Trim();
                List<string> valParts = new();
                if (!string.IsNullOrEmpty(afterEq))
                    valParts.Add(afterEq);
                if (parts.Count > 1)
                    valParts.AddRange(parts.GetRange(1, parts.Count - 1));

                if (valParts.Count == 0)
                    return (InterpreterResult.InvalidSyntax, string.Join(" ", parts));

                string valueText = string.Join(" ", valParts);
                currentInstructions.Add(new SetInstruction(
                    name,
                    valParts,
                    valueText,
                    lineNumber,
                    requirePreexisting: true,
                    TryCompileSetValue(valParts, valueText)));
                return (InterpreterResult.Success, null);
            }
        }

        // Direct function call: <function>(...)
        string fullLine = string.Join(" ", parts);
        int openParen = fullLine.IndexOf('(');
        if (openParen > 0 && fullLine.EndsWith(')'))
        {
            currentInstructions.Add(new CallInstruction(parts, lineNumber, TryCompileExpression(parts)));
            return (InterpreterResult.Success, null);
        }

        return (InterpreterResult.UnknownCommand, parts[0]);
    }

    private static CompiledExpression? TryCompileExpression(List<string>? parts)
    {
        if (parts == null || parts.Count == 0)
            return null;

        return ExpressionCompiler.TryCompile(parts, out CompiledExpression compiled)
            ? compiled
            : null;
    }

    private static CompiledExpression? TryCompileSetValue(List<string> parts, string valueText)
    {
        if (parts.Count == 0)
            return null;

        if (parts.Count == 1 && !InstructionHelper.HasExpressionSymbols(valueText, parts))
            return null;

        return TryCompileExpression(parts);
    }
}
