#!/usr/bin/env bash
# guard-readonly-paths.sh — PreToolUse 훅
# 목적: "패키지 = 토대의 일부 = 읽기 전용" 가드레일을 기계적으로 집행.
# 차단: Library/PackageCache/**, Packages/manifest.json, Packages/packages-lock.json
# 동작: 종료 코드 2 = 도구 호출 차단 (stderr가 Claude에게 전달), 0 = 허용.
#
# ── 성역 ② 디자인 토큰: **/HudTheme.cs ─────────────────────────────────────────────────────
# HudTheme.cs는 HUD 디자인 토큰의 SSOT다. "성역"을 주석으로만 지키면 언젠가 뚫리므로 기계로 막는다.
#   · Write/Edit/MultiEdit/NotebookEdit → 플러그인 사본이든 studio 사본이든 **차단**.
#   · Bash 변조(sed -i / tee / > 리다이렉트 / rm / mv / truncate / ln / chmod) → **차단**.
#   · Bash `cp` **플러그인 assets → studio** 방향 → **허용**. 이게 SKILL.md Phase 1b(항상 덮어쓰기)의
#     설치 경로다. 막아버리면 스킬 자체가 못 돌아간다.
#   · Bash `cp` **studio → 플러그인 assets**(역방향) → **차단**. 복사는 단방향이고, 역방향 복사가
#     바로 드리프트가 SSOT로 승격되는 경로다.
#   · 읽기(cat/grep/ls/diff)는 전부 허용.
#
# ⚠ 탈출구(토큰을 정상적으로 바꾸는 유일한 길) — 막아놓고 못 고치면 안 되므로 명시한다:
#     환경변수 PROMPTSCENE_ALLOW_THEME_EDIT=1 이면 이 검사만 건너뛴다.
#   훅은 Claude Code의 환경을 물려받으므로 **에이전트가 호출 단위로 켤 수 없다** — settings.json 의
#   `env` 에 사람이 넣어야 한다. 즉 토큰 변경은 구조적으로 사람 승인을 거친다(의도된 마찰).
#   절차: ① settings.json env 에 플래그 추가 → ② 세션 재시작 → ③ **플러그인 assets 쪽** HudTheme.cs 수정
#         → ④ Phase 1b 재실행(studio 사본 덮어쓰기) → ⑤ 플래그 제거.
#   (PackageCache 성역에는 탈출구가 없다 — 그쪽은 절대 수정 대상이 아니다.)

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
# 파싱기를 못 찾으면 검사를 포기하지 않는다 — 파서 없이 원문(raw payload)만 훑는 저해상도 검사로
# 내려간다(아래 RAW_ONLY). 예전엔 여기서 exit 0(fail-open)이었고, 그게 실전에서 가드가 조용히
# 무력화된 3건 중 하나였다.
RAW_ONLY=0
[ -z "$PY" ] && { echo "경고(가드레일): JSON 파서(python)를 찾지 못해 원문 스캔으로 대체합니다(정밀도 낮음, 차단은 유지)." >&2; RAW_ONLY=1; }

json_get() {
  [ "$RAW_ONLY" = "1" ] && { printf ''; return; }
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

# ⚠ FAIL-CLOSED 백스톱.
# json_get 이 빈 문자열을 주는 경우는 두 가지다: ① 필드가 정말 없다 ② **파싱이 깨졌다**.
# ②가 위험하다 — 예: Windows 경로가 `c:\J_0\...` 처럼 이스케이프 안 된 채로 오면 `\J`/`\A` 는 유효한
# JSON 이스케이프가 아니라서 json.load 가 던지고, 예전 코드는 그걸 "경로 없음"으로 취급해 통과시켰다
# (실측 확인된 fail-open). 그래서 파싱된 경로가 비면 **원문 전체를 정규화해서 다시 훑는다.**
# 정상(유효 JSON) 경로에서는 이 백스톱이 발동하지 않으므로 오차단 위험이 없다.
RAW_NORM="${INPUT//\\//}"     # 원문도 백슬래시→슬래시 정규화 (입구 정규화 원칙과 동일)

# 파싱된 값이 비었을 때만 원문을 대신 쓴다.
fallback_raw() { [ -n "$1" ] && printf '%s' "$1" || printf '%s' "$RAW_NORM"; }

TOOL_NAME="$(json_get tool_name)"
# 파서가 죽었으면 tool_name 도 모른다 → 원문에서 도구명을 건져 분기를 유지한다.
if [ -z "$TOOL_NAME" ]; then
  case "$RAW_NORM" in
    *'"tool_name":"Bash"'*|*'"tool_name": "Bash"'*) TOOL_NAME="Bash" ;;
    *'"tool_name":"Write"'*|*'"tool_name": "Write"'*) TOOL_NAME="Write" ;;
    *'"tool_name":"MultiEdit"'*|*'"tool_name": "MultiEdit"'*) TOOL_NAME="MultiEdit" ;;
    *'"tool_name":"NotebookEdit"'*|*'"tool_name": "NotebookEdit"'*) TOOL_NAME="NotebookEdit" ;;
    *'"tool_name":"Edit"'*|*'"tool_name": "Edit"'*) TOOL_NAME="Edit" ;;
  esac
fi
READONLY_RE='Library/PackageCache|Packages/manifest\.json|Packages/packages-lock\.json'

# 성역 ② 디자인 토큰. 파일명으로 잡으므로 플러그인 사본·studio 사본·미래의 추가 사본을 전부 덮는다.
THEME_RE='/HudTheme\.cs'
THEME_SRC_DIR='skills/cross-platform-ui/assets'               # 단방향 복사의 출발지(= SSOT)
THEME_DST_DIR='ContentLogic/PromptScene/UI'                   # 단방향 복사의 도착지(= studio 사본)
# 역방향 복사 = cp 뒤에 (도착지 파일) … (출발지 디렉터리) 순서로 나타나는 경우.
THEME_REVERSE_RE="cp[^;&|]*${THEME_DST_DIR}/HudTheme\\.cs[^;&|]*${THEME_SRC_DIR}"
THEME_EXEMPT="${PROMPTSCENE_ALLOW_THEME_EDIT:-0}"

case "$TOOL_NAME" in
  Write|Edit|MultiEdit|NotebookEdit)
    FILE_PATH="$(json_get tool_input.file_path)"
    [ -z "$FILE_PATH" ] && FILE_PATH="$(json_get tool_input.notebook_path)"
    # 경로 구분자 정규화: Windows 백슬래시(\)를 슬래시(/)로 치환한 뒤 슬래시 정규식에 매칭.
    # 이렇게 입구에서 한 번만 정규화하면 READONLY_RE 패턴을 추가할 때 [/\\]를 매번
    # 넣을 필요가 없어 같은 구멍이 구조적으로 재발하지 않는다.
    FILE_PATH="${FILE_PATH//\\//}"
    # 파싱 실패(빈 값)면 원문 전체를 대신 검사한다 — fail-closed 백스톱.
    FILE_PATH="$(fallback_raw "$FILE_PATH")"
    if printf '%s' "$FILE_PATH" | grep -qE "$READONLY_RE"; then
      echo "차단(가드레일): '$FILE_PATH' 는 읽기 전용입니다. PackageCache와 패키지 매니페스트는 SYSTEMS 토대의 일부이므로 수정 금지 — 수정 대신 사용자에게 보고하고 지시를 기다리세요." >&2
      exit 2
    fi
    if [ "$THEME_EXEMPT" != "1" ] && printf '%s' "$FILE_PATH" | grep -qE "$THEME_RE"; then
      echo "차단(가드레일): '$FILE_PATH' 는 디자인 토큰의 SSOT(성역)입니다. 리터럴 색·px를 코드에 쓰지 말고, 새 값이 필요하면 **토큰을 제안하고 정지**하세요. studio 사본은 Phase 1b가 항상 덮어쓰므로 편집해도 사라집니다. 정상 변경 절차는 guard-readonly-paths.sh 헤더의 탈출구(PROMPTSCENE_ALLOW_THEME_EDIT)를 참고하세요." >&2
      exit 2
    fi
    ;;
  Bash)
    CMD="$(json_get tool_input.command)"
    # FILE_PATH와 동일한 이유로 command 문자열도 입구에서 백슬래시→슬래시 정규화.
    CMD="${CMD//\\//}"
    # 파싱 실패(빈 값)면 원문 전체를 대신 검사한다 — fail-closed 백스톱.
    CMD="$(fallback_raw "$CMD")"
    if printf '%s' "$CMD" | grep -qE "$READONLY_RE"; then
      # 선행 문자 클래스에 `"` 를 포함시키는 이유: fail-closed 백스톱이 발동하면 검사 대상이 JSON 원문이라
      # 명령이 `"rm ...` 처럼 따옴표 바로 뒤에 온다. 이걸 빼면 백스톱이 있어도 변조가 안 잡힌다(실측 FAIL).
      if printf '%s' "$CMD" | grep -qE '(^|[;&|"[:space:]])(rm|mv|cp|sed[[:space:]]+-i|tee|chmod|chown|truncate|ln)([[:space:]]|$)' \
         || printf '%s' "$CMD" | grep -qE '>>?[[:space:]]*[^[:space:]]*(PackageCache|manifest\.json|packages-lock\.json)'; then
        echo "차단(가드레일): PackageCache/패키지 매니페스트를 변조할 수 있는 셸 명령입니다. 읽기(cat/grep/ls)는 허용, 쓰기·삭제·이동은 금지입니다." >&2
        exit 2
      fi
    fi
    if [ "$THEME_EXEMPT" != "1" ] && printf '%s' "$CMD" | grep -qE "$THEME_RE"; then
      # ① 역방향 복사(studio → 플러그인 assets): 드리프트가 SSOT로 승격되는 경로 → 차단.
      if printf '%s' "$CMD" | grep -qE "$THEME_REVERSE_RE"; then
        echo "차단(가드레일): HudTheme.cs 복사는 **단방향(플러그인 assets → studio)** 입니다. 역방향 복사는 studio 쪽 드리프트를 SSOT로 승격시킵니다. studio 사본은 버리고 플러그인 assets 파일을 고치세요." >&2
        exit 2
      fi
      # ② 변조 명령(cp 제외 — cp는 Phase 1b의 정식 설치 경로) → 차단.
      if printf '%s' "$CMD" | grep -qE '(^|[;&|"[:space:]])(rm|mv|sed[[:space:]]+-i|tee|chmod|chown|truncate|ln)([[:space:]]|$)' \
         || printf '%s' "$CMD" | grep -qE '>>?[[:space:]]*[^[:space:]]*HudTheme\.cs'; then
        echo "차단(가드레일): HudTheme.cs(디자인 토큰 SSOT)를 변조할 수 있는 셸 명령입니다. 읽기(cat/grep/ls/diff)와 단방향 설치 복사(cp 플러그인→studio)만 허용됩니다. 정상 변경은 헤더의 탈출구 절차를 따르세요." >&2
        exit 2
      fi
    fi
    ;;
esac

exit 0
