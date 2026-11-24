namespace Qelno.CLI;

using Qelno.Core;
using Qelno.Runtime;

public static class ErrorPrinter
{
    public static void Print(
        InterpreterResult result,
        string? subject = null,
        int? lineNumber = null,
        string? details = null)
    {
        if (result == InterpreterResult.Success)
            return;

        string message = result switch
        {
            InterpreterResult.UnknownCommand =>
                !string.IsNullOrEmpty(subject) ? $"Unknown command: '{subject}'" : "Unknown command.",

            InterpreterResult.InvalidSyntax =>
                FormatSubjectAndDetails("Invalid syntax", subject, details),

            InterpreterResult.InvalidValue =>
                FormatSubjectAndDetails("Invalid value", subject, details),

            InterpreterResult.InvalidName =>
                !string.IsNullOrEmpty(subject) ? $"Invalid name: '{subject}'" : "Invalid name.",

            InterpreterResult.VariableNotFound =>
                !string.IsNullOrEmpty(subject) ? $"Variable not found: '{subject}'" : "Variable not found.",

            InterpreterResult.InputFailed =>
                !string.IsNullOrEmpty(details) ? $"Input failed: {details}" :
                (!string.IsNullOrEmpty(subject) ? $"Input failed: '{subject}'" : "Input failed."),

            InterpreterResult.PartsAreEmptyOrNull =>
                "Parts are empty or null.",

            InterpreterResult.MissingVariableName =>
                !string.IsNullOrEmpty(subject) ? $"Missing variable name: '{subject}'" : "Missing variable name.",

            InterpreterResult.DivisionByZero =>
                !string.IsNullOrEmpty(subject) ? $"The divisor cannot be zero: {subject}" : "The divisor cannot be zero.",

            InterpreterResult.FileError =>
                !string.IsNullOrEmpty(subject) ? $"File error: {subject}" : "File error.",

            InterpreterResult.FileAccessDenied =>
                "Access to the file was denied.",

            InterpreterResult.FunctionNotFound =>
                !string.IsNullOrEmpty(subject) ? $"Function not found: '{subject}'" : "Function not found.",

            InterpreterResult.InvalidParameterCount =>
                !string.IsNullOrEmpty(subject) ? $"Invalid parameter count for function: '{subject}'" : "Invalid parameter count.",

            InterpreterResult.CallDepthExceeded =>
                !string.IsNullOrEmpty(subject)
                    ? $"Maximum call depth ({ExecutionContext.MaxCallDepth}) exceeded while calling '{subject}'."
                    : $"Maximum call depth ({ExecutionContext.MaxCallDepth}) exceeded.",

            _ => "An unknown error occurred."
        };

        Console.ForegroundColor = ConsoleColor.Red;

        string prefix = lineNumber.HasValue ? $"Error [Line {lineNumber.Value}]" : "Error";
        Console.Error.WriteLine($"{prefix} | {message}");

        Console.ResetColor();
    }

    private static string FormatSubjectAndDetails(string title, string? subject, string? details)
    {
        if (!string.IsNullOrEmpty(subject) && !string.IsNullOrEmpty(details))
            return $"{title}: '{subject}' ({details})";

        if (!string.IsNullOrEmpty(subject))
            return $"{title}: '{subject}'";

        if (!string.IsNullOrEmpty(details))
            return $"{title}: {details}";

        return $"{title}.";
    }
}
