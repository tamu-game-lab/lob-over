#!/usr/bin/env bash
cd -- "$(dirname -- "${BASH_SOURCE[0]}")" || exit 1

if ! command -v node >/dev/null 2>&1; then
  export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
fi
if ! command -v node >/dev/null 2>&1 || ! node -e 'process.exit(Number(process.versions.node.split(".")[0]) >= 20 ? 0 : 1)'; then
  echo "Install Node.js 20 or newer from https://nodejs.org"
  read -r -n 1 -p "Press any key to close. "
  echo
  exit 1
fi

if [[ ! -d Monitor/node_modules ]]; then
  (cd Monitor && npm ci) || { echo "Dependencies could not be installed."; exit 1; }
fi

# WO-L6: USB is optional; a missing or disconnected device cannot block Wi-Fi.
adb_path=$(command -v adb 2>/dev/null || true)
if [[ -z "$adb_path" && -x "$HOME/Library/Android/sdk/platform-tools/adb" ]]; then
  adb_path="$HOME/Library/Android/sdk/platform-tools/adb"
fi
if [[ -n "$adb_path" ]]; then
  while read -r serial status rest; do
    if [[ "${status%$'\r'}" == device ]]; then
      "$adb_path" -s "$serial" reverse tcp:7331 tcp:7331 >/dev/null 2>&1 || true
    fi
  done < <("$adb_path" devices 2>/dev/null)
fi

node Monitor/src/host/server.js Data/Scenarios/level1_texas.json --discovery &
host_pid=$!
cleanup() {
  kill "$host_pid" 2>/dev/null || true
  wait "$host_pid" 2>/dev/null || true
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# Poll the actual listener before opening the page, without requiring curl.
if ! node --input-type=module -e '
  import net from "node:net";
  for (let attempt = 0; attempt < 100; attempt++) {
    const ready = await new Promise(resolve => {
      const socket = net.connect(7331, "127.0.0.1");
      socket.setTimeout(200);
      const finish = ok => { socket.destroy(); resolve(ok); };
      socket.once("connect", () => finish(true));
      socket.once("error", () => finish(false));
      socket.once("timeout", () => finish(false));
    });
    if (ready) process.exit(0);
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  process.exit(1);
' || ! kill -0 "$host_pid" 2>/dev/null; then
  echo "The game host did not start on port 7331."
  exit 1
fi

url=http://localhost:7331/play.html
case "$OSTYPE" in
  darwin*) open "$url" ;;
  msys*|cygwin*) start "" "$url" ;;
  *) xdg-open "$url" ;;
esac
echo "Press Ctrl-C to end the game"
wait "$host_pid"
