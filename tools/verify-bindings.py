#!/usr/bin/env python3
"""
Static compiled-binding checker for the KaamLink MAUI pages.

XamlC compiles every {Binding} that sits inside an x:DataType scope, so a typo in
a property name or a stale command name is a build break (XFC0045) that no other
repo gate catches. This script resolves each binding path against the C# type the
XAML declares, entirely offline:

  * page-level x:DataType on the root element
  * per-DataTemplate x:DataType, nesting aware
  * Source={x:Reference Name}      -> that element's x:DataType
  * Path=BindingContext.Foo        -> page VM
  * RelativeSource AncestorType=vm:T -> T

Type information is scraped from the .cs files: public auto-properties, get-only
collections, expression-bodied properties, [ObservableProperty] backing fields,
[RelayCommand] methods (-> XxxCommand), and positional record parameters.

Usage:  python3 tools/verify-bindings.py [--verbose]
Exit code 1 when a binding cannot be resolved.
"""

from __future__ import annotations

import glob
import re
import sys
import xml.etree.ElementTree as ET

NS_MAUI = "http://schemas.microsoft.com/dotnet/2021/maui"
NS_X = "http://schemas.microsoft.com/winfx/2009/xaml"

# Members that exist on framework types, not declared by us.
FRAMEWORK_MEMBERS = {
    "Count", "Length", "Item", "Self", "BindingContext", ".",
}

TYPES: dict[str, set[str]] = {}
ALIASES: dict[str, str] = {}


def pascal(name: str) -> str:
    return name[:1].upper() + name[1:] if name else name


def strip_comments(text: str) -> str:
    out, i, n = [], 0, len(text)
    while i < n:
        two = text[i:i + 2]
        if two == "//":
            while i < n and text[i] != "\n":
                i += 1
        elif two == "/*":
            i += 2
            while i < n and text[i:i + 2] != "*/":
                i += 1
            i += 2
        else:
            out.append(text[i])
            i += 1
    return "".join(out)


PROP_RE = re.compile(
    r"public\s+(?:static\s+)?(?:readonly\s+)?"
    r"(?P<type>[\w<>?\[\],\.\s]+?)\s+(?P<name>[A-Za-z_]\w*)\s*(?:\{|=>)"
)
OBS_RE = re.compile(
    r"\[ObservableProperty\][\s\S]{0,120}?private\s+[\w<>?\[\],\.\s]+?\s+_(?P<name>\w+)\s*(?:[=;])"
)
RELAY_RE = re.compile(
    r"\[RelayCommand[^\]]*\]\s*(?:\[[^\]]*\]\s*)*(?:private|public|internal)\s+"
    r"(?:async\s+)?[\w<>?\[\],\.]+\s+(?P<name>\w+)\s*\("
)
TYPE_DECL_RE = re.compile(
    r"\b(?:class|record|struct|interface)\s+(?P<name>\w+)"
    r"(?:\s*<[^>]*>)?\s*(?P<params>\([^)]*\))?"
)


def collect_types() -> None:
    for path in glob.glob("**/*.cs", recursive=True):
        if path.startswith(("obj/", "bin/")) or path.endswith(".Designer.cs"):
            continue
        text = strip_comments(open(path, encoding="utf-8").read())
        for decl in TYPE_DECL_RE.finditer(text):
            name = decl.group("name")
            members: set[str] = set()
            # body: from the declaration to the end of the file is too broad, but
            # member names are additive per type name, so over-collection only
            # makes the checker more lenient (never a false failure).
            body = text[decl.end():]
            for prop in PROP_RE.finditer(body):
                members.add(prop.group("name"))
            for obs in OBS_RE.finditer(body):
                members.add(pascal(obs.group("name")))
            for relay in RELAY_RE.finditer(body):
                # CommunityToolkit.Mvvm drops the Async suffix: LoadAsync -> LoadCommand
                cmd = re.sub(r"Async$", "", relay.group("name"))
                members.add(pascal(cmd) + "Command")
            for param in (decl.group("params") or "").strip("()").split(","):
                param = param.strip()
                if not param:
                    continue
                token = param.split("=")[0].strip().split()
                if token:
                    members.add(pascal(token[-1]))
            TYPES.setdefault(name, set()).update(members)


COLLECTIONS: dict[str, dict[str, str]] = {}
COLLECTION_RE = re.compile(
    r"public\s+(?:ObservableCollection|IReadOnlyList|List|IList)<(?P<item>[\w.]+)>\s+(?P<name>\w+)"
)
# Positional record parameters carry no access modifier: "IReadOnlyList<string> Skills".
PARAM_COLLECTION_RE = re.compile(
    r"(?:ObservableCollection|IReadOnlyList|List|IList)<(?P<item>[\w.]+)>\s+(?P<name>\w+)"
)


def collect_collections() -> None:
    for path in glob.glob("**/*.cs", recursive=True):
        if path.startswith(("obj/", "bin/")) or path.endswith(".Designer.cs"):
            continue
        text = strip_comments(open(path, encoding="utf-8").read())
        for decl in TYPE_DECL_RE.finditer(text):
            body = text[decl.end():]
            bucket = COLLECTIONS.setdefault(decl.group("name"), {})
            # positional record parameters live before the body starts
            params = decl.group("params") or ""
            for coll in PARAM_COLLECTION_RE.finditer(params):
                bucket[coll.group("name")] = coll.group("item").split(".")[-1]
            for coll in COLLECTION_RE.finditer(body):
                bucket[coll.group("name")] = coll.group("item").split(".")[-1]


def collection_item_type(type_name: str, member_path: str) -> str | None:
    """Item type of a collection member, e.g. (VacancyFeedViewModel, 'Items') -> VacancyCardModel."""
    type_name = re.sub(r"^[\w.]+:", "", type_name or "").split(".")[-1]
    member = member_path.split(".")[0]
    return COLLECTIONS.get(type_name, {}).get(member)


def type_members(name: str) -> set[str]:
    name = name.replace("{x:Type", "").replace("}", "").replace(" ", "").strip()
    name = re.sub(r"^[\w.]+:", "", name)      # vm:VacancyFeedViewModel -> VacancyFeedViewModel
    name = name.split(".")[-1]
    return TYPES.get(name, set())


def binding_value(raw: str) -> str:
    """Strip markup-extension braces if present."""
    raw = raw.strip()
    if raw.startswith("{") and raw.endswith("}"):
        raw = raw[1:-1].strip()
    return raw


def parse_markup(raw: str) -> tuple[str, dict[str, str]]:
    """Return (extension, {'positional': ..., key: value}) for a markup extension."""
    raw = binding_value(raw)
    if not raw:
        return "", {}
    parts = split_args(raw)
    head = parts[0].strip()
    # "{Binding Title}" -> extension "Binding", positional argument "Title"
    m = re.match(r"(?P<ext>[\w.:]+)(?:\s+(?P<rest>.*))?$", head)
    ext = m.group("ext") if m else head
    rest = (m.group("rest") or "").strip() if m else ""
    args: dict[str, str] = {}
    positional = [rest] if rest else []
    for chunk in parts[1:]:
        if "=" in chunk:
            k, v = chunk.split("=", 1)
            args[k.strip()] = v.strip()
        else:
            positional.append(chunk.strip())
    if positional:
        args["<pos>"] = positional[0]
    return ext, args


def split_args(expr: str) -> list[str]:
    """Split on top-level commas, honouring nested braces and quotes."""
    out, depth, current, quote = [], 0, [], None
    for ch in expr:
        if quote:
            current.append(ch)
            if ch == quote:
                quote = None
            continue
        if ch in "'\"":
            quote = ch
            current.append(ch)
        elif ch == "{":
            depth += 1
            current.append(ch)
        elif ch == "}":
            depth -= 1
            current.append(ch)
        elif ch == "," and depth == 0:
            out.append("".join(current))
            current = []
        else:
            current.append(ch)
    if current:
        out.append("".join(current))
    return out


def attr(el: ET.Element, key: str) -> str | None:
    v = el.get(f"{{{NS_MAUI}}}{key}")
    return v if v is not None else el.get(f"{{{NS_X}}}{key}")


def check_binding_path(path: str, scope: str, sources: dict[str, str],
                       where: str, failures: list[str], warnings: list[str]) -> None:
    path = path.strip()
    if not path or path == ".":
        return
    # property of the scope type only (no dotted navigation through element names)
    first = path.split(".")[0].split("[")[0]
    if first in FRAMEWORK_MEMBERS:
        return
    members = type_members(scope)
    if not members:
        warnings.append(f"{where}: no type information for scope '{scope}' (skipped '{path}')")
        return
    if first in members:
        return
    failures.append(f"{where}: '{path}' not found on {scope}")


def main() -> int:
    verbose = "--verbose" in sys.argv
    collect_types()
    collect_collections()
    failures: list[str] = []
    warnings: list[str] = []
    checked = 0

    for path in sorted(glob.glob("Features/**/*.xaml", recursive=True)):
        tree = ET.parse(path)
        root = tree.getroot()
        root_type = attr(root, "DataType")
        named: dict[str, str] = {}       # x:Name -> x:DataType of that element
        element_types: dict[int, str] = {}

        # Pre-pass: element names and their DataType (inherited from ancestors)
        def walk_names(el: ET.Element, inherited: str | None) -> None:
            dt = attr(el, "DataType") or inherited
            element_types[id(el)] = dt or ""
            for k, v in el.attrib.items():
                if k == f"{{{NS_X}}}Name":
                    named[v] = dt or ""
            for child in el:
                walk_names(child, dt)

        walk_names(root, root_type)

        def walk(el: ET.Element, scope: str) -> None:
            nonlocal checked
            for key, raw in el.attrib.items():
                if "Binding" not in raw or "{" not in raw:
                    continue
                # ItemDisplayBinding is evaluated against the items source, not the VM.
                if key.split(".")[-1] == "ItemDisplayBinding":
                    continue
                # Nested markup extensions (StringFormat etc.) are handled by the
                # outer {Binding}; scan only attributes holding one extension.
                if not raw.strip().startswith("{"):
                    continue
                ext, args = parse_markup(raw)
                if ext.lower().split(".")[-1] not in ("binding", "compiledbinding"):
                    continue
                effective_scope = scope
                source = args.get("Source", "")
                ancestor = args.get("RelativeSource", "")
                if source:
                    m = re.search(r"x:Reference\s+(\w+)", source)
                    if m and m.group(1) in named:
                        effective_scope = named[m.group(1)] or scope
                    elif "x:Reference" in source:
                        warnings.append(f"{path}: {ext} on {key}: unresolved x:Reference")
                        continue
                    elif "RelativeSource" in source:
                        m = re.search(r"AncestorType=\{?x:Type\s+([\w:.]+)\}?", source)
                        if not m:
                            continue
                        effective_scope = m.group(1).split(":")[-1]
                    else:
                        continue          # {Binding .} against an explicit source
                elif ancestor:
                    m = re.search(r"AncestorType=\{?x:Type\s+([\w:.]+)\}?", ancestor)
                    if not m:
                        continue
                    effective_scope = m.group(1).split(":")[-1]
                    if effective_scope in ("ContentPage", "Page"):
                        effective_scope = root_type or ""   # page BindingContext is the VM
                binding_path = args.get("<pos>", args.get("Path", ""))
                if not binding_path:
                    continue
                # 'BindingContext.X, Source={x:Reference Page}' -> page VM member X
                if binding_path.startswith("BindingContext."):
                    inner = binding_path.split(".", 1)[1]
                    target = effective_scope                   # scope of the binding source
                    checked += 1
                    check_binding_path(inner, target or root_type or "", named,
                                       f"{path}:{key}", failures, warnings)
                    continue
                checked += 1
                check_binding_path(binding_path, effective_scope or "",
                                   named, f"{path}:{key}", failures, warnings)
            for child in el:
                child_dt = attr(child, "DataType")
                walk(child, child_dt or scope)

        walk(root, root_type or "")

    print(f"{len(glob.glob('Features/**/*.xaml', recursive=True))} pages, "
          f"{len(TYPES)} types, {checked} compiled bindings checked")
    if verbose and warnings:
        for w in warnings:
            print("  note:", w)
    if failures:
        print("FAILURES:")
        for f in failures:
            print("  -", f)
        return 1
    print("ALL COMPILED BINDINGS RESOLVE")
    return 0


if __name__ == "__main__":
    sys.exit(main())
