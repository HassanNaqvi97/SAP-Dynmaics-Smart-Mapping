#!/usr/bin/env bash
set -euo pipefail

# Removes generated build artifacts that commonly retain bad merge content.
rm -rf obj bin

# Remove broken NuGet cache files if present.
find . -type f \( -name 'project.nuget.cache' -o -name '*.nuget.dgspec.json' \) -print -delete || true

# Detect unresolved merge markers in tracked text files.
if rg -n "^(<<<<<<<|=======|>>>>>>>)" . --glob '!.git' ; then
  echo "\nUnresolved merge markers were found. Please resolve files listed above." >&2
  exit 2
else
  echo "No unresolved merge markers found."
fi
