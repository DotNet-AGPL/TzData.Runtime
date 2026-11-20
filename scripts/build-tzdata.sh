#!/usr/bin/env bash
#
# Build the app-local tzdata tree from an IANA release.
#
#   ./scripts/build-tzdata.sh [version] [--verify]
#
#   version   IANA release, e.g. 2026f (default: from Directory.Build.props)
#   --verify  rebuild into a temp dir and diff against the committed tzdata/ tree
#
# Requires: curl, tar, sha512sum, zic (and optionally zdump for the sanity check).
# The script pins the SHA-512 of every supported release; for a new release set
# TZDATA_SHA512 (get it from https://data.iana.org/time-zones/releases/).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="${ROOT}/tzdata"

VER="${1:-}"
if [ -z "${VER}" ]; then
  VER="$(sed -n 's/.*<TzDataVersion[^>]*>\([^<]*\)<.*/\1/p' "${ROOT}/Directory.Build.props" | head -1)"
fi
MODE="${2:-build}"

case "${VER}" in
  2026f) SHA512="26189803c8b3e903dcc467404e2feefb7d190cb1870249a23577ce31e9dcbf187e4704e0b45c8d1366d1c501bb5b53a2e80f8e2828b2163be15e532f6acab52d" ;;
  *)
    if [ -z "${TZDATA_SHA512:-}" ]; then
      echo "Unknown tzdata release '${VER}'. Set TZDATA_SHA512 to its SHA-512." >&2
      exit 1
    fi
    SHA512="${TZDATA_SHA512}"
    ;;
esac

URL="https://data.iana.org/time-zones/releases/tzdata${VER}.tar.gz"
WORK="$(mktemp -d)"
trap 'rm -rf "${WORK}"' EXIT

echo "Downloading tzdata ${VER}: ${URL}"
curl -fsSL --retry 3 -o "${WORK}/tzdata.tar.gz" "${URL}"
echo "${SHA512}  ${WORK}/tzdata.tar.gz" | sha512sum -c -

mkdir -p "${WORK}/src" "${WORK}/out"
tar xzf "${WORK}/tzdata.tar.gz" -C "${WORK}/src"

ZIC="$(command -v zic || true)"
if [ -z "${ZIC}" ] && [ -x /usr/sbin/zic ]; then ZIC=/usr/sbin/zic; fi
if [ -z "${ZIC}" ]; then
  echo "zic is required (Debian: apt-get install tzdata)." >&2
  exit 1
fi

# Compile the canonical TDATA set directly (the same inputs the tzdata Makefile uses:
# YDATA + NDATA + BACKWARD). Running `make tzdata.zi` would additionally require the
# tzcode tarball (asctime.c) just to format a version string.
echo "Compiling with ${ZIC}"
(
  cd "${WORK}/src"
  "${ZIC}" -b fat -d "${WORK}/out" \
    africa antarctica asia australasia europe northamerica southamerica etcetera factory backward
)

# zone.tab drives TimeZoneInfo.GetSystemTimeZones(); the other two are useful metadata.
cp "${WORK}/src/zone.tab" "${WORK}/src/zone1970.tab" "${WORK}/src/iso3166.tab" "${WORK}/out/"

# zic may emit symlinks for Link entries; dereference them so the git tree is portable
# (Windows checkouts do not preserve symlinks reliably).
find "${WORK}/out" -type l | while IFS= read -r link; do
  target="$(readlink -f "${link}")"
  rm -f "${link}"
  cp "${target}" "${link}"
done

for zone in Europe/Kyiv Asia/Shanghai America/New_York Etc/UTC; do
  [ -f "${WORK}/out/${zone}" ] || { echo "Missing generated zone: ${zone}" >&2; exit 1; }
done

ZDUMP="$(command -v zdump || true)"
if [ -n "${ZDUMP}" ]; then
  "${ZDUMP}" -v -c 2026,2027 "${WORK}/out/Europe/Kyiv" | grep -q "2026" || {
    echo "zdump sanity check failed" >&2
    exit 1
  }
fi

if [ "${MODE}" = "--verify" ] || [ "${MODE}" = "verify" ]; then
  diff -ru "${DEST}" "${WORK}/out"
  echo "tzdata ${VER} verified"
else
  rm -rf "${DEST}"
  mkdir -p "${DEST}"
  cp -a "${WORK}/out/." "${DEST}/"
  echo "tzdata ${VER} written to ${DEST}"
  du -sh "${DEST}"
fi
