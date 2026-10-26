#!/usr/bin/env python3
"""
Compile-surface gate for the MAUI app.

The Python gates in this repository parse XAML and C# with regular expressions, so a
type error that a compiler would reject in one second can pass them silently. That is
exactly how `IApiClient` shipped without three Phase-05 methods while every view model
called them: 3 x CS1061 on a real build, and every gate reported green.

This gate does not try to be a compiler. It checks the handful of cross-file
relationships that our project actually depends on and that regressions break most
often:

  A. `IApiClient` and `ApiClient` declare the same members, with the same arity.
  B. every `_api.<Method>(...)` call site targets a member declared on `IApiClient`
     and passes a compatible number of arguments.
  C. every `AppStrings.<Key>` reference exists in the generated `AppStrings.Designer.cs`.
  D. every `ApiClient.Codes.<Key>` reference exists as a declared constant.
  E. every `AppJsonContext.Default.<Name>` reference has a matching
     `[JsonSerializable(typeof(...))]` registration (source-generator naming rules).
  F. every type this project defines is reachable from every file that uses it:
     same namespace, or a `using` directive for its namespace (CS0246 / CS0103).

Exit code 0 = all checks passed; 1 = at least one failure (printed with file and line).
Usage: python3 tools/verify-compile-surface.py [-v]
"""

from __future__ import annotations

import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
os.chdir(ROOT)

VERBOSE = "-v" in sys.argv

checks = 0
failures: list[str] = []


def ok(condition: bool, message: str) -> None:
    global checks
    checks += 1
    if not condition:
        failures.append(message)


def read(path: str) -> str:
    return open(path, encoding="utf-8").read()


def source_files() -> list[str]:
    files = []
    for base in ("Core", "Features", "Controls", "Platforms", "Properties"):
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in {"obj", "bin"}]
            files += [os.path.join(dirpath, f) for f in filenames if f.endswith(".cs")]
    files += ["App.xaml.cs", "AppShell.xaml.cs", "MauiProgram.cs"]
    return [f for f in files if os.path.exists(f)]


def line_of(text: str, index: int) -> int:
    return text.count("\n", 0, index) + 1


def split_args(argtext: str) -> list[str]:
    """Split a call's argument list on top-level commas (respects <>, (), [], {})."""
    args, depth, current = [], 0, []
    for char in argtext:
        if char in "<([{":
            depth += 1
        elif char in ">)]}":
            depth -= 1
        if char == "," and depth == 0:
            args.append("".join(current).strip())
            current = []
        else:
            current.append(char)
    if "".join(current).strip():
        args.append("".join(current).strip())
    return args


def balanced_call(text: str, open_index: int) -> str:
    """Return the argument text of the call whose '(' is at open_index."""
    depth = 0
    for index in range(open_index, len(text)):
        if text[index] == "(":
            depth += 1
        elif text[index] == ")":
            depth -= 1
            if depth == 0:
                return text[open_index + 1:index]
    return ""


# ------------------------------------------------------------------ A. the client
API_PATH = "Core/Api/ApiClient.cs"
api = read(API_PATH)

interface_start = api.index("public interface IApiClient")
interface_text = api[interface_start:api.index("\n    }", interface_start)]

class_marker = "public sealed class ApiClient : IApiClient"
class_start = api.index(class_marker)
class_text = api[class_start:]

# Interface members are implicitly public, so they carry no modifier at all.
INTERFACE_SIGNATURE = r"^\s*Task<[^;{]*?>\s+(\w+)\s*\(([^;{]*?)\)\s*;"
# Implementations are public, and may be `async`.
CLASS_SIGNATURE = r"public\s+(?:static\s+|async\s+)?Task<[^;{]*?>\s+(\w+)\s*\(([^;{]*?)\)"


def members(text: str, signature: str) -> dict[str, int]:
    found = {}
    for name, params in re.findall(signature, text, re.S | re.M):
        found[name] = len([p for p in split_args(params) if p])
    return found


interface_members = members(interface_text, INTERFACE_SIGNATURE)
class_members = members(class_text, CLASS_SIGNATURE)

ok(interface_members, "could not parse any IApiClient members")
for name in sorted(set(class_members) - set(interface_members)):
    ok(False,
       f"{API_PATH}: ApiClient.{name} is not declared on IApiClient "
       f"(CS1061 at every call through the interface)")
for name in sorted(set(interface_members) - set(class_members)):
    ok(False, f"{API_PATH}: IApiClient.{name} has no implementation on ApiClient (CS0535)")

for name in sorted(set(interface_members) & set(class_members)):
    if interface_members[name] != class_members[name]:
        ok(False,
           f"{API_PATH}: {name} takes {interface_members[name]} parameter(s) on the "
           f"interface but {class_members[name]} on the class (CS0535)")

ok(class_marker in api, f"{API_PATH}: ApiClient no longer declares ': IApiClient'")

# ------------------------------------------------------- B. every call site resolves
# Fields/variables typed IApiClient, or the conventional injected field name `_api`.
CALL = re.compile(r"\b(_api|api)\s*\.\s*([A-Z]\w*)\s*(?:<[^>]*>)?\s*\(")

for path in source_files():
    text = read(path)
    if path == API_PATH:
        continue
    for match in CALL.finditer(text):
        name = match.group(2)
        open_index = match.end() - 1
        args = [a for a in split_args(balanced_call(text, open_index)) if a]
        line = line_of(text, match.start())
        if name not in interface_members:
            ok(False, f"{path}:{line}: {match.group(1)}.{name}() is not declared on "
                      f"IApiClient (CS1061)")
            continue
        expected = interface_members[name]
        # A trailing CancellationToken has a default; allow omitting it.
        if not (len(args) == expected or len(args) == expected - 1):
            ok(False, f"{path}:{line}: {name}() is called with {len(args)} argument(s) "
                      f"but the interface declares {expected} (CS7036)")

# ------------------------------------------------------------- C. resource strings
designer = read("Resources/Strings/AppStrings.Designer.cs")
designer_keys = set(re.findall(r"public static string (\w+)", designer))
used_keys = set()
for path in source_files():
    text = read(path)
    for match in re.finditer(r"\bAppStrings\.(\w+)", text):
        used_keys.add((match.group(1), path, line_of(text, match.start())))

for key, path, line in sorted(used_keys):
    ok(key in designer_keys,
       f"{path}:{line}: AppStrings.{key} does not exist (CS0117)")

# --------------------------------------------------------------- D. api error codes
codes_block = api[api.index("public static class Codes"):]
codes_block = codes_block[:codes_block.index("\n        }")]
code_names = set(re.findall(r"public const string (\w+)", codes_block))

for path in source_files():
    text = read(path)
    for match in re.finditer(r"\bCodes\.(\w+)", text):
        ok(match.group(1) in code_names,
           f"{path}:{line_of(text, match.start())}: ApiClient.Codes.{match.group(1)} "
           f"does not exist (CS0117)")

# ------------------------------------------------------- E. source-generated JSON
context_start = read("Core/Api/ApiContracts.cs")
context_start = context_start[context_start.index("public sealed partial class AppJsonContext"):]
serializable = re.findall(r"\[JsonSerializable\(typeof\(([^)]+)\)\)\]", read("Core/Api/ApiContracts.cs"))


def generated_name(t: str) -> str:
    """STJ's JsonSerializerContext names a generic list property by concatenation."""
    outer = re.match(r"([\w.]+)<(.+)>$", t)
    if outer:
        return generated_name(outer.group(1)) + generated_name(outer.group(2))
    return t.split(".")[-1]


generated = {generated_name(t) for t in serializable}

for path in ("Core/Api/ApiClient.cs", "Core/Api/SessionService.cs"):
    text = read(path)
    for match in re.finditer(r"AppJsonContext\.Default\.(\w+)", text):
        ok(match.group(1) in generated,
           f"{path}:{line_of(text, match.start())}: AppJsonContext.Default."
           f"{match.group(1)} has no [JsonSerializable(typeof(...))] registration (CS0117)")

# ------------------------------------------------------------ F. namespaces / usings
TYPE_DECL = re.compile(
    r"^\s*(?:public|internal)?\s*(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+)*"
    r"(?:class|record|struct|interface|enum)\s+(\w+)", re.M)
NAMESPACE = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)

type_namespace: dict[str, str] = {}
for path in source_files():
    text = read(path)
    ns = NAMESPACE.search(text)
    ns = ns.group(1) if ns else ""
    for name in TYPE_DECL.findall(text):
        type_namespace.setdefault(name, ns)

def strip_noise(text: str) -> str:
    """Blank out comments and string/char literals, keeping newlines so lines still map."""
    def blank(match: re.Match[str]) -> str:
        return re.sub(r"[^\n]", " ", match.group(0))

    text = re.sub(r"/\*.*?\*/", blank, text, flags=re.S)
    text = re.sub(r"//[^\n]*", blank, text)
    text = re.sub(r'@"(?:[^"]|"")*"', blank, text)
    text = re.sub(r'\$?"(?:\\.|[^"\\])*"', blank, text)
    text = re.sub(r"'(?:\\.|[^'\\])'", blank, text)
    return text


# GlobalUsings.cs makes a namespace available to every file without a per-file using.
GLOBAL_USINGS = set()
if os.path.exists("GlobalUsings.cs"):
    GLOBAL_USINGS = set(re.findall(
        r"^\s*global\s+using\s+(?:static\s+)?([\w.]+)\s*;", read("GlobalUsings.cs"), re.M))

# Types that live in a shared namespace and are used everywhere; require the using.
for path in source_files():
    text = strip_noise(read(path))
    ns_match = NAMESPACE.search(text)
    namespace = ns_match.group(1) if ns_match else ""
    usings = set(re.findall(r"^\s*using\s+(?:static\s+)?([\w.]+)\s*;", text, re.M))
    body = text
    for name, owner in type_namespace.items():
        if owner == namespace or owner in usings or owner in GLOBAL_USINGS or not owner:
            continue
        # Ignore the type's own declaration file and any declaration site.
        if re.search(rf"\b(?:class|record|struct|interface|enum)\s+{name}\b", body):
            continue
        for match in re.finditer(rf"(?<![\w.]){name}\b(?!\s*:)", body):
            tail = body[match.end():match.end() + 12]
            if tail.lstrip().startswith("=") and "." not in tail[:3]:
                pass  # still an identifier use; keep the report
            ok(False,
               f"{path}:{line_of(text, match.start())}: uses type {name} from "
               f"namespace '{owner}' without `using {owner};` (CS0246)")
            break

# ------------------------------------------------ G. MVVM source-generator surface
# [ObservableProperty] on `_isBusy` generates `IsBusy`; a duplicate field, a clashing
# member or a mismatched `partial void OnIsBusyChanged(...)` is a hard compile error
# that no XAML gate can see.

TYPE_START = re.compile(
    r"^\s*(?:public|internal)?\s*(?:static\s+|sealed\s+|abstract\s+|partial\s+)*"
    r"(?:class|record)\s+(\w+)", re.M)


def type_bodies(text: str):
    for match in TYPE_START.finditer(text):
        brace = text.find("{", match.end())
        if brace < 0:
            continue
        depth, index = 0, brace
        while index < len(text):
            if text[index] == "{":
                depth += 1
            elif text[index] == "}":
                depth -= 1
                if depth == 0:
                    break
            index += 1
        yield match.group(1), text[brace:index + 1]


OBSERVABLE = re.compile(
    r"\[ObservableProperty\][^\n]*\n(?:\s*\[[^\]]*\]\s*\n)*\s*private\s+"
    r"([\w<>?\[\],\. ]+?)\s+_?(\w+)\s*(?:=[^;]*)?;", re.M)


def generated_name(field: str) -> str:
    return field[:1].upper() + field[1:]


for path in source_files():
    text = read(path)
    for type_name, body in type_bodies(text):
        fields = OBSERVABLE.findall(body)
        generated = {}
        for field_type, field_name in fields:
            prop = generated_name(field_name)
            if prop in generated:
                ok(False, f"{path}: {type_name} has two [ObservableProperty] fields that "
                          f"both generate `{prop}` (CS0102)")
            generated[prop] = field_type.strip()

        # an explicit member with the same name as a generated property also clashes
        declared_members = set(re.findall(
            r"^\s*(?:public|private|protected|internal)\s+"
            r"(?:(?:static|readonly|virtual|override|async|partial|sealed|new|required)\s+)*"
            r"[\w<>?\[\],\. ]+?\s+(\w+)\s*(?:\(|=>|\{|;|=)", body, re.M))
        for prop in generated:
            if prop in declared_members:
                ok(False, f"{path}: {type_name}.{prop} is declared explicitly while "
                          f"[ObservableProperty] also generates it (CS0102)")

        # `partial void OnXChanged(T value)` must match the generated property type
        for callback, param in re.findall(r"partial\s+void\s+On(\w+?)(?:Changed|Changing)"
                                          r"\s*\(([^)]*)\)", body):
            if callback not in generated:
                ok(False, f"{path}: {type_name}.On{callback}Changed has no source property "
                          f"`{callback}` (CS0759)")
                continue
            parts = param.replace("\n", " ").split()
            declared = " ".join(parts[:-1]) if len(parts) > 1 else (parts[0] if parts else "")
            if declared and declared not in generated[callback] and \
                    generated[callback] not in declared:
                ok(False, f"{path}: {type_name}.On{callback}Changed takes `{declared}` but "
                          f"the property is `{generated[callback]}` (CS0761)")

        # [RelayCommand] must return void or Task, and CanExecute must name a bool method
        for attributes, return_type, method in re.findall(
                r"\[RelayCommand([^\]]*)\]\s*(?:private|public|internal)\s+"
                r"(?:async\s+)?([\w<>?]+)\s+(\w+)\s*\(", body):
            ok(return_type in {"void", "Task"} or return_type.startswith("Task<"),
               f"{path}: {type_name}.{method} is a [RelayCommand] returning "
               f"`{return_type}` (must be void or Task) (CS0407)")
            for guard in re.findall(r"CanExecute\s*=\s*nameof\((\w+)\)", attributes):
                guard_match = re.search(
                    rf"\b(?:private|public|internal)\s+(\w+)\s+{guard}\s*\(", body)
                if not guard_match:
                    ok(False, f"{path}: {type_name}.{method} uses CanExecute={guard} but no "
                              f"such method exists in the type (CS0103)")
                elif guard_match.group(1) != "bool":
                    ok(False, f"{path}: {type_name}.{guard} returns "
                              f"`{guard_match.group(1)}`, CanExecute needs bool (CS0407)")

# ------------------------------------------------------------ J. duplicate x:Name
import xml.etree.ElementTree as ET

X_NS = "{http://schemas.microsoft.com/winfx/2009/xaml}Name"

for dirpath, dirnames, filenames in os.walk("Features"):
    dirnames[:] = [d for d in dirnames if d not in {"obj", "bin"}]
    for filename in filenames:
        if not filename.endswith(".xaml"):
            continue
        page = os.path.join(dirpath, filename)
        try:
            root = ET.parse(page).getroot()
        except ET.ParseError as error:
            ok(False, f"{page}: not well-formed XML ({error})")
            continue
        names = [element.get(X_NS) for element in root.iter() if element.get(X_NS)]
        duplicates = {name for name in names if names.count(name) > 1}
        for name in sorted(duplicates):
            ok(False, f"{page}: x:Name '{name}' is declared more than once (XFC error)")

# ------------------------------------------------------------------------------ report
print()
if failures:
    print(f"{checks} checks, {len(failures)} failure(s):\n")
    for failure in failures:
        print("  -", failure)
    sys.exit(1)

print(f"{checks} checks, 0 failures")
print("COMPILE SURFACE CONSISTENT")
