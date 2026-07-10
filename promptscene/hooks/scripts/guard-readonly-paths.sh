#!/usr/bin/env bash
# guard-readonly-paths.sh — PreToolUse 훅
# 목적: "패키지 = 토대의 일부 = 읽기 전용" 가드레일을 기계적으로 집행.
# 차단: Library/PackageCache/**, Packages/manifest.json, Packages/packages-lock.json
# 동작: 종료 코드 2 = 도구 호출 차단 (stderr가 Claude에게 전달), 0 = 허용.

set -u

INPUT="$(cat)"

# python3 는 일부 환경(예: Windows Store 스텁)에서 실제 인터프리터가 아니므로
# JSON 파싱이 실제로 되는 첫 인터프리터를 탐지한다.
PY=""
for _c in python3 python py; do
  command -v "$_c" >/dev/null 2>&1 || continue
  if [ "$(printf '{"__probe__":"ok"}' | "$_c" -c 'import json,sys;print(json.load(sys.stdin)["__probe__"])' 2>/dev/null)" = "ok" ]; then
    PY="$_c"; break
  fi
done
# 파싱기를 못 찾으면 도구를 막지 않고 통과시키되(fail-open) 경고를 남긴다.
[ -z "$PY" ] && { echo "경고(가드레일): JSON 파서(python)를 찾지 못해 읽기 전용 검사를 건너뜁니다." >&2; exit 0; }

json_get() {
  printf '%s' "$INPUT" | "$PY" -c "
import json, sys
try:
    d = json.load(sys.stdin)
except Exception:
    sys.exit(0)
cur = d
for key in '$1'.split('.'):
    cur = cur.get(key, {}) if isinstance(cur, dict) else {}
print(cur if isinstance(cur, str) else '')
" 2>/dev/null
}

TOOL_NAME="$(json_get tool_name)"
READONLY_RE='Library/PackageCache|Packages/manifest\.json|Packages/packages-lock\.json'

case "$TOOL_NAME" in
  Write|Edit|MultiEdit|NotebookEdit)
    FILE_PATH="$(json_get tool_input.file_path)"
    [ -z "$FILE_PATH" ] && FILE_PATH="$(json_get tool_input.notebook_path)"
    # 경로 구분자 정규화: Windows 백슬래시(\)를 슬래시(/)로 치환한 뒤 슬래시 정규식에 매칭.
    # 이렇게 입구에서 한 번만 정규화하면 READONLY_RE 패턴을 추가할 때 [/\\]를 매번
    # 넣을 필요가 없어 같은 구멍이 구조적으로 재발하지 않는다.
    FILE_PATH="${FILE_PATH//\\//}"
    if printf '%s' "$FILE_PATH" | grep -qE "$READONLY_RE"; then
      echo "차단(가드레일): '$FILE_PATH' 는 읽기 전용입니다. PackageCache와 패키지 매니페스트는 SYSTEMS 토대의 일부이므로 수정 금지 — 수정 대신 사용자에게 보고하고 지시를 기다리세요." >&2
      exit 2
    fi
    ;;
  Bash)
    CMD="$(json_get tool_input.command)"
    # FILE_PATH와 동일한 이유로 command 문자열도 입구에서 백슬래시→슬래시 정규화.
    CMD="${CMD//\\//}"
    if printf '%s' "$CMD" | grep -qE "$READONLY_RE"; then
      if printf '%s' "$CMD" | grep -qE '(^|[;&|[:space:]])(rm|mv|cp|sed[[:space:]]+-i|tee|chmod|chown|truncate|ln)([[:space:]]|$)' \
         || printf '%s' "$CMD" | grep -qE '>>?[[:space:]]*[^[:space:]]*(PackageCache|manifest\.json|packages-lock\.json)'; then
        echo "차단(가드레일): PackageCache/패키지 매니페스트를 변조할 수 있는 셸 명령입니다. 읽기(cat/grep/ls)는 허용, 쓰기·삭제·이동은 금지입니다." >&2
        exit 2
      fi
    fi
    ;;
esac

exit 0
