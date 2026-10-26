#!/usr/bin/env bash
# What sdk/ (the source this repository builds) changes against .reference/ (upstream as forked).
#
#   scripts/diff-upstream.sh            a summary: the files that differ
#   scripts/diff-upstream.sh -u         the full unified diff
#
# sdk/ was renamed from upstream's names (scripts/upstream-names.py holds the mapping). So the kept parts of
# .reference/ are copied to a temporary folder and renamed by the same mapping first: what is left is the
# changes we made, not every renamed identifier.
#
# Left out: build output and local state (dist/, target/, node_modules/, .cache/), and the files that exist
# on our side by design - the SDK's package-lock.json and .npmrc (npm here, pnpm upstream). Expected to
# differ, and listed last: the workspace Cargo.toml and Cargo.lock (Python and Android are not members) and
# crates/core/models.json (each model carries its download url).
#
# To take a newer upstream: replace .reference/ with it, run this with -u to list our changes, copy the new
# sources into sdk/, rename them (python3 scripts/upstream-names.py tree sdk/...) and carry the changes over.
set -euo pipefail
cd "$(dirname "$0")/.."

mode=(-rq)
[[ "${1:-}" == "-u" ]] && mode=(-ru)

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

# The kept parts of upstream, at their upstream paths, then renamed as sdk/ was.
mkdir -p "$tmp/upstream/crates" "$tmp/upstream/packages"
for part in crates/engine crates/core crates/wasm; do cp -r ".reference/$part" "$tmp/upstream/crates/"; done
cp -r .reference/packages/* "$tmp/upstream/packages/"
cp .reference/rust-toolchain.toml .reference/rustfmt.toml .reference/Cargo.toml .reference/Cargo.lock "$tmp/upstream/"
python3 scripts/upstream-names.py tree "$tmp/upstream"

excludes=(-x node_modules -x dist -x target -x .cache -x package-lock.json -x .npmrc -x test-results -x models.json -x LICENSE -x NOTICE)

status=0
for part in crates/engine crates/core crates/wasm packages rust-toolchain.toml rustfmt.toml; do
  diff "${mode[@]}" "${excludes[@]}" "$tmp/upstream/$part" "sdk/$part" | sed "s#$tmp/upstream#upstream#g" || status=1
done

# The license texts keep upstream's name on both sides, so they are compared as they are.
for f in LICENSE NOTICE; do
  cmp -s ".reference/packages/al""kove/$f" "sdk/packages/lauschwerk/$f" || { echo "License text differs: sdk/packages/lauschwerk/$f"; status=1; }
done

echo "--- expected differences:"
for f in Cargo.toml Cargo.lock crates/core/models.json; do
  cmp -s "$tmp/upstream/$f" "sdk/$f" || echo "  sdk/$f"
done

[[ $status == 0 ]] && echo "sdk/ matches upstream's source, names aside."
exit 0
