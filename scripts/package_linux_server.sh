#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEFAULT_BUILD_DIR="$PROJECT_ROOT/Builds/LinuxServer"
DEFAULT_ARCHIVE_PATH="$PROJECT_ROOT/Builds/LinuxServer.tar.gz"

BUILD_DIR="${1:-$DEFAULT_BUILD_DIR}"
ARCHIVE_PATH="${2:-$DEFAULT_ARCHIVE_PATH}"

if [[ ! -d "$BUILD_DIR" ]]; then
    echo "Build directory not found: $BUILD_DIR" >&2
    echo "Usage: $(basename "$0") [build_dir] [archive_path]" >&2
    exit 1
fi

mkdir -p "$(dirname "$ARCHIVE_PATH")"

BUILD_PARENT_DIR="$(cd "$(dirname "$BUILD_DIR")" && pwd)"
BUILD_FOLDER_NAME="$(basename "$BUILD_DIR")"

rm -f "$ARCHIVE_PATH"

tar -czf "$ARCHIVE_PATH" -C "$BUILD_PARENT_DIR" "$BUILD_FOLDER_NAME"

echo "Archive created: $ARCHIVE_PATH"
