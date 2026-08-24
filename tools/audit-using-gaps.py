#!/usr/bin/env python3
"""
Static audit: find C# files that reference a KaamLink.* type which is NOT
resolvable through that file's namespace, enclosing namespaces, usings, or
project-wide global usings. Approximates CS0246 name-lookup failures without
a .NET SDK.

Heuristics: comments and string/char literals are stripped (EF migrations
reference entities via strings — no using needed there); aliases are not
handled. Member access like `x.Availability` can still false-positive — each
hit prints its line so the list can be eyeballed in seconds.

Exit code 1 when violations are found. Scans backend/src and backend/tests.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def iter_cs_files(base_dirs):
    for base in base_dirs:
        for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, base)):
            dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", ".git")]
            for name in filenames:
                if name.endswith(".cs"):
                    yield os.path.join(dirpath, name)


def strip_noise(text):
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.DOTALL)
    text = re.sub(r"//[^\n]*", " ", text)
    for _ in range(3):  # nested interpolation holes may need several passes
        text = re.sub(r'\$?@"(?:""|[^"])*"', '""', text)          # verbatim
        text = re.sub(r'\$?"(?:\\.|[^"\\\n])*"', '""', text)      # regular/interp
        text = re.sub(r"'(?:\\.|[^'\\\n])*'", "''", text)         # char literal
    return text


NAMESPACE_RE = re.compile(r"^\s*namespace\s+([\w.]+)", re.MULTILINE)
USING_RE = re.compile(r"^\s*using\s+(?!static\b)([\w.]+)\s*;", re.MULTILINE)
GLOBAL_USING_RE = re.compile(r"^\s*global\s+using\s+(?!static\b)([\w.]+)\s*;", re.MULTILINE)
DECL_RE = re.compile(r"\b(?:class|record|interface|enum|struct)\s+([A-Z]\w*)")


def parents_of(ns):
    parts = ns.split(".")
    return [".".join(parts[:i]) for i in range(1, len(parts) + 1)]


def project_dir_of(path):
    directory = os.path.dirname(path)
    while True:
        if any(f.endswith(".csproj") for f in os.listdir(directory)):
            return directory
        parent = os.path.dirname(directory)
        if parent == directory or not parent.startswith(ROOT):
            return os.path.dirname(path)
        directory = parent


def main():
    files = list(iter_cs_files(["backend/src", "backend/tests"]))

    declarations = {}
    file_info = {}
    for path in files:
        raw = open(path, encoding="utf-8-sig").read()
        text = strip_noise(raw)
        ns_match = NAMESPACE_RE.search(text)
        namespace = ns_match.group(1) if ns_match else ""
        usings = set(USING_RE.findall(text)) | set(GLOBAL_USING_RE.findall(text))
        file_info[path] = (namespace, usings, text)
        if namespace.startswith("KaamLink"):
            for name in DECL_RE.findall(text):
                declarations.setdefault(name, set()).add(namespace)

    globals_by_project = {}
    for path, (_, _, text) in file_info.items():
        project = project_dir_of(path)
        globals_by_project.setdefault(project, set())
        globals_by_project[project] |= set(GLOBAL_USING_RE.findall(text))

    violations = []
    for path, (namespace, usings, text) in file_info.items():
        visible = set(usings) | globals_by_project.get(project_dir_of(path), set())
        visible |= set(parents_of(namespace))
        lines = text.splitlines()
        for word_match in re.finditer(r"\b[A-Z]\w+\b", text):
            word = word_match.group()
            homes = declarations.get(word)
            if not homes or any(home in visible for home in homes):
                continue
            line_no = text.count("\n", 0, word_match.start()) + 1
            rel = os.path.relpath(path, ROOT)
            source = lines[line_no - 1].strip() if line_no <= len(lines) else ""
            violations.append(
                f"{rel}:{line_no}: '{word}' ({', '.join(sorted(homes))})\n      | {source}")

    if violations:
        print(f"{len(violations)} potential using-gap(s):")
        for line in violations:
            print(f"  - {line}")
        sys.exit(1)
    print(f"no using-gaps across {len(files)} backend files")


if __name__ == "__main__":
    main()
