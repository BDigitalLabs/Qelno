namespace Qelno.Parsing;

using Qelno.Core;
using Qelno.Instructions;
using System.Globalization;

/// <summary>
/// Converts raw infix expression parts into a <see cref="CompiledExpression"/> at compile time.
/// Compilation is purely structural; variable and function values are resolved at runtime.
/// Returns <c>false</c> on any syntax issue, allowing the caller to fall back to the existing
/// legacy runtime evaluator so that error messages and behavior are preserved exactly.
/// </summary>
public static class ExpressionCompiler
{
    public static bool TryCompile(List<string>? parts, out CompiledExpression expression)
    {
        expression = null!;
        if (parts == null || parts.Count == 0)
            return false;

        try
        {
            return TryCompileParts(parts, out expression);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCompileParts(List<string> parts, out CompiledExpression expression)
    {
        expression = null!;

        string source = string.Join(" ", parts).Trim();
        if (source.Length == 0)
            return false;

        // Array literal?
        if (ExpressionParser.IsSingleArrayLiteral(source))
            return TryCompileArrayLiteral(source, out expression);

        // Single or double quoted string literal?
        if (InstructionHelper.IsValidStringLiteral(source))
        {
            expression = CompiledExpression.Rpn(
                new List<RpnToken>(1)
                {
                    RpnToken.Literal(Value.FromString(InstructionHelper.Unescape(source[1..^1])))
                },
                source);
            return true;
        }

        // Single array index access? (e.g. arr[0], arr[i + 1])
        if (ExpressionParser.IsSingleIndexAccess(source))
            return TryCompileIndexAccess(source, out expression);

        // Single function call? (void function result may be null here)
        if (ExpressionParser.IsSingleFunctionCall(source))
        {
            if (!TryBuildFunctionToken(source, out RpnToken token))
                return false;

            expression = CompiledExpression.Rpn(
                new List<RpnToken>(1) { token },
                source,
                allowNullResult: true);
            return true;
        }

        var tokenizeResult = ExpressionTokenizer.TokenizeExpression(source, out List<string> tokens);
        if (tokenizeResult.interpreterResult != InterpreterResult.Success)
            return false;

        return TryCompileTokens(tokens, out expression);
    }

    /// <summary>
    /// Compiles the token list while preserving the exact short-circuit behavior of the existing interpreter.
    /// </summary>
    private static bool TryCompileTokens(List<string> tokens, out CompiledExpression expression)
    {
        expression = null!;
        if (tokens.Count == 0)
            return false;

        // 1. Outermost "or"
        List<int> orIndices = FindTopLevel(tokens, LanguageNames.Or);
        if (orIndices.Count > 0)
            return TryCompileLogicalChain(tokens, orIndices, LogicalOpType.Or, out expression);

        // 2. Outermost "and"
        List<int> andIndices = FindTopLevel(tokens, LanguageNames.And);
        if (andIndices.Count > 0)
            return TryCompileLogicalChain(tokens, andIndices, LogicalOpType.And, out expression);

        // 3. Strip redundant parentheses wrapping the entire expression
        if (tokens.Count >= 2 && tokens[0] == "(" && tokens[^1] == ")")
        {
            int parenDepth = 0;
            bool wrapsAll = true;

            for (int i = 0; i < tokens.Count - 1; i++)
            {
                if (tokens[i] == "(")
                    parenDepth++;
                else if (tokens[i] == ")")
                    parenDepth--;

                if (parenDepth == 0)
                {
                    wrapsAll = false;
                    break;
                }
            }

            if (wrapsAll)
                return TryCompileTokens(tokens.GetRange(1, tokens.Count - 2), out expression);
        }

        // 4. Leading "not" at outermost level
        if (tokens.Count > 1 && tokens[0] == LanguageNames.Not)
        {
            if (!TryCompileTokens(tokens.GetRange(1, tokens.Count - 1), out CompiledExpression inner))
                return false;

            expression = CompiledExpression.Logical(
                LogicalOpType.Not,
                new List<CompiledExpression>(1) { inner },
                string.Join(" ", tokens));
            return true;
        }

        // 5. Base case: produce RPN via Shunting-Yard
        return TryCompileBase(tokens, out expression);
    }

    private static List<int> FindTopLevel(List<string> tokens, string op)
    {
        List<int> indices = new();
        int depth = 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            string t = tokens[i];

            if (t == "(")
                depth++;
            else if (t == ")")
                depth--;
            else if (depth == 0 && t == op)
                indices.Add(i);
        }

        return indices;
    }

    private static bool TryCompileLogicalChain(
        List<string> tokens,
        List<int> separatorIndices,
        LogicalOpType logicalOp,
        out CompiledExpression expression)
    {
        expression = null!;
        List<CompiledExpression> operands = new(separatorIndices.Count + 1);
        int start = 0;

        foreach (int separatorIndex in separatorIndices)
        {
            if (separatorIndex == start)
                return false;

            if (!TryCompileTokens(tokens.GetRange(start, separatorIndex - start), out CompiledExpression operand))
                return false;

            operands.Add(operand);
            start = separatorIndex + 1;
        }

        if (start >= tokens.Count)
            return false;

        if (!TryCompileTokens(tokens.GetRange(start, tokens.Count - start), out CompiledExpression last))
            return false;

        operands.Add(last);

        expression = CompiledExpression.Logical(logicalOp, operands, string.Join(" ", tokens));
        return true;
    }

    private static bool TryCompileBase(List<string> tokens, out CompiledExpression expression)
    {
        expression = null!;
        List<RpnToken> output = new(tokens.Count);
        Stack<string> operators = new();

        foreach (string token in tokens)
        {
            if (!ExpressionConverter.IsExpressionOperator(token))
            {
                if (!TryCompileLeaf(token, out RpnToken leaf))
                    return false;

                output.Add(leaf);
                continue;
            }

            if (token == "(")
            {
                operators.Push(token);
                continue;
            }

            if (token == ")")
            {
                while (operators.Count > 0 && operators.Peek() != "(")
                    output.Add(CreateOperatorToken(operators.Pop()));

                if (operators.Count == 0)
                    return false;

                operators.Pop();
                continue;
            }

            if (token is "u+" or "u-" or "not")
            {
                operators.Push(token);
                continue;
            }

            while (operators.Count > 0 && operators.Peek() != "(")
            {
                string top = operators.Peek();

                int topPrecedence = GetPrecedence(top);
                int currentPrecedence = GetPrecedence(token);

                bool shouldPop =
                    topPrecedence > currentPrecedence ||
                    (topPrecedence == currentPrecedence && token != "**");

                if (!shouldPop)
                    break;

                output.Add(CreateOperatorToken(operators.Pop()));
            }

            operators.Push(token);
        }

        while (operators.Count > 0)
        {
            string op = operators.Pop();
            if (op == "(" || op == ")")
                return false;

            output.Add(CreateOperatorToken(op));
        }

        if (output.Count == 0)
            return false;

        expression = CompiledExpression.Rpn(output, string.Join(" ", tokens));
        return true;
    }

    private static bool TryCompileLeaf(string token, out RpnToken leaf)
    {
        leaf = default;

        // Array literal?
        if (ExpressionParser.IsSingleArrayLiteral(token))
        {
            if (!TryCompileArrayLiteral(token, out CompiledExpression arrayExpr))
                return false;

            leaf = arrayExpr.Tokens[0];
            return true;
        }

        // String literal?
        if (InstructionHelper.IsValidStringLiteral(token))
        {
            leaf = RpnToken.Literal(Value.FromString(InstructionHelper.Unescape(token[1..^1])));
            return true;
        }

        // Boolean literal?
        if (token == LanguageNames.True)
        {
            leaf = RpnToken.Literal(Value.True);
            return true;
        }

        if (token == LanguageNames.False)
        {
            leaf = RpnToken.Literal(Value.False);
            return true;
        }

        // Function call?
        int openParen = token.IndexOf('(');
        if (openParen > 0 && token.EndsWith(')'))
        {
            if (!TryBuildFunctionToken(token, out leaf))
                return false;

            return true;
        }

        // Array index access?
        if (ExpressionParser.IsSingleIndexAccess(token))
        {
            if (!TryBuildIndexToken(token, out leaf))
                return false;

            return true;
        }

        // Integer literal?
        if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intNumber))
        {
            leaf = RpnToken.Literal(Value.FromInteger(intNumber));
            return true;
        }

        // Decimal literal?
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatNumber))
        {
            leaf = RpnToken.Literal(Value.FromFloat(floatNumber));
            return true;
        }

        // Variable
        leaf = RpnToken.Variable(token);
        return true;
    }

    private static bool TryBuildFunctionToken(string callText, out RpnToken token)
    {
        token = default;

        string trimmed = callText.Trim();
        int openParen = trimmed.IndexOf('(');
        if (openParen <= 0 || !trimmed.EndsWith(')'))
            return false;

        string funcName = trimmed[..openParen].Trim();
        string argsContent = trimmed.Substring(openParen + 1, trimmed.Length - openParen - 2).Trim();

        List<CompiledExpression> arguments = new();

        if (argsContent.Length > 0)
        {
            List<string> rawArgs = ExpressionParser.SplitArguments(argsContent);
            foreach (string rawArg in rawArgs)
            {
                string arg = rawArg.Trim();
                if (arg.Length == 0)
                    return false;

                if (!TryCompileParts(new List<string>(1) { arg }, out CompiledExpression argExpr))
                    return false;

                arguments.Add(argExpr);
            }
        }

        token = RpnToken.Function(funcName, arguments, trimmed);
        return true;
    }

    private static bool TryBuildIndexToken(string accessText, out RpnToken token)
    {
        token = default;

        string trimmed = accessText.Trim();
        int openBracket = trimmed.IndexOf('[');
        if (openBracket <= 0 || !trimmed.EndsWith(']'))
            return false;

        string varName = trimmed[..openBracket].Trim();
        string indexExpr = trimmed.Substring(openBracket + 1, trimmed.Length - openBracket - 2).Trim();

        if (indexExpr.Length == 0)
            return false;

        if (!TryCompileParts(new List<string>(1) { indexExpr }, out CompiledExpression indexCompiled))
            return false;

        token = RpnToken.Index(varName, indexCompiled, indexExpr);
        return true;
    }

    private static bool TryCompileIndexAccess(string accessText, out CompiledExpression expression)
    {
        expression = null!;

        if (!TryBuildIndexToken(accessText, out RpnToken token))
            return false;

        expression = CompiledExpression.Rpn(
            new List<RpnToken>(1) { token },
            accessText.Trim());

        return true;
    }

    private static bool TryCompileArrayLiteral(string source, out CompiledExpression expression)
    {
        expression = null!;

        string trimmed = source.Trim();
        if (!ExpressionParser.IsSingleArrayLiteral(trimmed))
            return false;

        string inner = trimmed[1..^1].Trim();
        List<CompiledExpression> elements = new();

        if (inner.Length > 0)
        {
            List<string> rawItems = ExpressionParser.SplitArguments(inner);
            foreach (string rawItem in rawItems)
            {
                string item = rawItem.Trim();
                if (item.Length == 0)
                    return false;

                if (!TryCompileParts(new List<string>(1) { item }, out CompiledExpression element))
                    return false;

                elements.Add(element);
            }
        }

        expression = CompiledExpression.Rpn(
            new List<RpnToken>(1) { RpnToken.Array(elements) },
            trimmed);

        return true;
    }

    private static RpnToken CreateOperatorToken(string op)
    {
        return op switch
        {
            "+" => RpnToken.Op(RpnOpType.Add),
            "-" => RpnToken.Op(RpnOpType.Subtract),
            "*" => RpnToken.Op(RpnOpType.Multiply),
            "/" => RpnToken.Op(RpnOpType.Divide),
            "%" => RpnToken.Op(RpnOpType.Modulo),
            "**" => RpnToken.Op(RpnOpType.Power),
            "u+" => RpnToken.Op(RpnOpType.UnaryPlus),
            "u-" => RpnToken.Op(RpnOpType.Negate),
            "not" => RpnToken.Op(RpnOpType.Not),
            "and" => RpnToken.Op(RpnOpType.And),
            "or" => RpnToken.Op(RpnOpType.Or),
            "==" => RpnToken.Op(RpnOpType.Equal),
            "!=" => RpnToken.Op(RpnOpType.NotEqual),
            "<" => RpnToken.Op(RpnOpType.LessThan),
            "<=" => RpnToken.Op(RpnOpType.LessOrEqual),
            ">" => RpnToken.Op(RpnOpType.GreaterThan),
            ">=" => RpnToken.Op(RpnOpType.GreaterOrEqual),
            _ => throw new InvalidOperationException($"Unknown operator: {op}")
        };
    }

    private static int GetPrecedence(string op)
    {
        return op switch
        {
            "or" => 0,
            "and" => 1,
            "not" => 2,
            ">" or "<" or ">=" or "<=" or "==" or "!=" => 3,
            "+" or "-" => 4,
            "*" or "/" or "%" => 5,
            "u+" or "u-" => 6,
            "**" => 7,
            _ => 0
        };
    }
}
