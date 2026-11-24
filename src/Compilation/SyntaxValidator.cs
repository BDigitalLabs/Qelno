namespace Qelno.Compilation;

using Qelno.Core;
using Qelno.Instructions;
using Qelno.Runtime;
public static class SyntaxValidator
{
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
        if (!IsValidIdentifier(funcName) || LanguageNames.Contains(funcName) || FunctionRegistry.IsBuiltinFunction(funcName))
            return false;

        string paramStr = rest.Substring(openParen + 1, rest.Length - openParen - 2).Trim();
        if (paramStr.Length == 0)
            return true;

        string[] paramParts = paramStr.Split(',');
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string rawParam in paramParts)
        {
            string p = rawParam.Trim();
            if (string.IsNullOrEmpty(p) || !IsValidIdentifier(p) || LanguageNames.Contains(p) || FunctionRegistry.IsBuiltinFunction(p) || !seen.Add(p))
                return false;

            parameters.Add(p);
        }

        return true;
    }

    public static int GetLineBlockDelta(string rawLine)
    {
        string line = InstructionHelper.StripComment(rawLine);
        List<string> parts = InstructionHelper.SplitLinePreservingQuotes(line);

        if (parts.Count == 0 || parts[0].StartsWith(LanguageNames.OneLineComment))
            return 0;

        if (parts[0] == LanguageNames.If || parts[0] == LanguageNames.While || parts[0] == LanguageNames.For || parts[0] == LanguageNames.Def)
            return 1;

        if (parts[0] == LanguageNames.End)
            return -1;

        return 0;
    }

}
