#!/usr/bin/env bash
# test-guard-readonly-paths.sh — guard-readonly-paths.sh 단위 테스트
# 실제 훅 스크립트에 BOM 없는 JSON을 stdin으로 넣고 exit code를 기대값과 대조한다.
#   exit 2 = 차단(BLOCK), exit 0 = 허용(ALLOW)
# 사용: bash test-guard-readonly-paths.sh   (전 케이스 PASS 시 0, 하나라도 FAIL 시 1)

set -u

HERE="$(cd "$(dirname "$0")" && pwd)"
HOOK="$HERE/guard-readonly-paths.sh"

PASS=0
FAIL=0

printf '%-4s | %-52s | %-5s | %-6s | %s\n' "그룹" "케이스" "기대" "실제" "결과"
printf '%s\n' "-----+------------------------------------------------------+-------+--------+------"

run() {
  local group="$1" name="$2" expected="$3" json="$4"
  printf '%s' "$json" | bash "$HOOK" >/dev/null 2>&1
  local actual=$?
  local verdict
  if [ "$actual" = "$expected" ]; then verdict="PASS"; PASS=$((PASS+1)); else verdict="FAIL"; FAIL=$((FAIL+1)); fi
  printf '%-4s | %-52s | %-5s | %-6s | %s\n' "$group" "$name" "$expected" "$actual" "$verdict"
}

# ===== 기존 6케이스 (베이스라인) =====
run "기존" "Edit 슬래시 PackageCache" 2 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:/J_0/XRCollabDemo/Library/PackageCache/a.cs"}}'
run "기존" "Edit 백슬래시 PackageCache (수정 대상 버그)" 2 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:\\J_0\\XRCollabDemo\\Library\\PackageCache\\a.cs"}}'
run "기존" "Edit 안전 경로(Assets, 백슬래시)" 0 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:\\J_0\\XRCollabDemo\\Assets\\PromptScene\\Foo.cs"}}'
run "기존" "Edit Packages/manifest.json" 2 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:/J_0/XRCollabDemo/Packages/manifest.json"}}'
run "기존" "Bash rm PackageCache (백슬래시)" 2 \
  '{"tool_name":"Bash","tool_input":{"command":"rm c:\\J_0\\XRCollabDemo\\Library\\PackageCache\\x.dll"}}'
run "기존" "Bash 안전 명령(무관 경로)" 0 \
  '{"tool_name":"Bash","tool_input":{"command":"ls c:\\J_0\\XRCollabDemo\\Assets"}}'

# ===== 신규 4항목 (요청) =====
# ① 백슬래시 절대경로 Edit → 차단
run "신규①" "Edit 백슬래시 절대경로 PackageCache" 2 \
  '{"tool_name":"Edit","tool_input":{"file_path":"C:\\Unity\\DeepChairProject\\Library\\PackageCache\\pkg\\y.cs"}}'
# ② 소문자/대문자 드라이브 혼용 각 1개 → 차단
run "신규②" "Edit 소문자 드라이브 c:\\ (백슬래시)" 2 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:\\proj\\Library\\PackageCache\\z.cs"}}'
run "신규②" "Edit 대문자 드라이브 C:\\ (백슬래시)" 2 \
  '{"tool_name":"Edit","tool_input":{"file_path":"C:\\proj\\Library\\PackageCache\\z.cs"}}'
# ③ 백슬래시 경로 대상 Bash >> 리다이렉트 → 차단
run "신규③" "Bash >> 리다이렉트 (백슬래시 PackageCache)" 2 \
  '{"tool_name":"Bash","tool_input":{"command":"echo hi >> c:\\J_0\\XRCollabDemo\\Library\\PackageCache\\log.txt"}}'
# ④ 백슬래시 경로 cat 읽기 → 허용
run "신규④" "Bash cat 읽기 (백슬래시 PackageCache)" 0 \
  '{"tool_name":"Bash","tool_input":{"command":"cat c:\\J_0\\XRCollabDemo\\Library\\PackageCache\\a.cs"}}'

printf '%s\n' "-----+------------------------------------------------------+-------+--------+------"
printf '합계: PASS=%d FAIL=%d\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ]
