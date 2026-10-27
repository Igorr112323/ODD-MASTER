#!/usr/bin/env bash
# Резервный способ получить .NET SDK в среде без доступа к dot.net / NuGet.
# Обычному пользователю (и CI с нормальной сетью) этот скрипт не нужен:
# ставьте .NET 8 обычным установщиком, global.json позволяет более новый SDK.
set -euo pipefail

CACHE="${ODD_SDK_CACHE:-$HOME/.cache/oddmaster}"
SDK_DIR="$CACHE/dotnet"
SSL_DIR="$CACHE/openssl/lib"
NPM_TARBALL="https://registry.npmjs.org/dotnet-sdk-3.1-linux-x64/-/dotnet-sdk-3.1-linux-x64-3.1.1007.tgz"
OPENSSL_TARBALL="https://codeload.github.com/openssl/openssl/tar.gz/refs/tags/OpenSSL_1_1_1w"

mkdir -p "$CACHE"

if [ ! -x "$SDK_DIR/dotnet" ]; then
  echo "Скачиваю .NET Core SDK 3.1.102 из зеркала npm (доступно в ограниченной сети)..."
  curl -fsSL "$NPM_TARBALL" -o "$CACHE/dotnet-sdk.tgz"
  mkdir -p "$SDK_DIR"
  tar xzf "$CACHE/dotnet-sdk.tgz" -C "$SDK_DIR" --strip-components=1
fi

# .NET Core 3.1 умеет работать только с OpenSSL 1.1. На Debian 12+/Ubuntu 22.04+
# его нет, поэтому при необходимости собираем его из исходников GitHub.
if ! ldconfig -p 2>/dev/null | grep -q 'libssl.so.1.1'; then
  if [ ! -f "$SSL_DIR/libssl.so.1.1" ]; then
    echo "Собираю OpenSSL 1.1.1w (нужен .NET Core 3.1; занимает несколько минут)..."
    curl -fsSL "$OPENSSL_TARBALL" -o "$CACHE/openssl.tar.gz"
    rm -rf "$CACHE/openssl-src"
    mkdir -p "$CACHE/openssl-src"
    tar xzf "$CACHE/openssl.tar.gz" -C "$CACHE/openssl-src" --strip-components=1
    (cd "$CACHE/openssl-src" \
      && ./config --prefix="$CACHE/openssl" --openssldir="$CACHE/openssl/ssl" shared no-asm no-tests >/dev/null \
      && make -j"$(nproc)" >/dev/null \
      && make install_sw >/dev/null)
  fi
fi

echo "Готово. Используйте: source scripts/dotnet-env.sh"
