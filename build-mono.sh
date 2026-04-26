#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$ROOT"

mkdir -p lib/DirectXWraper/Release
mcs \
  -target:library \
  -out:lib/DirectXWraper/Release/DirectXWrapper.dll \
  -r:System.Drawing \
  -r:System.Windows.Forms \
  lib/DirectXWraper/ManagedDirectXWrapper.cs

msbuild tools/Driver/Driver.2008.csproj \
  /p:Configuration=Debug \
  /p:Platform=AnyCPU \
  /v:minimal

rsync -a --exclude-from=excludelist.txt core/res/ bin/Debug/res/
rsync -a --exclude-from=excludelist.txt plugins/ bin/Debug/plugins/
rsync -a --exclude-from=excludelist.txt doc/ bin/Debug/
