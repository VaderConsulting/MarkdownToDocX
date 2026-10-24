#!/usr/bin/env bash
set -euo pipefail
export PATH="${HOME}/.dotnet:${PATH}"
/workspace/scripts/capture-layout.sh /tmp/wt-18e5b09 /tmp/capture/18e5b09
/workspace/scripts/capture-audit.sh /tmp/wt-18e5b09 /tmp/capture/18e5b09
/workspace/scripts/capture-layout.sh /tmp/wt-eb09c58 /tmp/capture/eb09c58
/workspace/scripts/capture-audit.sh /tmp/wt-eb09c58 /tmp/capture/eb09c58
/workspace/scripts/capture-layout.sh /workspace /tmp/capture/current
/workspace/scripts/capture-audit.sh /workspace /tmp/capture/current
