#!/usr/bin/env bash
# Builds the documentation site.
# Usage: ./build.sh [output-dir]
# The site is generated into the root of gh-pages branch by default.
# Set DOCFX environment variable to use custom docfx executable.

set -euo pipefail

DOCS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCFX="${DOCFX:-docfx}"

# API metadata is generated from assemblies, not from the source code
dotnet build "$DOCS_DIR/../src/DotNext.slnx" -c Debug

"$DOCFX" metadata "$DOCS_DIR/docfx.json"
python3 "$DOCS_DIR/llms.py"

if [ $# -gt 0 ]; then
  # docfx.json has "dest": "../" which is resolved relative to the output dir
  "$DOCFX" build "$DOCS_DIR/docfx.json" -o "$1/_"
else
  "$DOCFX" build "$DOCS_DIR/docfx.json"
fi
