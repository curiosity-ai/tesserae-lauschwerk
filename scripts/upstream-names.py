#!/usr/bin/env python3
"""The names this repository changed from upstream's, in one place.

The code in sdk/ came from .reference/ (upstream as forked) and was renamed to Lauschwerk. This mapping is
what did it, and what scripts/diff-upstream.sh applies to a copy of .reference/ before diffing, so the
diff shows the changes we made rather than thousands of renamed identifiers.

    python3 scripts/upstream-names.py text   < in > out    rename the names in a text stream
    python3 scripts/upstream-names.py path   <relative path> rename a path
    python3 scripts/upstream-names.py tree   <dir>          rename every text file and path under dir, in place

Upstream's name is spelled here and in the model download URLs (sdk/crates/core/models.json); nowhere
else on our side but the LICENSE, the NOTICE files and the README's credit.
"""
import os
import re
import sys

OLD = "al" + "kove"            # kept split so a search for the old name finds the mapping, not this line
NEW = "lauschwerk"

# Ordered: the short id/class prefixes first, then the brand in its three casings.
RULES = [
    (re.compile(r"\btss-" + OLD + "-"), "tss-lw-"),
    (re.compile(r"\balk-(?=[a-z])"), "lw-"),
    (re.compile(OLD.upper()), NEW.upper()),
    (re.compile(OLD.capitalize()), NEW.capitalize()),
    (re.compile(OLD), NEW),
]

BINARY = {".wav", ".f32", ".png", ".webp", ".wasm", ".alk", ".ico", ".woff2", ".zip"}


def text(s: str) -> str:
    for pattern, replacement in RULES:
        s = pattern.sub(replacement, s)
    return s


def path(p: str) -> str:
    return "/".join(text(part) for part in p.split("/"))


def tree(root: str) -> None:
    for here, dirs, files in os.walk(root, topdown=False):
        for name in files:
            full = os.path.join(here, name)
            if os.path.splitext(name)[1].lower() not in BINARY:
                try:
                    with open(full, encoding="utf-8") as f:
                        content = f.read()
                except UnicodeDecodeError:
                    content = None
                if content is not None and text(content) != content:
                    with open(full, "w", encoding="utf-8") as f:
                        f.write(text(content))
            if text(name) != name:
                os.rename(full, os.path.join(here, text(name)))
        for name in dirs:
            if text(name) != name:
                os.rename(os.path.join(here, name), os.path.join(here, text(name)))


if __name__ == "__main__":
    mode = sys.argv[1]
    if mode == "text":
        sys.stdout.write(text(sys.stdin.read()))
    elif mode == "path":
        print(path(sys.argv[2]))
    elif mode == "tree":
        tree(sys.argv[2])
