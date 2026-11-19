using System;
using System.Globalization;


public class NumericArgumentResolver
{
    private readonly Variables variables;
    private readonly Builtins builtins;

    public NumericArgumentResolver(
        Variables variables,
        Builtins builtins)
    {
        this.variables = variables;
        this.builtins = builtins;
    }

    public (InterpreterResult interpreterResult, string? subject) ResolveNumericArgument(
    string argument,
    out double number)
    {
        number = default;

        // Direct number
        if (double.TryParse(
            argument,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number))

            return (InterpreterResult.Success, null);


        // Read value from keyboard
        if (argument == LanguageNames.Cin)
        {
            var inputResult = ReadNumericInput(out number);
            return inputResult;
        }

        // Find variable
        if (!variables.TryGetVariable(
            argument,
            out object? variableValue))

            return (InterpreterResult.VariableNotFound, argument);


        // Convert variable value to number
        if (TryConvertNumericValue(variableValue, out number))

            return (InterpreterResult.Success, null);


        return (InterpreterResult.InvalidValue, argument);
    }
    private static bool TryConvertNumericValue(
        object? value,
        out double number)
    {
        switch (value)
        {
            case long integerValue:
                number = integerValue;
                return true;

            case double doubleValue:
                number = doubleValue;
                return true;

            case string text:
                return double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number);

            default:
                number = default;
                return false;
        }
    }
    private (InterpreterResult interpreterResult, string? subject) ReadNumericInput(
        out double number)
    {
        number = default;

        var (cinResult, subject, line) = builtins.ReadInput();
        if (cinResult != InterpreterResult.Success)
            return (cinResult, subject);

        if (TryConvertNumericValue(line, out number))
            return (InterpreterResult.Success, null);

        return (
            InterpreterResult.InvalidValue,
            line
        );
    }
}
