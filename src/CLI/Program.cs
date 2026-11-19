using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    private readonly Parser parser = new Parser();

    public Parser Parser => parser;

    private (InterpreterResult interpreterResult, string? subject, int? lineNumber)
    Validate(IEnumerable<string> lines)
    {
        return SyntaxValidator.Validate(lines);
    }

    static void Main(string[] args)
    {
        Program interpreter = new Program();

        // Version info
        if (args.Length > 0 && args[0] == "--version")
        {
            Console.WriteLine("Qelno v0.5");
            return;
        }

        // File mode
        if (args.Length > 0)
        {
            try
            {
                string[] lines = File.ReadAllLines(args[0]);

                var validationResult = interpreter.Validate(lines);
                if (validationResult.interpreterResult != InterpreterResult.Success)
                {
                    ErrorPrinter.Print(validationResult.interpreterResult, validationResult.subject, validationResult.lineNumber);
                    return;
                }

                var compileResult = interpreter.parser.Compile(lines);
                if (compileResult.interpreterResult != InterpreterResult.Success)
                {
                    ErrorPrinter.Print(compileResult.interpreterResult, compileResult.subject, compileResult.lineNumber);
                    return;
                }

                var runResult = interpreter.parser.Execute(compileResult.instructions!);
                if (runResult.interpreterResult != InterpreterResult.Success)
                {
                    ErrorPrinter.Print(runResult.interpreterResult, runResult.subject, runResult.lineNumber);
                }
            }
            catch (IOException ex)
            {
                ErrorPrinter.Print(InterpreterResult.FileError, subject: ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                ErrorPrinter.Print(InterpreterResult.FileAccessDenied);
            }

            return;
        }

        // REPL mode
        Console.WriteLine("Hello, write some code!");
        List<string> replBuffer = new List<string>();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(replBuffer.Count > 0 ? "... " : ">>> ");
            Console.ForegroundColor = ConsoleColor.White;

            string? line = Console.ReadLine();

            if (line == null || (replBuffer.Count == 0 && line.Trim() == "exit"))
                break;

            if (string.IsNullOrWhiteSpace(line) && replBuffer.Count == 0)
                continue;

            replBuffer.Add(line);

            int openBlocks = SyntaxValidator.GetOpenBlockCount(replBuffer);
            if (openBlocks > 0)
                continue;

            var validationResult = interpreter.Validate(replBuffer);
            if (validationResult.interpreterResult != InterpreterResult.Success)
            {
                ErrorPrinter.Print(validationResult.interpreterResult, validationResult.subject);
                replBuffer.Clear();
                continue;
            }

            var compileResult = interpreter.parser.Compile(replBuffer);
            if (compileResult.interpreterResult != InterpreterResult.Success)
            {
                ErrorPrinter.Print(compileResult.interpreterResult, compileResult.subject);
                replBuffer.Clear();
                continue;
            }

            var runResult = interpreter.parser.Execute(compileResult.instructions!);
            if (runResult.interpreterResult != InterpreterResult.Success)
            {
                ErrorPrinter.Print(runResult.interpreterResult, runResult.subject);
            }

            if (interpreter.parser.Context.Terminated)
                break;

            replBuffer.Clear();
        }
    }
}
