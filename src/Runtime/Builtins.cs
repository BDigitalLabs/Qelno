using System;
using System.Collections.Generic;
using System.Globalization;

public class Builtins
{
    private readonly Variables variables;

    public Builtins(Variables variables)
    {
        this.variables = variables;
    }

    public (InterpreterResult interpreterResult, string? subject, string? value) ReadInput()
    {
        Console.Write("Input: ");
        string? line = Console.ReadLine();

        if (line == null)
            return (InterpreterResult.InputFailed, "Input failed!", null);

        return (InterpreterResult.Success, null, line);
    }

    public (InterpreterResult interpreterResult, string? subject) Cin(string variableName)
    {
        var (cinResult, subject, line) = ReadInput();

        if (cinResult != InterpreterResult.Success)
            return (cinResult, subject);

        variables.SetVariable(variableName, line!);
        return (InterpreterResult.Success, null);
    }

    public static string FormatValue(object? val)
    {
        if (val is null) return "null";
        if (val is bool b) return b ? "true" : "false";
        if (val is string s) return s;
        if (val is double d) return d.ToString(CultureInfo.InvariantCulture);
        if (val is List<object> list)
        {
            var formattedElements = new List<string>(list.Count);
            foreach (var elem in list)
            {
                if (elem is string str)
                    formattedElements.Add($"\"{str}\"");
                else
                    formattedElements.Add(FormatValue(elem));
            }
            return $"[{string.Join(", ", formattedElements)}]";
        }
        if (val is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
        return val.ToString() ?? string.Empty;
    }

    public void Cout(object value)
    {
        if (value is List<object> list)
            Console.WriteLine(FormatValue(list));
        else if (value is bool b)
            Console.WriteLine(b ? "true" : "false");
        else if (value is IFormattable formattable)
            Console.WriteLine(formattable.ToString(null, CultureInfo.InvariantCulture));
        else
            Console.WriteLine(value);
    }
}
