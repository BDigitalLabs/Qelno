
public static class ExpressionConverter
{
    
    public static bool IsExpressionOperator(string token)
    {
        return token is
            "and" or "not" or "or" or
            "(" or ")" or
            "u+" or "u-" or
            "**" or "*" or "/" or "%" or "+" or "-" or
            ">" or "<" or "==" or "!=" or ">=" or "<=";
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

    public static bool TryConvertToPostfix(
        List<string> tokens,
        out List<string> postfix)
    {
        postfix = new();
        Stack<string> operators = new();

        foreach (string token in tokens)
        {
            // Numbers and resolved variables are added directly to the output.
            if (!IsExpressionOperator(token))
            {
                postfix.Add(token);
                continue;
            }

            if (token == "(")
            {
                operators.Push(token);
                continue;
            }

            if (token == ")")
            {
                while (operators.Count > 0 &&
                       operators.Peek() != "(")
                {
                    postfix.Add(operators.Pop());
                }

                if (operators.Count == 0)
                    return false;

                operators.Pop(); // Discard "(".
                continue;
            }

            // Since unary operators precede the operand,
            // they are pushed directly onto the operator stack.
            if (token is "u+" or "u-" or "not")
            {
                operators.Push(token);
                continue;
            }

            // Take precedence into account for binary operators.
            while (operators.Count > 0 &&
                   operators.Peek() != "(")
            {
                string top = operators.Peek();

                int topPrecedence = GetPrecedence(top);
                int currentPrecedence = GetPrecedence(token);

                bool shouldPop =
                    topPrecedence > currentPrecedence ||
                    (topPrecedence == currentPrecedence &&
                     token != "**");

                if (!shouldPop)
                    break;

                postfix.Add(operators.Pop());
            }

            operators.Push(token);
        }

        while (operators.Count > 0)
        {
            string op = operators.Pop();

            if (op == "(" || op == ")")
                return false;

            postfix.Add(op);
        }

        return true;
    }
}
