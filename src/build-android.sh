set -euo pipefail
cd "$(dirname "$0")"
if [ -z "${DOTNET:-}" ]; then
  if [ -x "$LOCALAPPDATA/dotnet10/dotnet.exe" ]; then
    DOTNET="$LOCALAPPDATA/dotnet10/dotnet.exe"
  else
    DOTNET="dotnet"
  fi
fi
if [ -z "${JAVA_SDK:-}" ]; then
  JDK21_DIR=$(ls -d "$LOCALAPPDATA"/jdk21/jdk-* 2>/dev/null | head -n 1 || true)
  if [ -n "$JDK21_DIR" ]; then
    JAVA_SDK="$JDK21_DIR"
  else
    JAVA_SDK="C:/Program Files/Java/jdk-24"
  fi
fi
ANDROID_SDK="${ANDROID_SDK:-$LOCALAPPDATA/Android/Sdk}"
CONFIG="${1:-Release}"
shift || true
for plugin in Skua.Plugin.DailyTracker Skua.Plugin.ButlerV5 Skua.Plugin.Cosmetics; do
  "$DOTNET" build "$plugin" -c "$CONFIG" "$@"
done
SIGN_ARGS=()
if [ "$CONFIG" = "Release" ]; then
  PROPS="signing/signing.properties"
  if [ ! -f "$PROPS" ]; then
    echo "Release build: $PROPS is missing (storeFile, storePassword, keyAlias, keyPassword)." >&2
    exit 1
  fi
  prop() { grep -E "^$1=" "$PROPS" | head -n 1 | cut -d= -f2- | tr -d '\r'; }
  export MVS_STORE_PASS="$(prop storePassword)" MVS_KEY_PASS="$(prop keyPassword)"
  SIGN_ARGS=(-p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=$(pwd -W 2>/dev/null || pwd)/$(prop storeFile)"
    "-p:AndroidSigningKeyAlias=$(prop keyAlias)" -p:AndroidSigningStorePass=env:MVS_STORE_PASS -p:AndroidSigningKeyPass=env:MVS_KEY_PASS)
fi
"$DOTNET" build MoVibeSkua.Android -c "$CONFIG" -f net10.0-android \
  -p:AndroidSdkDirectory="$ANDROID_SDK" -p:JavaSdkDirectory="$JAVA_SDK" "${SIGN_ARGS[@]}" "$@"
