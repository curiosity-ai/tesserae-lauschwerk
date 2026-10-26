#!/usr/bin/env bash
# What sdk/ (the alkove source this repository builds) changes against .reference/ (upstream as forked).
#
#   scripts/diff-upstream.sh            a summary: the files that differ
#   scripts/diff-upstream.sh -u         the full unified diff
#
# Build output and local state are left out (dist/, target/, node_modules/, .cache/), and so are the
# files that only exist on our side by design: sdk/packages/alkove's package-lock.json and .npmrc (it is
# installed with npm here, with pnpm upstream). The workspace Cargo.toml and Cargo.lock differ on
# purpose too (Python and Android are not members) and are listed last.
#
# To take a newer upstream: replace .reference/ with it, run this to see what we changed, then carry
# those changes over onto a fresh copy in sdk/.
set -euo pipefail
cd "$(dirname "$0")/.."

mode=(-rq)
[[ "${1:-}" == "-u" ]] && mode=(-ru)

excludes=(-x node_modules -x dist -x target -x .cache -x package-lock.json -x .npmrc -x test-results)

status=0
for part in crates/engine crates/core crates/wasm packages/alkove rust-toolchain.toml rustfmt.toml; do
  diff "${mode[@]}" "${excludes[@]}" ".reference/$part" "sdk/$part" || status=1
done

echo "--- expected differences (workspace members):"
diff -q .reference/Cargo.toml sdk/Cargo.toml || true
diff -q .reference/Cargo.lock sdk/Cargo.lock || true

[[ $status == 0 ]] && echo "sdk/ matches upstream's source."
exit 0
