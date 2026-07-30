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

# ===== 성역 ② 디자인 토큰 HudTheme.cs =====
# 검증 방식 = **차단 재현**. 통과 테스트로는 fail-open을 못 잡는다(python 스텁·세션 스코프·백슬래시
# 3건 전부 "통과 테스트"는 성공했으나 실전에서 fail-open이었다). 그래서 아래는 "정말 exit 2가 나오는가"를
# 슬래시·백슬래시 **양쪽 경로**로 재현하는 데 무게를 둔다.
PLUG_S='c:/J_0/promptscene/skills/cross-platform-ui/assets/HudTheme.cs'
PLUG_B='c:\\J_0\\promptscene\\skills\\cross-platform-ui\\assets\\HudTheme.cs'
STUD_S='c:/J_0/XumFlow-studio/Assets/App/Scripts/ContentLogic/PromptScene/UI/HudTheme.cs'
STUD_B='c:\\J_0\\XumFlow-studio\\Assets\\App\\Scripts\\ContentLogic\\PromptScene\\UI\\HudTheme.cs'

# ⓐ 플러그인 사본(SSOT) 직접 편집 → 차단 (슬래시/백슬래시)
run "토큰ⓐ" "Edit 플러그인 HudTheme (슬래시)" 2 \
  "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$PLUG_S\"}}"
run "토큰ⓐ" "Edit 플러그인 HudTheme (백슬래시)" 2 \
  "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$PLUG_B\"}}"
# ⓑ studio 사본 편집 → 차단 (슬래시/백슬래시) — Phase 1b가 덮어쓰므로 편집 자체가 무의미+위험
run "토큰ⓑ" "Edit studio HudTheme (슬래시)" 2 \
  "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$STUD_S\"}}"
run "토큰ⓑ" "Write studio HudTheme (백슬래시)" 2 \
  "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$STUD_B\"}}"
# ⓒ Bash 변조 → 차단 (슬래시/백슬래시)
run "토큰ⓒ" "Bash sed -i HudTheme (백슬래시)" 2 \
  "{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"sed -i s/62/99/ $STUD_B\"}}"
run "토큰ⓒ" "Bash > 리다이렉트 HudTheme (슬래시)" 2 \
  "{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"echo x > $PLUG_S\"}}"
run "토큰ⓒ" "Bash rm HudTheme (백슬래시)" 2 \
  "{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"rm $PLUG_B\"}}"
# ⓓ 역방향 복사(studio → 플러그인) → 차단. 드리프트가 SSOT로 승격되는 경로.
run "토큰ⓓ" "Bash 역방향 cp studio→플러그인 (슬래시)" 2 \
  "{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"cp $STUD_S c:/J_0/promptscene/skills/cross-platform-ui/assets/HudTheme.cs\"}}"
run "토큰ⓓ" "Bash 역방향 cp studio→플러그인 (백슬래시)" 2 \
  "{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"cp $STUD_B $PLUG_B\"}}"
# ⓔ 남겨둔 길: 정방향 설치 복사(Phase 1b) → 허용. 막아놓고 못 고치면 안 된다.
run "토큰ⓔ" "Bash 정방향 cp 플러그인→studio (허용)" 0 \
  "{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"cp -f $PLUG_S $STUD_S\"}}"
run "토큰ⓔ" "Bash 정방향 cp 백슬래시 (허용)" 0 \
  "{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"cp -f $PLUG_B $STUD_B\"}}"
run "토큰ⓔ" "Bash cat 읽기 HudTheme (허용)" 0 \
  "{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"cat $PLUG_B\"}}"
# ⓕ 과잉 차단 방지: 이름이 비슷한/이웃한 파일은 계속 편집 가능해야 한다
run "토큰ⓕ" "Edit 이웃 CrossPlatformRoomHud.cs (허용)" 0 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:\\J_0\\promptscene\\skills\\cross-platform-ui\\assets\\CrossPlatformRoomHud.cs"}}'
run "토큰ⓕ" "Edit 유사명 HudThemeEditor.cs (허용)" 0 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:\\J_0\\promptscene\\skills\\cross-platform-ui\\assets\\HudThemeEditor.cs"}}'

# ===== fail-open 백스톱: 깨진 JSON =====
# 2026-07-30 실측 발견. Windows 경로가 이스케이프 안 된 채(`c:\J_0\...`) 오면 `\J`/`\A` 는 유효한 JSON
# 이스케이프가 아니라서 json.load 가 던지고, 예전 코드는 그걸 "경로 없음"으로 취급해 **통과**시켰다.
# 슬래시/이스케이프 정상 케이스만 테스트하면 절대 안 잡히는 구멍이라 별도 그룹으로 못박는다.
# (아래 JSON들은 의도적으로 깨져 있다 — 작은따옴표로 감싸 셸이 손대지 못하게 한다.)
run "깨짐" 'Edit 이스케이프 안 된 경로 HudTheme → 차단' 2 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:\J_0\XumFlow-studio\Assets\App\Scripts\ContentLogic\PromptScene\UI\HudTheme.cs"}}'
run "깨짐" 'Edit 이스케이프 안 된 경로 PackageCache → 차단' 2 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:\J_0\XRCollabDemo\Library\PackageCache\a.cs"}}'
run "깨짐" 'Bash 이스케이프 안 된 rm PackageCache → 차단' 2 \
  '{"tool_name":"Bash","tool_input":{"command":"rm c:\J_0\XRCollabDemo\Library\PackageCache\x.dll"}}'
run "깨짐" 'JSON 자체가 잘림 → 차단(보호 경로 언급 시)' 2 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:/x/Library/PackageCache/a.cs"'
run "깨짐" '깨진 JSON + 무관 경로 → 허용(오차단 아님)' 0 \
  '{"tool_name":"Edit","tool_input":{"file_path":"c:\J_0\XumFlow-studio\Assets\App\Foo.cs"}}'

printf '%s\n' "-----+------------------------------------------------------+-------+--------+------"
printf '합계: PASS=%d FAIL=%d\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ]
