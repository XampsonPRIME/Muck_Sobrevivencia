#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage:
  deploy_linux_server.sh --host HOST [options]

Options:
  --host HOST             Droplet IP or hostname. Required.
  --user USER             SSH user. Default: root
  --key PATH              SSH private key path.
  --archive PATH          Local LinuxServer.tar.gz path.
  --port PORT             Game server port. Default: 7777
  --scene-set NAME        Scene set to boot. Default: Overworld
  --remote-dir PATH       Server install directory. Default: /opt/muck-server
  --service-name NAME     systemd service name. Default: muck-server
  --remote-archive PATH   Remote archive path. Default: /root/LinuxServer.tar.gz

Examples:
  ./scripts/deploy_linux_server.sh \
    --host 161.35.177.72 \
    --key ~/.ssh/id_ed25519 \
    --archive ~/Documents/MarpedBuilds/LinuxServer.tar.gz \
    --scene-set BossFight
EOF
}

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

HOST=""
USER_NAME="root"
SSH_KEY=""
ARCHIVE_PATH="$PROJECT_ROOT/Builds/LinuxServer.tar.gz"
PORT="7777"
SCENE_SET="Overworld"
REMOTE_DIR="/opt/muck-server"
SERVICE_NAME="muck-server"
REMOTE_ARCHIVE="/root/LinuxServer.tar.gz"

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
        --archive)
            ARCHIVE_PATH="${2:-}"
            shift 2
            ;;
        --port)
            PORT="${2:-}"
            shift 2
            ;;
        --scene-set)
            SCENE_SET="${2:-}"
            shift 2
            ;;
        --remote-dir)
            REMOTE_DIR="${2:-}"
            shift 2
            ;;
        --service-name)
            SERVICE_NAME="${2:-}"
            shift 2
            ;;
        --remote-archive)
            REMOTE_ARCHIVE="${2:-}"
            shift 2
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

if [[ ! -f "$ARCHIVE_PATH" ]]; then
    echo "Archive not found: $ARCHIVE_PATH" >&2
    exit 1
fi

SSH_TARGET="$USER_NAME@$HOST"

SSH_ARGS=()
if [[ -n "$SSH_KEY" ]]; then
    SSH_ARGS+=(-i "$SSH_KEY")
fi

SCP_ARGS=("${SSH_ARGS[@]}")

echo "Uploading archive to $SSH_TARGET:$REMOTE_ARCHIVE"
scp "${SCP_ARGS[@]}" "$ARCHIVE_PATH" "$SSH_TARGET:$REMOTE_ARCHIVE"

echo "Deploying on server..."
ssh "${SSH_ARGS[@]}" "$SSH_TARGET" \
    "SERVICE_NAME='$SERVICE_NAME' PORT='$PORT' SCENE_SET='$SCENE_SET' REMOTE_DIR='$REMOTE_DIR' REMOTE_ARCHIVE='$REMOTE_ARCHIVE' bash -s" <<'EOF'
set -euo pipefail

systemctl stop "$SERVICE_NAME" 2>/dev/null || true

rm -rf "$REMOTE_DIR"
mkdir -p "$REMOTE_DIR"

tar -xzf "$REMOTE_ARCHIVE" -C "$REMOTE_DIR"
find "$REMOTE_DIR" -name '._*' -delete

if ! find "$REMOTE_DIR" -maxdepth 1 -name '*.x86_64' | grep -q .; then
    first_dir="$(find "$REMOTE_DIR" -mindepth 1 -maxdepth 1 -type d | head -n 1 || true)"
    if [[ -n "$first_dir" ]]; then
        shopt -s dotglob nullglob
        mv "$first_dir"/* "$REMOTE_DIR"/
        shopt -u dotglob nullglob
        rmdir "$first_dir" 2>/dev/null || true
    fi
fi

EXECUTABLE_PATH="$(find "$REMOTE_DIR" -maxdepth 1 -name '*.x86_64' | head -n 1 || true)"
if [[ -z "$EXECUTABLE_PATH" ]]; then
    echo "No .x86_64 executable found in $REMOTE_DIR" >&2
    exit 1
fi

chmod +x "$EXECUTABLE_PATH"

cat > "/etc/systemd/system/${SERVICE_NAME}.service" <<SERVICE
[Unit]
Description=Muck Linux Dedicated Server
After=network.target

[Service]
Type=simple
WorkingDirectory=$REMOTE_DIR
ExecStart=$EXECUTABLE_PATH -batchmode -nographics -server -port $PORT -sceneSet $SCENE_SET
Restart=always
RestartSec=5
User=root

[Install]
WantedBy=multi-user.target
SERVICE

systemctl daemon-reload
systemctl enable "$SERVICE_NAME"
systemctl restart "$SERVICE_NAME"

echo
echo "Service status:"
systemctl --no-pager --full status "$SERVICE_NAME" || true

echo
echo "Listening sockets:"
ss -ltnp | grep ":$PORT" || true
EOF

echo "Deploy finished."
