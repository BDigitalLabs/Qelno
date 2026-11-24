namespace Qelno.Core;
public static class LanguageNames
{
    public const string Set = "set";
    public const string And = "and";
    public const string Or = "or";

    public const string Not = "not";

    public const string False = "false";
    public const string True = "true";
    public const string If = "if";
    public const string While = "while";
    public const string For = "for";
    public const string To = "to";
    public const string Step = "step";
    public const string End = "end";
    public const string Elif = "elif";
    public const string Else = "else";
    public const string Def = "def";
    public const string Return = "return";
    public const string Break = "break";
    public const string Continue = "continue";
    public const string OneLineComment = "#";

    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        Set,
        Not,
        And,
        Or,
        OneLineComment,
        True,
        False,
        If,
        While,
        For,
        To,
        Step,
        End,
        Else,
        Elif,
        Def,
        Return,
        Break,
        Continue
    };

    public static bool Contains(string value)
        => Names.Contains(value);

}
