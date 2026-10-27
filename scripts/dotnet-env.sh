#!/usr/bin/env bash
# Настраивает окружение для сборки. Использование: source scripts/dotnet-env.sh
# Переменные: ODD_DOTNET_DIR, ODD_OPENSSL_DIR, ODD_SDK_CACHE — можно задать заранее.
if [ -n "${ODD_DOTNET_DIR:-}" ]; then
  DOTNET_ROOT="$ODD_DOTNET_DIR"
elif [ -x "${ODD_SDK_CACHE:-$HOME/.cache/oddmaster}/dotnet/dotnet" ]; then
  DOTNET_ROOT="${ODD_SDK_CACHE:-$HOME/.cache/oddmaster}/dotnet"
else
  DOTNET_ROOT="$(cd "$(dirname "$(command -v dotnet)")" && pwd)"
fi
export DOTNET_ROOT
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1

SSL_LIB="${ODD_OPENSSL_DIR:-${ODD_SDK_CACHE:-$HOME/.cache/oddmaster}/openssl/lib}"
if [ -f "$SSL_LIB/libssl.so.1.1" ]; then
  export LD_LIBRARY_PATH="$SSL_LIB${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

# .NET Core 3.1 ищет libicuuc.so.<версия> только до 67; на Debian 12 стоит ICU 72.
if [ -z "${CLR_ICU_VERSION_OVERRIDE:-}" ]; then
  ICU_VER="$(ldconfig -p 2>/dev/null | grep -o 'libicuuc.so.[0-9]\+' | head -1 | cut -d. -f3)"
  if [ -n "${ICU_VER:-}" ]; then export CLR_ICU_VERSION_OVERRIDE="$ICU_VER"; fi
fi
