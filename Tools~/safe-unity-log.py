#!/usr/bin/env python3
"""Run Unity with combined, redacted output; never retain raw command output."""
import argparse
import os
from pathlib import Path
import re
import subprocess
import sys


SENSITIVE = re.compile(
    r"access[-_]?token|refresh[-_]?token|session[-_]?token|"
    r"authorization|password|client[-_]?secret|api[-_]?key|"
    r"service[-_]?account[-_]?secret|\bbearer\b", re.IGNORECASE)
JWT = re.compile(r"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b")


def sanitized(lines):
    pending_value = False
    for line in lines:
        if pending_value:
            pending_value = False
            yield "[REDACTED credential value]\n"
            continue
        match = SENSITIVE.search(line)
        if match:
            # Unity may echo an option and its value on separate lines.
            pending_value = not line[match.end():].strip(" \t\r\n:=\"'")
            yield "[REDACTED credential-bearing line]\n"
        else:
            yield JWT.sub("[REDACTED JWT]", line)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command
    if command[:1] == ["--"]:
        command = command[1:]
    if not command:
        parser.error("a command is required")
    # Native Editor logging must also flow through this process, not another file.
    for i, arg in enumerate(command):
        if arg.lower() == "-logfile" and (i + 1 == len(command) or command[i + 1] != "-"):
            parser.error("use -logFile - so native output is sanitized")
    output = Path(args.output)
    if not output.name.endswith(".sanitized.log"):
        parser.error("output must end with .sanitized.log")
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8") as retained:
        os.chmod(output, 0o600)
        with subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                              text=True, errors="replace") as process:
            for line in sanitized(process.stdout):
                retained.write(line)
                retained.flush()
                sys.stdout.write(line)
                sys.stdout.flush()
            return process.wait()


if __name__ == "__main__":
    sys.exit(main())
