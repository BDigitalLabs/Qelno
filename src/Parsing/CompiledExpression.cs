namespace Qelno.Parsing;

using Qelno.Core;

/// <summary>
/// RPN (Reverse Polish Notation) instruction types.
/// Expressions are converted into this opcode sequence at compile time.
/// </summary>
public enum RpnOpType : byte
{
    // Values
    Literal = 0,
    Variable,

    // Arithmetic Operators
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Power,
    Negate,
    UnaryPlus,

    // Comparison Operators
    Equal,
    NotEqual,
    LessThan,
    LessOrEqual,
    GreaterThan,
    GreaterOrEqual,

    // Logical Operators
    And,
    Or,
    Not,

    // Special Operations
    FunctionCall,
    IndexAccess,
    ArrayLiteral
}

/// <summary>
/// Logical node type for short-circuit (and / or) support.
/// </summary>
public enum LogicalOpType : byte
{
    None = 0,
    And,
    Or,
    Not
}

/// <summary>
/// A single RPN token of a compiled expression.
/// Carries literal values, variable names, and sub-expressions (function arguments,
/// array elements, and index expressions).
/// </summary>
public readonly struct RpnToken
{
    public RpnOpType Type { get; }
    public Value LiteralValue { get; }
    public string Name { get; }
    public string Text { get; }
    public int ArgumentCount { get; }
    public IReadOnlyList<CompiledExpression>? Arguments { get; }
    public CompiledExpression? Operand { get; }

    public RpnToken(
        RpnOpType type,
        Value literalValue = default,
        string name = "",
        string text = "",
        int argCount = 0,
        IReadOnlyList<CompiledExpression>? arguments = null,
        CompiledExpression? operand = null)
    {
        Type = type;
        LiteralValue = literalValue;
        Name = name;
        Text = text;
        ArgumentCount = argCount;
        Arguments = arguments;
        Operand = operand;
    }

    public static RpnToken Literal(Value val) =>
        new(RpnOpType.Literal, literalValue: val);

    public static RpnToken Variable(string varName) =>
        new(RpnOpType.Variable, name: varName);

    public static RpnToken Op(RpnOpType op) => new(op);

    public static RpnToken Function(
        string name,
        IReadOnlyList<CompiledExpression> arguments,
        string text) =>
        new(RpnOpType.FunctionCall, name: name, text: text, argCount: arguments.Count, arguments: arguments);

    public static RpnToken Index(string name, CompiledExpression index, string indexText) =>
        new(RpnOpType.IndexAccess, name: name, text: indexText, operand: index);

    public static RpnToken Array(IReadOnlyList<CompiledExpression> elements) =>
        new(RpnOpType.ArrayLiteral, argCount: elements.Count, arguments: elements);
}

/// <summary>
/// An expression produced at compile time.
/// Holds either a flat RPN token list or a logical node for short-circuit evaluation.
/// </summary>
public sealed class CompiledExpression
{
    public IReadOnlyList<RpnToken> Tokens { get; }
    public LogicalOpType LogicalOp { get; }
    public IReadOnlyList<CompiledExpression>? LogicalOperands { get; }
    public string DisplayText { get; }
    public bool AllowNullResult { get; }

    private CompiledExpression(
        IReadOnlyList<RpnToken> tokens,
        LogicalOpType logicalOp,
        IReadOnlyList<CompiledExpression>? logicalOperands,
        string displayText,
        bool allowNullResult)
    {
        Tokens = tokens;
        LogicalOp = logicalOp;
        LogicalOperands = logicalOperands;
        DisplayText = displayText;
        AllowNullResult = allowNullResult;
    }

    public bool IsLogical => LogicalOp != LogicalOpType.None;

    public static CompiledExpression Rpn(
        List<RpnToken> tokens,
        string displayText,
        bool allowNullResult = false) =>
        new(tokens, LogicalOpType.None, null, displayText, allowNullResult);

    public static CompiledExpression Logical(
        LogicalOpType op,
        List<CompiledExpression> operands,
        string displayText = "") =>
        new(Array.Empty<RpnToken>(), op, operands, displayText, false);
}
