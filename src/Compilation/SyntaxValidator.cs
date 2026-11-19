using System;
using System.Collections.Generic;

public static class SyntaxValidator
{
    private class ValidationBlock
    {
        public string BlockType { get; set; } = "";
        public bool ElseSeen { get; set; }
        public int LineNumber { get; set; }
    }

    public static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        for (int i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                return false;
        }

        return true;
    }

    public static bool TryParseDefHeader(string rawLine, out string funcName, out List<string> parameters)
    {
        funcName = string.Empty;
        parameters = new List<string>();

        string trimmed = rawLine.Trim();
        if (!trimmed.StartsWith(LanguageNames.Def, StringComparison.Ordinal))
            return false;

        string rest = trimmed[LanguageNames.Def.Length..].Trim();
        int openParen = rest.IndexOf('(');
        if (openParen <= 0 || !rest.EndsWith(')'))
            return false;

        funcName = rest[..openParen].Trim();
        if (!IsValidIdentifier(funcName) || LanguageNames.Contains(funcName))
            return false;

        string paramStr = rest.Substring(openParen + 1, rest.Length - openParen - 2).Trim();
        if (paramStr.Length == 0)
            return true;

        string[] paramParts = paramStr.Split(',');
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string rawParam in paramParts)
        {
            string p = rawParam.Trim();
            if (string.IsNullOrEmpty(p) || !IsValidIdentifier(p) || LanguageNames.Contains(p) || !seen.Add(p))
                return false;

            parameters.Add(p);
        }

        return true;
    }

    public static (InterpreterResult interpreterResult, string? subject,
    int? lineNumber) Validate(IEnumerable<string> lines)
    {
        Stack<ValidationBlock> blockStack = new();
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

            if (parts.Count == 0 ||
            parts[0].StartsWith(LanguageNames.OneLineComment))
                continue;

            switch (parts[0])
            {
                case LanguageNames.Def:
                    if (!TryParseDefHeader(rawLine, out _, out _))
                        return (InterpreterResult.InvalidSyntax,
                        LanguageNames.Def, lineNumber);

                    blockStack.Push(new ValidationBlock
                    {
                        BlockType = LanguageNames.Def,
                        LineNumber = lineNumber
                    });
                    break;

                case LanguageNames.Return:
                    bool inDef = false;
                    foreach (var b in blockStack)
                    {
                        if (b.BlockType == LanguageNames.Def)
                        {
                            inDef = true;
                            break;
                        }
                    }
                    if (!inDef)
                        return (InterpreterResult.InvalidSyntax,
                        LanguageNames.Return, lineNumber);
                    break;

                case LanguageNames.Break:
                case LanguageNames.Continue:
                    if (parts.Count != 1)
                        return (InterpreterResult.InvalidSyntax, parts[0], lineNumber);

                    bool inLoop = false;
                    foreach (var b in blockStack)
                    {
                        if (b.BlockType == LanguageNames.While || b.BlockType == LanguageNames.For)
                        {
                            inLoop = true;
                            break;
                        }
                        if (b.BlockType == LanguageNames.Def)
                        {
                            break;
                        }
                    }
                    if (!inLoop)
                        return (InterpreterResult.InvalidSyntax, parts[0], lineNumber);
                    break;

                case LanguageNames.If:
                    if (parts.Count < 2)
                        return (InterpreterResult.InvalidSyntax,
                        LanguageNames.If, lineNumber);

                    blockStack.Push(new ValidationBlock
                    {
                        BlockType = LanguageNames.If,
                        LineNumber = lineNumber
                    });
                    break;

                case LanguageNames.While:
                    if (parts.Count < 2)
                        return (InterpreterResult.InvalidSyntax,
                        LanguageNames.While, lineNumber);

                    blockStack.Push(new ValidationBlock
                    {
                        BlockType = LanguageNames.While,
                        LineNumber = lineNumber
                    });
                    break;

                case LanguageNames.For:
                    int toIndex = parts.IndexOf(LanguageNames.To);
                    if (parts.Count < 6 || parts[2] != "=" || toIndex <= 3 || toIndex >= parts.Count - 1)
                        return (InterpreterResult.InvalidSyntax,
                        LanguageNames.For, lineNumber);

                    blockStack.Push(new ValidationBlock
                    {
                        BlockType = LanguageNames.For,
                        LineNumber = lineNumber
                    });
                    break;

                case LanguageNames.Elif:
                    if (blockStack.Count == 0 ||
                        blockStack.Peek().BlockType != LanguageNames.If ||
                        blockStack.Peek().ElseSeen ||
                        parts.Count < 2)
                        return (InterpreterResult.InvalidSyntax,
                        LanguageNames.Elif, lineNumber);
                    break;

                case LanguageNames.Else:
                    if (blockStack.Count == 0 ||
                        blockStack.Peek().BlockType != LanguageNames.If ||
                        blockStack.Peek().ElseSeen ||
                        parts.Count != 1)
                        return (InterpreterResult.InvalidSyntax,
                        LanguageNames.Else, lineNumber);

                    blockStack.Peek().ElseSeen = true;
                    break;

                case LanguageNames.End:
                    if (blockStack.Count == 0 || parts.Count != 1)
                        return (InterpreterResult.InvalidSyntax,
                        LanguageNames.End, lineNumber);

                    blockStack.Pop();
                    break;
            }
        }

        if (blockStack.Count > 0)
        {
            return (InterpreterResult.InvalidSyntax, LanguageNames.End,
            blockStack.Peek().LineNumber);
        }

        return (InterpreterResult.Success, null, null);
    }

    public static int GetOpenBlockCount(IEnumerable<string> lines)
    {
        int count = 0;
        foreach (string rawLine in lines)
        {
            List<string> parts = new List<string>(
                rawLine.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries
                )
            );

            if (parts.Count == 0 || parts[0].StartsWith(LanguageNames.OneLineComment))
                continue;

            if (parts[0] == LanguageNames.If || parts[0] == LanguageNames.While || parts[0] == LanguageNames.For || parts[0] == LanguageNames.Def)
            {
                count++;
            }
            else if (parts[0] == LanguageNames.End && count > 0)
            {
                count--;
            }
        }

        return count;
    }
}
