# Qelno

An interpreted programming language written in C# (.NET 10).

> **Status:** v0.6 — early, experimental release. Syntax and behavior may change.

## Features

- Variables, arithmetic, booleans, strings, and arrays
- `if` / `elif` / `else`, `while`, `for`, `break`, and `continue`
- Functions with parameters and return values
- Built-in math, string, type, and array functions
- Console input/output (`Cin()`, `Cout()`) and an `Exit()` function
- Interactive REPL and file execution

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/)

## Build

```bash
dotnet build
```

## Run

Start the REPL:

```bash
dotnet run
```

Run a script:

```bash
dotnet run -- path/to/script.txt
```

Check the version:

```bash
dotnet run -- --version
```

Any text file can be used as a script. In the REPL, type `exit` on an empty line to quit.

## Example

```text
set name = "World"
Cout("Hello, " + name)

set total = 0
for i = 1 to 5
    total = total + i
end
Cout(total)

def square(x)
    return x * x
end
Cout(square(5))
```

## Tests

```bash
python3 tests/run_tests.py
```

## License

Licensed under the [Apache License, Version 2.0](LICENSE.txt).

Copyright 2026 Hüseyin Berke. See [NOTICE.txt](NOTICE.txt) for attribution notices.
