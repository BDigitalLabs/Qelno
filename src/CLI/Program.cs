namespace Qelno.CLI;

using Qelno.Core;
using Qelno.Compilation;
using Qelno.Runtime;
﻿using System;
using System.IO;

class Program
{
    private readonly Parser parser = new Parser();

    public Parser Parser => parser;

    static void Main(string[] args)
    {
        Program interpreter = new Program();

        // Version info
        if (args.Length > 0 && args[0] == "--version")
        {
            Console.WriteLine("Qelno v0.6");
            return;
        }

        // File mode
        if (args.Length > 0)
        {
            try
            {
                string[] lines = File.ReadAllLines(args[0]);

                var compileResult = interpreter.parser.Compile(lines);
                if (compileResult.interpreterResult != InterpreterResult.Success)
                {
                    ErrorPrinter.Print(compileResult.interpreterResult, compileResult.subject, compileResult.lineNumber);
                    Environment.ExitCode = 1;
                    return;
                }

                var runResult = interpreter.parser.Execute(compileResult.instructions!);
                if (runResult.interpreterResult != InterpreterResult.Success)
                {
                    ErrorPrinter.Print(runResult.interpreterResult, runResult.subject, runResult.lineNumber);
                    Environment.ExitCode = 1;
                    return;
                }
            }
            catch (IOException ex)
            {
                ErrorPrinter.Print(InterpreterResult.FileError, subject: ex.Message);
                Environment.ExitCode = 1;
            }
            catch (UnauthorizedAccessException)
            {
                ErrorPrinter.Print(InterpreterResult.FileAccessDenied);
                Environment.ExitCode = 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Fatal Error | Unexpected runtime exception: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 1;
            }

            return;
        }

        // REPL mode
        Console.WriteLine("Hello, write some code!");
        List<string> replBuffer = new List<string>();
        int openBlocks = 0;

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
            openBlocks += SyntaxValidator.GetLineBlockDelta(line);
            if (openBlocks < 0)
                openBlocks = 0;

            if (openBlocks > 0)
                continue;

            try
            {
                var compileResult = interpreter.parser.Compile(replBuffer);
                if (compileResult.interpreterResult != InterpreterResult.Success)
                {
                    ErrorPrinter.Print(compileResult.interpreterResult, compileResult.subject);
                    replBuffer.Clear();
                    openBlocks = 0;
                    continue;
                }

                var runResult = interpreter.parser.Execute(compileResult.instructions!);
                if (runResult.interpreterResult != InterpreterResult.Success)
                {
                    ErrorPrinter.Print(runResult.interpreterResult, runResult.subject);
                }

                if (interpreter.parser.Context.Terminated)
                    break;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error | Unexpected exception: {ex.Message}");
                Console.ResetColor();
            }

            replBuffer.Clear();
            openBlocks = 0;
        }
    }
}
