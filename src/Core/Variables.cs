using System;
using System.Collections.Generic;
using System.Globalization;

public class Variables
{
    private readonly Dictionary<string, object> variables =
        new Dictionary<string, object>(StringComparer.Ordinal);

    private readonly Stack<Dictionary<string, object>> scopes = new();

    public void PushScope(Dictionary<string, object>? initialScope = null)
    {
        scopes.Push(initialScope ?? new Dictionary<string, object>(StringComparer.Ordinal));
    }

    public void PopScope()
    {
        if (scopes.Count > 0)
            scopes.Pop();
    }

    public bool IsNameAvailable(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || LanguageNames.Contains(name)) return false;
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

    // Stores the given value without changing its type.
    public void SetVariable(string name, object value, bool requirePreexisting = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Invalid variable name!");

        if (value == null)
            throw new ArgumentNullException(nameof(value));

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
        if (text.Length >= 2 &&
            ((text[0] == '"' && text[^1] == '"') ||
             (text[0] == '\'' && text[^1] == '\'')))
        {
            SetVariable(name, text[1..^1], requirePreexisting);
            return;
        }

        if (bool.TryParse(text, out bool boolResult))
        {
            SetVariable(name, boolResult, requirePreexisting);
        }
        else if (long.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long longResult))
        {
            SetVariable(name, longResult, requirePreexisting);
        }
        else if (double.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double doubleResult))
        {
            SetVariable(name, doubleResult, requirePreexisting);
        }
        else
        {
            SetVariable(name, text, requirePreexisting);
        }
    }

    public bool TryGetVariable(string name, out object? value)
    {
        if (scopes.Count > 0 && scopes.Peek().TryGetValue(name, out value))
            return true;

        return variables.TryGetValue(name, out value);
    }
}
