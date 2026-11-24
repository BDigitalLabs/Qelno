namespace Qelno.Core;

using System.Globalization;
using Qelno.Compilation;
using Qelno.Instructions;

public class Variables
{
    private readonly Dictionary<string, Value> variables =
        new Dictionary<string, Value>(StringComparer.Ordinal);

    private readonly Stack<Dictionary<string, Value>> scopes = new();

    public void PushScope(Dictionary<string, Value>? initialScope = null)
    {
        scopes.Push(initialScope ?? new Dictionary<string, Value>(StringComparer.Ordinal));
    }

    public void PopScope()
    {
        if (scopes.Count > 0)
            scopes.Pop();
    }

    public bool IsNameAvailable(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || LanguageNames.Contains(name)) return false;
        if (!SyntaxValidator.IsValidIdentifier(name)) return false;
        return true;
    }

    public bool ContainsVariable(string name)
    {
        if (scopes.Count > 0 && scopes.Peek().ContainsKey(name))
            return true;

        return variables.ContainsKey(name);
    }

    public void DeleteVariable(string name)
    {
        if (scopes.Count > 0 && scopes.Peek().ContainsKey(name))
        {
            scopes.Peek().Remove(name);
            return;
        }

        if (!variables.Remove(name))
            throw new ArgumentException("Variable not found!");
    }

    public void SetVariable(string name, Value value, bool requirePreexisting = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Invalid variable name!");

        if (scopes.Count > 0)
        {
            if (requirePreexisting && !scopes.Peek().ContainsKey(name) && variables.ContainsKey(name))
            {
                variables[name] = value;
                return;
            }

            scopes.Peek()[name] = value;
        }
        else
        {
            variables[name] = value;
        }
    }

    // Parses the text and stores it in the appropriate type.
    public void SetVariableFromText(string name, string text, bool requirePreexisting = false)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        // Quoted string literal.
        if (InstructionHelper.IsValidStringLiteral(text))
        {
            SetVariable(name, Value.FromString(InstructionHelper.Unescape(text[1..^1])), requirePreexisting);
            return;
        }

        if (bool.TryParse(text, out bool boolResult))
        {
            SetVariable(name, Value.FromBool(boolResult), requirePreexisting);
        }
        else if (long.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long longResult))
        {
            SetVariable(name, Value.FromInteger(longResult), requirePreexisting);
        }
        else if (double.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double doubleResult))
        {
            SetVariable(name, Value.FromFloat(doubleResult), requirePreexisting);
        }
        else
        {
            SetVariable(name, Value.FromString(text), requirePreexisting);
        }
    }

    public bool TryGetVariable(string name, out Value value)
    {
        if (scopes.Count > 0 && scopes.Peek().TryGetValue(name, out value))
            return true;

        return variables.TryGetValue(name, out value);
    }
}
