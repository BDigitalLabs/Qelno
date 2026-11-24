namespace Qelno.Core;
public enum InterpreterResult
{
    Success,
    UnknownCommand,
    InvalidSyntax,
    InvalidValue,
    InvalidName,
    VariableNotFound,
    InputFailed,
    PartsAreEmptyOrNull,
    MissingVariableName,
    DivisionByZero,
    FileError,
    FileAccessDenied,
    FunctionNotFound,
    InvalidParameterCount,
    CallDepthExceeded
}
