from pathlib import Path
import difflib
import subprocess
import sys


# Paths
TESTS_DIR = Path(__file__).resolve().parent
PROJECT_DIR = TESTS_DIR.parent
EXPECTED_DIR = TESTS_DIR / "expected"
DLL_PATH = PROJECT_DIR / "bin" / "Debug" / "net10.0" / "Qelno.dll"

# Maximum execution time for each test
TEST_TIMEOUT = 30


def normalize_output(output):
    """Normalize line endings without changing spaces."""
    output = output.replace("\r\n", "\n").replace("\r", "\n")
    return output.removesuffix("\n")


def show_diff(expected, actual, expected_name, actual_name):
    """Display differences between two outputs."""
    diff = difflib.unified_diff(
        expected.splitlines(keepends=True),
        actual.splitlines(keepends=True),
        fromfile=expected_name,
        tofile=actual_name
    )

    for line in diff:
        print(f"    {line}", end="")

    print()


def build_project():
    """Build the Qelno project once."""
    print("Building Qelno...\n")

    try:
        result = subprocess.run(
            ["dotnet", "build", "--nologo", "--no-restore"],
            cwd=PROJECT_DIR,
            capture_output=True,
            text=True,
            timeout=120
        )
    except (FileNotFoundError, subprocess.TimeoutExpired) as error:
        print(f"Build failed: {error}")
        return False

    if result.returncode != 0:
        print("Build failed!\n")
        print(result.stdout)
        print(result.stderr)
        return False

    print("Build successful!\n")
    return True


def run_test(test_file):
    """Run one test and compare its results."""

    relative_path = test_file.relative_to(PROJECT_DIR)
    expected_file = EXPECTED_DIR / test_file.relative_to(TESTS_DIR)

    # Optional expected stderr and exit code files
    expected_stderr_file = expected_file.with_suffix(".stderr")
    expected_exitcode_file = expected_file.with_suffix(".exitcode")
    stdin_file = test_file.with_suffix(".in")
    stdin_data = stdin_file.read_text() if stdin_file.exists() else None

    expected_stdout = normalize_output(expected_file.read_text())

    expected_stderr = ""
    if expected_stderr_file.exists():
        expected_stderr = normalize_output(
            expected_stderr_file.read_text()
        )

    expected_exitcode = 1 if expected_stderr_file.exists() else 0

    if expected_exitcode_file.exists():
        try:
            expected_exitcode = int(
                expected_exitcode_file.read_text().strip()
            )
        except ValueError:
            print(f"[FAIL] {relative_path}: Invalid expected exit code")
            return False

    cmd = ["dotnet", str(DLL_PATH), str(relative_path)] if DLL_PATH.exists() else [
        "dotnet", "run", "--no-build", "--", str(relative_path)
    ]

    try:
        result = subprocess.run(
            cmd,
            cwd=PROJECT_DIR,
            input=stdin_data,
            capture_output=True,
            text=True,
            timeout=TEST_TIMEOUT
        )

    except subprocess.TimeoutExpired:
        print(f"[FAIL] {relative_path}: Execution timed out")
        return False

    except FileNotFoundError as error:
        print(f"[FAIL] {relative_path}: {error}")
        return False

    actual_stdout = normalize_output(result.stdout)
    actual_stderr = normalize_output(result.stderr)

    passed = True

    # Compare standard output
    if actual_stdout != expected_stdout:
        passed = False

        print(f"\n    Standard output mismatch:")
        show_diff(
            expected_stdout,
            actual_stdout,
            "expected",
            "actual"
        )

    # Compare standard error
    if actual_stderr != expected_stderr:
        passed = False

        print(f"\n    Standard error mismatch:")
        show_diff(
            expected_stderr,
            actual_stderr,
            "expected stderr",
            "actual stderr"
        )

    # Compare exit codes
    if result.returncode != expected_exitcode:
        passed = False

        print(
            f"\n    Expected exit code: {expected_exitcode}\n"
            f"    Actual exit code:   {result.returncode}"
        )

    if passed:
        print(f"[PASS] {relative_path}")
    else:
        print(f"[FAIL] {relative_path}")

    return passed


def main():

    print("================================")
    print("    Qelno Test Runner")
    print("================================\n")

    # Find all test files, excluding the expected directory
    test_files = sorted(
        file for file in TESTS_DIR.rglob("*.txt")
        if EXPECTED_DIR not in file.parents
    )

    if not test_files:
        print("No test files found!")
        return 2

    # Separate configured tests from tests without expected output
    runnable_tests = []
    skipped_tests = []

    for test_file in test_files:
        expected_file = EXPECTED_DIR / test_file.relative_to(TESTS_DIR)

        if expected_file.exists():
            runnable_tests.append(test_file)
        else:
            skipped_tests.append(test_file)

    if not runnable_tests:
        print("No configured tests found!")
        return 2

    # Build the project before running tests (unless --no-build is specified)
    if "--no-build" not in sys.argv:
        if not build_project():
            return 2
    else:
        print("Skipping build (--no-build)...\n")

    passed = 0
    failed = 0

    print("Running tests...\n")

    for test_file in runnable_tests:

        if run_test(test_file):
            passed += 1
        else:
            failed += 1

    # Report skipped tests
    for test_file in skipped_tests:
        relative_path = test_file.relative_to(PROJECT_DIR)
        print(f"[SKIP] {relative_path} (no expected output)")

    # Final report
    total = len(test_files)
    skipped = len(skipped_tests)

    print("\n================================")
    print("           Test Summary")
    print("================================")

    print(f"Total:   {total}")
    print(f"Passed:  {passed}")
    print(f"Failed:  {failed}")
    print(f"Skipped: {skipped}")

    if failed == 0:
        print("\nAll configured tests passed!")
        return 0

    print("\nSome tests failed!")
    return 1


if __name__ == "__main__":
    sys.exit(main())
