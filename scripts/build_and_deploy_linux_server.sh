#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage:
  build_and_deploy_linux_server.sh --host HOST [options]

Pipeline:
  1. Builds Linux Server with Unity in batchmode
  2. Packages the build into LinuxServer.tar.gz
  3. Uploads the archive to the server
  4. Extracts the build on the server
  5. Recreates and restarts the systemd service

Options:
  --host HOST             Droplet IP or hostname. Required.
  --user USER             SSH user. Default: root
  --key PATH              SSH private key path. Default: ~/.ssh/digitalocean
  --scene-set NAME        Scene set to boot. Default: Overworld
  --port PORT             Game server port. Default: 7777
  --service-name NAME     systemd service name. Default: muck-server
  --remote-dir PATH       Server install directory. Default: /opt/muck-server
  --remote-archive PATH   Remote archive path. Default: /root/LinuxServer.tar.gz
  --unity PATH            Unity executable path. Auto-detected by ProjectVersion.txt
  --build-dir PATH        Output folder for Linux Server build. Default: <repo>/Builds/LinuxServer
  --archive PATH          Output tar.gz path. Default: <repo>/Builds/LinuxServer.tar.gz
  --execute-method NAME   Unity execute method. Default: LinuxServerBuild.BuildFromCommandLine
  --skip-build            Skip Unity build and only package/deploy existing build
  --skip-package          Skip packaging and deploy existing archive
  -h, --help              Show this help

Examples:
  ./scripts/build_and_deploy_linux_server.sh \
    --host 161.35.177.72 \
    --key ~/.ssh/digitalocean \
    --scene-set BossFight

  ./scripts/build_and_deploy_linux_server.sh \
    --host 161.35.177.72 \
    --key ~/.ssh/digitalocean \
    --scene-set Overworld \
    --skip-build
EOF
}

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_SCRIPT="$PROJECT_ROOT/scripts/package_linux_server.sh"
DEPLOY_SCRIPT="$PROJECT_ROOT/scripts/deploy_linux_server.sh"
PROJECT_VERSION_FILE="$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt"
DEFAULT_BUILD_DIR="$PROJECT_ROOT/Builds/LinuxServer"
DEFAULT_ARCHIVE_PATH="$PROJECT_ROOT/Builds/LinuxServer.tar.gz"

HOST=""
USER_NAME="root"
SSH_KEY="$HOME/.ssh/digitalocean"
SCENE_SET="Overworld"
PORT="7777"
SERVICE_NAME="muck-server"
REMOTE_DIR="/opt/muck-server"
REMOTE_ARCHIVE="/root/LinuxServer.tar.gz"
UNITY_PATH=""
BUILD_DIR="$DEFAULT_BUILD_DIR"
ARCHIVE_PATH="$DEFAULT_ARCHIVE_PATH"
EXECUTE_METHOD="LinuxServerBuild.BuildFromCommandLine"
SKIP_BUILD="false"
SKIP_PACKAGE="false"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --host)
            HOST="${2:-}"
            shift 2
            ;;
        --user)
            USER_NAME="${2:-}"
            shift 2
            ;;
        --key)
            SSH_KEY="${2:-}"
            shift 2
            ;;
        --scene-set)
            SCENE_SET="${2:-}"
            shift 2
            ;;
        --port)
            PORT="${2:-}"
            shift 2
            ;;
        --service-name)
            SERVICE_NAME="${2:-}"
            shift 2
            ;;
        --remote-dir)
            REMOTE_DIR="${2:-}"
            shift 2
            ;;
        --remote-archive)
            REMOTE_ARCHIVE="${2:-}"
            shift 2
            ;;
        --unity)
            UNITY_PATH="${2:-}"
            shift 2
            ;;
        --build-dir)
            BUILD_DIR="${2:-}"
            shift 2
            ;;
        --archive)
            ARCHIVE_PATH="${2:-}"
            shift 2
            ;;
        --execute-method)
            EXECUTE_METHOD="${2:-}"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD="true"
            shift
            ;;
        --skip-package)
            SKIP_PACKAGE="true"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
done

if [[ -z "$HOST" ]]; then
    echo "--host is required." >&2
    usage >&2
    exit 1
fi

if [[ ! -f "$BUILD_SCRIPT" ]]; then
    echo "Build packaging script not found: $BUILD_SCRIPT" >&2
    exit 1
fi

if [[ ! -f "$DEPLOY_SCRIPT" ]]; then
    echo "Deploy script not found: $DEPLOY_SCRIPT" >&2
    exit 1
fi

detect_unity_path() {
    if [[ -n "$UNITY_PATH" ]]; then
        return 0
    fi

    if [[ ! -f "$PROJECT_VERSION_FILE" ]]; then
        echo "ProjectVersion.txt not found: $PROJECT_VERSION_FILE" >&2
        exit 1
    fi

    local unity_version
    unity_version="$(awk -F': ' '/^m_EditorVersion:/ { print $2 }' "$PROJECT_VERSION_FILE")"

    if [[ -z "$unity_version" ]]; then
        echo "Could not parse Unity version from $PROJECT_VERSION_FILE" >&2
        exit 1
    fi

    local candidate="/Applications/Unity/Hub/Editor/${unity_version}/Unity.app/Contents/MacOS/Unity"
    if [[ -x "$candidate" ]]; then
        UNITY_PATH="$candidate"
        return 0
    fi

    echo "Unity executable not found for version ${unity_version}." >&2
    echo "Pass it explicitly with --unity /path/to/Unity" >&2
    exit 1
}

ensure_project_not_open() {
    local running_project
    running_project="$(ps -ax -o command | grep "/Unity.app/Contents/MacOS/Unity" | grep -F "$PROJECT_ROOT" || true)"
    if [[ -n "$running_project" ]]; then
        echo "Unity is currently open with this project." >&2
        echo "Close the editor before running the local pipeline." >&2
        exit 1
    fi
}

run_unity_build() {
    detect_unity_path
    ensure_project_not_open

    mkdir -p "$(dirname "$BUILD_DIR")"

    echo "Building Linux Server with Unity..."
    "$UNITY_PATH" \
        -batchmode \
        -quit \
        -projectPath "$PROJECT_ROOT" \
        -executeMethod "$EXECUTE_METHOD" \
        -buildOutput "$BUILD_DIR" \
        -logFile -

    echo "Unity build finished: $BUILD_DIR"
}

run_package() {
    echo "Packaging build..."
    bash "$BUILD_SCRIPT" "$BUILD_DIR" "$ARCHIVE_PATH"
}

run_deploy() {
    echo "Deploying to $USER_NAME@$HOST..."

    local deploy_args=(
        --host "$HOST"
        --user "$USER_NAME"
        --archive "$ARCHIVE_PATH"
        --port "$PORT"
        --scene-set "$SCENE_SET"
        --remote-dir "$REMOTE_DIR"
        --service-name "$SERVICE_NAME"
        --remote-archive "$REMOTE_ARCHIVE"
    )

    if [[ -n "$SSH_KEY" ]]; then
        deploy_args+=(--key "$SSH_KEY")
    fi

    bash "$DEPLOY_SCRIPT" "${deploy_args[@]}"
}

if [[ "$SKIP_BUILD" != "true" ]]; then
    run_unity_build
fi

if [[ "$SKIP_PACKAGE" != "true" ]]; then
    run_package
fi

if [[ ! -f "$ARCHIVE_PATH" ]]; then
    echo "Archive not found: $ARCHIVE_PATH" >&2
    echo "Generate it first or remove --skip-package." >&2
    exit 1
fi

run_deploy

echo
echo "Pipeline finished successfully."
