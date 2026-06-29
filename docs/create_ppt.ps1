# OXR-SDK AI Ready PPT 생성 스크립트
$OutputPath = "c:\J_0\docs\OXR-SDK-AI-Ready.pptx"

$ppt = New-Object -ComObject PowerPoint.Application
$ppt.Visible = [Microsoft.Office.Core.MsoTriState]::msoTrue

$presentation = $ppt.Presentations.Add()

# Slide dimensions (widescreen 16:9)
$presentation.PageSetup.SlideWidth  = 960
$presentation.PageSetup.SlideHeight = 540

# ─────────── 색상 상수 ───────────
$DARK_BG   = 0x1A1A2E   # 진한 남색
$ACCENT    = 0x00B4D8   # 청록 강조
$ACCENT2   = 0x48CAE4   # 연청록
$WHITE     = 0xFFFFFF
$LIGHT_BG  = 0x16213E   # 중간 남색
$CARD_BG   = 0x0F3460   # 카드 배경
$GREEN_OK  = 0x06D6A0   # 성공 초록
$YELLOW    = 0xFFD166   # 경고/포인트 노랑

# ─────────── 헬퍼 함수 ───────────
function Add-Slide($layout = 12) {
    $s = $presentation.Slides.Add($presentation.Slides.Count + 1, $layout)
    # 레이아웃 12 = ppLayoutBlank
    return $s
}

function Set-BG($slide, $color) {
    $slide.Background.Fill.ForeColor.RGB = $color
    $slide.Background.Fill.Solid()
}

function Add-Rect($slide, $left, $top, $width, $height, $fillColor, $lineColor = -1) {
    $shape = $slide.Shapes.AddShape(1, $left, $top, $width, $height)  # msoShapeRectangle=1
    $shape.Fill.ForeColor.RGB = $fillColor
    $shape.Fill.Solid()
    if ($lineColor -eq -1) {
        $shape.Line.Visible = [Microsoft.Office.Core.MsoTriState]::msoFalse
    } else {
        $shape.Line.ForeColor.RGB = $lineColor
        $shape.Line.Weight = 1.5
    }
    return $shape
}

function Add-TextBox($slide, $text, $left, $top, $width, $height, $fontSize, $color, $bold = $false, $align = 1) {
    # align: 1=Left, 2=Center, 3=Right
    $tb = $slide.Shapes.AddTextbox(1, $left, $top, $width, $height)
    $tf = $tb.TextFrame
    $tf.WordWrap = [Microsoft.Office.Core.MsoTriState]::msoTrue
    $tf.AutoSize = 0
    $r = $tf.TextRange
    $r.Text = $text
    $r.Font.Size = [float]$fontSize
    $r.Font.Color.RGB = [int]$color
    if ($bold) { $r.Font.Bold = [Microsoft.Office.Core.MsoTriState]::msoTrue }
    $r.ParagraphFormat.Alignment = $align
    return $tb
}

function Add-Label($slide, $text, $left, $top, $width, $height, $bgColor, $fgColor, $fontSize = 14) {
    $r = Add-Rect $slide $left $top $width $height $bgColor
    $r.Name = "LabelBG"
    Add-TextBox $slide $text $left $top $width $height $fontSize $fgColor $true 2 | Out-Null
}

# ═══════════════════════════════════════════
# SLIDE 1 — 타이틀
# ═══════════════════════════════════════════
$s1 = Add-Slide
Set-BG $s1 $DARK_BG

# 상단 강조 바
Add-Rect $s1 0 0 960 6 $ACCENT | Out-Null

# 배경 장식 원
$circ = $s1.Shapes.AddShape(9, 620, 180, 360, 360)  # msoShapeOval=9
$circ.Fill.ForeColor.RGB = $LIGHT_BG
$circ.Fill.Solid()
$circ.Line.Visible = [Microsoft.Office.Core.MsoTriState]::msoFalse

# 배지
Add-Label $s1 "OXR-SDK" 60 60 140 36 $ACCENT $DARK_BG 15

# 메인 타이틀
Add-TextBox $s1 "AI Ready" 60 115 700 100 72 $WHITE $true 1 | Out-Null
Add-TextBox $s1 "검증 완료" 60 215 700 80 52 $ACCENT $true 1 | Out-Null

# 서브타이틀
Add-TextBox $s1 "자연어 프롬프트로 XR 룸을 합성·검증하는 시스템," 60 310 700 30 18 0xB0C4DE $false 1 | Out-Null
Add-TextBox $s1 "XRCollabDemo에서 End-to-End 실측 확인" 60 340 700 30 18 0xB0C4DE $false 1 | Out-Null

# 날짜/버전
Add-TextBox $s1 "2026-06-12   |   XRCollabDemo Unity 6000.3.11f1" 60 490 860 30 13 0x607080 $false 1 | Out-Null

# 하단 바
Add-Rect $s1 0 534 960 6 $ACCENT2 | Out-Null

# ═══════════════════════════════════════════
# SLIDE 2 — 한 줄 요약
# ═══════════════════════════════════════════
$s2 = Add-Slide
Set-BG $s2 $DARK_BG
Add-Rect $s2 0 0 960 6 $ACCENT | Out-Null

Add-TextBox $s2 "한 줄 요약" 40 20 880 40 14 $ACCENT $true 1 | Out-Null

# 큰 인용 박스
$qBox = Add-Rect $s2 40 70 880 200 $CARD_BG
Add-TextBox $s2 """문서만 보고 작동하는 룸을 빌드""하는 것을 실제로 증명 완료" 60 90 840 160 28 $WHITE $true 2 | Out-Null

# 3개 포인트 카드
$cards = @(
    @{ x=40;  text="룸 베이스"; sub="네트워크 + 아바타 + UI 전환" },
    @{ x=340; text="콘텐츠 플러그인"; sub="IToggleableContent 아키텍처" },
    @{ x=640; text="Ruler 파일럿"; sub="첫 콘텐츠 기능 동작 확인" }
)
foreach ($c in $cards) {
    Add-Rect $s2 $c.x 295 280 190 $LIGHT_BG | Out-Null
    Add-Rect $s2 $c.x 295 280 4 $ACCENT | Out-Null
    Add-TextBox $s2 $c.text ($c.x + 10) 310 260 50 20 $ACCENT $true 1 | Out-Null
    Add-TextBox $s2 $c.sub ($c.x + 10) 360 260 80 15 0xB0C4DE $false 1 | Out-Null
}

Add-TextBox $s2 "다음: 런치패드 UI → 스킬화 → 자연어 합성+자동검증" 40 505 880 25 13 0x607080 $false 2 | Out-Null

# ═══════════════════════════════════════════
# SLIDE 3 — AI Ready 란?
# ═══════════════════════════════════════════
$s3 = Add-Slide
Set-BG $s3 $DARK_BG
Add-Rect $s3 0 0 960 6 $ACCENT | Out-Null

Add-TextBox $s3 "AI Ready 란?" 40 20 880 40 14 $ACCENT $true 1 | Out-Null
Add-TextBox $s3 "OXR-SDK가 LLM 에이전트의 도구가 된다" 40 55 880 50 28 $WHITE $true 1 | Out-Null

# 아키텍처 다이어그램 (텍스트 기반)
# 왼쪽: 사람/LLM
Add-Rect $s3 40 130 200 120 $CARD_BG | Out-Null
Add-TextBox $s3 "개발자 / LLM" 40 145 200 25 14 $ACCENT $true 2 | Out-Null
Add-TextBox $s3 "자연어 프롬프트" 40 175 200 25 13 $WHITE $false 2 | Out-Null
Add-TextBox $s3 """의자 측정 기능이" 40 198 200 20 11 0xB0C4DE $false 2 | Out-Null
Add-TextBox $s3 "있는 룸 만들어줘""  " 40 216 200 20 11 0xB0C4DE $false 2 | Out-Null

# 화살표 레이블
Add-TextBox $s3 "───▶" 248 175 80 30 18 $ACCENT2 $false 2 | Out-Null

# 가운데: Claude Code 스킬
Add-Rect $s3 335 130 290 120 $CARD_BG | Out-Null
Add-Rect $s3 335 130 290 4 $ACCENT | Out-Null
Add-TextBox $s3 "Claude Code Skill" 335 145 290 25 14 $ACCENT $true 2 | Out-Null
Add-TextBox $s3 "/promptscene" 335 172 290 22 13 $WHITE $false 2 | Out-Null
Add-TextBox $s3 "/RoomContent-*" 335 194 290 22 13 $WHITE $false 2 | Out-Null
Add-TextBox $s3 "MCP로 Unity 씬 조립 + 검증" 335 220 290 30 12 0xB0C4DE $false 2 | Out-Null

# 화살표
Add-TextBox $s3 "───▶" 633 175 60 30 18 $ACCENT2 $false 2 | Out-Null

# 오른쪽: Unity 런타임
Add-Rect $s3 700 130 220 120 $CARD_BG | Out-Null
Add-Rect $s3 700 130 220 4 $GREEN_OK | Out-Null
Add-TextBox $s3 "Unity Runtime" 700 145 220 25 14 $GREEN_OK $true 2 | Out-Null
Add-TextBox $s3 "RoomCore + Content" 700 172 220 22 13 $WHITE $false 2 | Out-Null
Add-TextBox $s3 "네트워크 + 아바타" 700 194 220 22 13 $WHITE $false 2 | Out-Null
Add-TextBox $s3 "실제 동작 확인" 700 220 220 22 12 $GREEN_OK $false 2 | Out-Null

# 하단 설명
Add-TextBox $s3 "두 층 구조" 40 280 120 25 13 $ACCENT $true 1 | Out-Null

$layers = @(
    @{ label="런타임 (Unity)"; desc="얇은 코어 + 옵트인 콘텐츠 모듈 + 런치패드" },
    @{ label="저작 (Claude Code)"; desc="MCP로 룸을 조립/검증하는 스킬 세트" }
)
$ly = 310
foreach ($l in $layers) {
    Add-Rect $s3 40 $ly 880 50 $LIGHT_BG | Out-Null
    Add-TextBox $s3 $l.label 50 ($ly+8) 200 32 14 $ACCENT $true 1 | Out-Null
    Add-TextBox $s3 $l.desc 260 ($ly+8) 650 32 13 $WHITE $false 1 | Out-Null
    $ly += 58
}

# ═══════════════════════════════════════════
# SLIDE 4 — 검증 방법: XRCollabDemo BasicRoom
# ═══════════════════════════════════════════
$s4 = Add-Slide
Set-BG $s4 $DARK_BG
Add-Rect $s4 0 0 960 6 $ACCENT | Out-Null

Add-TextBox $s4 "검증 방법: XRCollabDemo BasicRoom" 40 20 880 40 14 $ACCENT $true 1 | Out-Null
Add-TextBox $s4 "문서 한 장만으로 룸을 처음부터 재현" 40 55 880 40 24 $WHITE $true 1 | Out-Null

# 프로세스 플로우
$steps = @(
    @{ n="1"; title="문서 읽기"; desc="build-working-room.md 만 참조" },
    @{ n="2"; title="씬 조립"; desc="스포너·NM·씬전환 설정" },
    @{ n="3"; title="빌드·실행"; desc="Server exe + Editor Client" },
    @{ n="4"; title="실측 검증"; desc="서버 로그 + Unity 인스펙터" }
)
$sx = 40
foreach ($st in $steps) {
    $circle = $s4.Shapes.AddShape(9, $sx, 115, 60, 60)  # oval
    $circle.Fill.ForeColor.RGB = $ACCENT
    $circle.Fill.Solid()
    $circle.Line.Visible = [Microsoft.Office.Core.MsoTriState]::msoFalse
    Add-TextBox $s4 $st.n ($sx) 128 60 34 20 $DARK_BG $true 2 | Out-Null
    Add-TextBox $s4 $st.title ($sx - 20) 185 100 25 13 $WHITE $true 2 | Out-Null
    Add-TextBox $s4 $st.desc ($sx - 30) 208 120 40 11 0xB0C4DE $false 2 | Out-Null
    if ($sx -lt 700) {
        Add-TextBox $s4 "──▶" ($sx + 65) 132 60 28 18 0x607080 $false 1 | Out-Null
    }
    $sx += 215
}

# 결과 박스
Add-Rect $s4 40 270 880 210 $LIGHT_BG | Out-Null
Add-Rect $s4 40 270 4 210 $GREEN_OK | Out-Null
Add-TextBox $s4 "실측 결과" 55 278 150 25 14 $GREEN_OK $true 1 | Out-Null

$results = @(
    "서버:  `"Online Scene: BasicRoom`" 등록  +  `"Client 0 is successfully validated`"",
    "클라:  Lobby씬 언로드  /  BasicRoom 로드  /  C-MasterCanvas 소멸 (UI 자동 전환)",
    "NetworkObjects = 5  |  Desktop(Clone) + mixamorig:Hips 아바타 룸에 스폰",
    "아바타 카메라 활성  |  게임뷰에 룸 표시 (로비 가림 없음)"
)
$ry = 305
foreach ($r in $results) {
    Add-TextBox $s4 ("✓  " + $r) 55 $ry 850 28 15 $WHITE $false 1 | Out-Null
    $ry += 38
}

Add-TextBox $s4 '→ "문서/패키지만으로 작동 룸 재현 가능"이 성립' 55 455 850 28 15 $GREEN_OK $true 1 | Out-Null

# ═══════════════════════════════════════════
# SLIDE 5 — 룸 작동 불변식 C1~C4
# ═══════════════════════════════════════════
$s5 = Add-Slide
Set-BG $s5 $DARK_BG
Add-Rect $s5 0 0 960 6 $ACCENT | Out-Null

Add-TextBox $s5 "룸 작동 불변식 C1 ~ C4" 40 20 880 40 14 $ACCENT $true 1 | Out-Null
Add-TextBox $s5 "이거 안 맞으면 안 돈다 — 전부 실측으로 발굴" 40 55 880 35 20 $WHITE $true 1 | Out-Null

$checks = @(
    @{ label="C1  컬렉션 일치"; color=$ACCENT;  desc="룸서버·클라 NM 모두 DefaultPrefabObjects 동일하게 설정"; warn="불일치 시 → 입장은 되나 아바타 안 보임" },
    @{ label="C2  플레이어 스포너"; color=$ACCENT2; desc="XumPlayerSpawner + NetworkObject + XumNetwork + sp 태그, 플랫폼 카탈로그로 Desktop/UnityXR 스폰"; warn="패키지 R-PlayerSpawner(Example Cube) 사용 금지" },
    @{ label="C3  씬 전환"; color=$GREEN_OK; desc="_onlineScene=<룸>  /  _offlineScene=Client 필수 설정"; warn="offline 비면 → 로비가 룸 위에 남음" },
    @{ label="C4  토폴로지"; color=$YELLOW;  desc="서버 = Master+Room exe / 에디터 = Client+Room 동시 로드"; warn="" }
)
$cy = 105
foreach ($c in $checks) {
    Add-Rect $s5 40 $cy 880 95 $CARD_BG | Out-Null
    Add-Rect $s5 40 $cy 5 95 $c.color | Out-Null
    Add-TextBox $s5 $c.label 55 ($cy+8) 250 28 $c.color $true 1 | Out-Null
    Add-TextBox $s5 $c.desc 55 ($cy+36) 860 28 $WHITE $false 1 | Out-Null
    if ($c.warn -ne "") {
        Add-TextBox $s5 ("⚠  " + $c.warn) 55 ($cy+64) 860 24 $YELLOW $false 1 | Out-Null
    }
    $cy += 103
}

# ═══════════════════════════════════════════
# SLIDE 6 — 핵심 발견: 스포너는 프리팹이어야
# ═══════════════════════════════════════════
$s6 = Add-Slide
Set-BG $s6 $DARK_BG
Add-Rect $s6 0 0 960 6 $ACCENT | Out-Null

Add-TextBox $s6 "핵심 발견 — 스포너는 반드시 프리팹" 40 20 880 40 14 $ACCENT $true 1 | Out-Null
Add-TextBox $s6 "from-docs 재현이 찾아낸 실전 갭" 40 55 880 35 20 $WHITE $true 1 | Out-Null

# 비교 표
# 왼쪽 (X)
Add-Rect $s6 40 110 430 180 $CARD_BG | Out-Null
Add-Rect $s6 40 110 430 4 0xFF4444 | Out-Null
Add-TextBox $s6 "AddComponent 방식  ✗" 50 120 410 28 16 0xFF6666 $true 1 | Out-Null
Add-TextBox $s6 "NetworkObject가 유효 scene id를 못 받음" 50 155 410 25 13 $WHITE $false 1 | Out-Null
Add-TextBox $s6 "→ 입장 시 `"Failed to confirm the access`" 거부" 50 180 410 25 13 0xFF9999 $false 1 | Out-Null
Add-TextBox $s6 "ReproRoom 실측 확인" 50 210 410 25 13 0x607080 $false 1 | Out-Null

# 오른쪽 (O)
Add-Rect $s6 490 110 430 180 $CARD_BG | Out-Null
Add-Rect $s6 490 110 430 4 $GREEN_OK | Out-Null
Add-TextBox $s6 "프리팹 인스턴스화  ✓" 500 120 410 28 16 $GREEN_OK $true 1 | Out-Null
Add-TextBox $s6 "유효 scene id 정상 취득" 500 155 410 25 13 $WHITE $false 1 | Out-Null
Add-TextBox $s6 "→ 입장 검증 통과" 500 180 410 25 13 $GREEN_OK $false 1 | Out-Null
Add-TextBox $s6 "BasicRoom 실측 확인" 500 210 410 25 13 0x607080 $false 1 | Out-Null

# 결론 박스
Add-Rect $s6 40 315 880 90 $LIGHT_BG | Out-Null
Add-Rect $s6 40 315 880 4 $ACCENT | Out-Null
Add-TextBox $s6 "해결책" 55 323 100 25 13 $ACCENT $true 1 | Out-Null
Add-TextBox $s6 "Assets/PromptScene/Prefabs/Room-PlayerSpawner.prefab 으로 패키지화" 55 348 860 30 15 $WHITE $false 1 | Out-Null
Add-TextBox $s6 "부수 발견: DocRoom이 '됐던' 것도 Room.unity에서 클론했기 때문 — 순수 문서만으론 안 됐음. 문서+프리팹으로 진짜 재현 가능하게 완성." 55 380 860 30 13 0xB0C4DE $false 1 | Out-Null

# Ruler 파일럿
Add-Rect $s6 40 425 880 90 $CARD_BG | Out-Null
Add-Rect $s6 40 425 4 90 $GREEN_OK | Out-Null
Add-TextBox $s6 "Ruler 콘텐츠 파일럿  ✓" 55 433 400 25 14 $GREEN_OK $true 1 | Out-Null
Add-TextBox $s6 "RulerContent : IToggleableContent → RoomCore 자기등록 → SetEnabled(true) → 두 점 측정선 + 거리 라벨(4.00m) 렌더 확인" 55 460 860 28 13 $WHITE $false 1 | Out-Null
Add-TextBox $s6 "DeepChairProject 앱레이어 의존 0 — 계약 위에 클린 재구현" 55 490 860 25 13 0xB0C4DE $false 1 | Out-Null

# ═══════════════════════════════════════════
# SLIDE 7 — 콘텐츠 플러그인 아키텍처
# ═══════════════════════════════════════════
$s7 = Add-Slide
Set-BG $s7 $DARK_BG
Add-Rect $s7 0 0 960 6 $ACCENT | Out-Null

Add-TextBox $s7 "콘텐츠 플러그인 아키텍처" 40 20 880 40 14 $ACCENT $true 1 | Out-Null
Add-TextBox $s7 "업계 표준: UE5 Modular Game Features 동일 패턴" 40 55 880 35 20 $WHITE $true 1 | Out-Null

# 두 열
# SYSTEMS
Add-Rect $s7 40 110 430 240 $CARD_BG | Out-Null
Add-Rect $s7 40 110 430 5 $ACCENT | Out-Null
Add-TextBox $s7 "SYSTEMS (Core)" 50 120 410 28 18 $ACCENT $true 2 | Out-Null
Add-TextBox $s7 "RoomCore" 50 155 410 28 18 $WHITE $true 2 | Out-Null
Add-TextBox $s7 "• 특정 기능을 컴파일타임에 모름" 60 185 400 25 13 0xB0C4DE $false 1 | Out-Null
Add-TextBox $s7 "• IRoomCore 서비스 + 레지스트리 제공" 60 210 400 25 13 0xB0C4DE $false 1 | Out-Null
Add-TextBox $s7 "• 기능을 빼도 코어는 안 깨짐" 60 235 400 25 13 $GREEN_OK $false 1 | Out-Null
Add-TextBox $s7 '판별: "빼도 안 깨지면 FEATURE"' 60 270 400 28 13 $YELLOW $false 1 | Out-Null

# FEATURES
Add-Rect $s7 490 110 430 240 $CARD_BG | Out-Null
Add-Rect $s7 490 110 430 5 $GREEN_OK | Out-Null
Add-TextBox $s7 "FEATURES (Content)" 500 120 410 28 18 $GREEN_OK $true 2 | Out-Null
Add-TextBox $s7 "Ruler · 기타 콘텐츠" 500 155 410 28 18 $WHITE $true 2 | Out-Null
Add-TextBox $s7 "• IToggleableContent 구현" 510 185 400 25 13 0xB0C4DE $false 1 | Out-Null
Add-TextBox $s7 "• RoomCore.Instance에 자기등록" 510 210 400 25 13 0xB0C4DE $false 1 | Out-Null
Add-TextBox $s7 "• 코어에 의존하지 않음" 510 235 400 25 13 $GREEN_OK $false 1 | Out-Null
Add-TextBox $s7 "런치패드 UI로 토글 제어 예정" 510 270 400 28 13 $YELLOW $false 1 | Out-Null

# 씬 계층 표준
Add-Rect $s7 40 375 880 55 $LIGHT_BG | Out-Null
Add-TextBox $s7 "씬 계층 표준" 55 380 150 25 13 $ACCENT $true 1 | Out-Null
Add-TextBox $s7 "===== SYSTEMS / ENVIRONMENT / UI / FEATURES / _DYNAMIC =====" 215 380 670 25 14 $WHITE $true 2 | Out-Null

# 흐름 다이어그램
Add-Rect $s7 40 450 880 60 $CARD_BG | Out-Null
Add-TextBox $s7 "자연어 입력" 50 462 120 36 13 $ACCENT $true 2 | Out-Null
Add-TextBox $s7 "▶" 178 462 30 36 18 0x607080 $false 2 | Out-Null
Add-TextBox $s7 "/promptscene 스킬" 215 462 180 36 13 $WHITE $true 2 | Out-Null
Add-TextBox $s7 "▶" 403 462 30 36 18 0x607080 $false 2 | Out-Null
Add-TextBox $s7 "MCP → Unity씬 조립" 440 462 180 36 13 $WHITE $true 2 | Out-Null
Add-TextBox $s7 "▶" 628 462 30 36 18 0x607080 $false 2 | Out-Null
Add-TextBox $s7 "C1~C4 자동검증" 665 462 180 36 13 $GREEN_OK $true 2 | Out-Null
Add-TextBox $s7 "▶" 853 462 30 36 18 0x607080 $false 2 | Out-Null

# ═══════════════════════════════════════════
# SLIDE 8 — 산출물 & 로드맵
# ═══════════════════════════════════════════
$s8 = Add-Slide
Set-BG $s8 $DARK_BG
Add-Rect $s8 0 0 960 6 $ACCENT | Out-Null

Add-TextBox $s8 "산출물 & 로드맵" 40 20 880 40 14 $ACCENT $true 1 | Out-Null

# 산출물 표
$deliverables = @(
    @{ label="문서"; path="c:\J_0\docs\*.md"; note="계약·절차·보고서" },
    @{ label="런타임 코드/프리팹"; path="XRCollabDemo\Assets\PromptScene\"; note="Core + Content(Ruler) + Prefabs" },
    @{ label="Claude Code 스킬"; path=".claude\skills\*"; note="/promptscene, /RoomContent-* (Phase 4·5)" }
)
$dy = 65
Add-TextBox $s8 "산출물" 40 $dy 880 25 14 $WHITE $true 1 | Out-Null
$dy += 28
foreach ($d in $deliverables) {
    Add-Rect $s8 40 $dy 880 48 $CARD_BG | Out-Null
    Add-TextBox $s8 $d.label 50 ($dy+6) 190 32 15 $ACCENT $true 1 | Out-Null
    Add-TextBox $s8 $d.path 248 ($dy+6) 380 32 13 $WHITE $false 1 | Out-Null
    Add-TextBox $s8 $d.note 635 ($dy+6) 275 32 12 0xB0C4DE $false 1 | Out-Null
    $dy += 52
}

# 로드맵
$dy += 12
Add-TextBox $s8 "로드맵" 40 $dy 880 25 14 $WHITE $true 1 | Out-Null
$dy += 28

$phases = @(
    @{ phase="Phase 3"; title="런치패드 UI"; desc="레지스트리→아이콘 그리드→SetEnabled (스마트폰식 토글)"; done=$false },
    @{ phase="Phase 4"; title="스킬화"; desc="/RoomContent-<feature> + LLM 신규기능 생성 템플릿"; done=$false },
    @{ phase="Phase 5"; title="합성+하네스"; desc="/promptscene (자연어→룸 조립) + C1~C4/콘텐츠 자동검증"; done=$false }
)
foreach ($p in $phases) {
    $phColor = if ($p.done) { $GREEN_OK } else { $YELLOW }
    Add-Rect $s8 40 $dy 880 45 $LIGHT_BG | Out-Null
    Add-Rect $s8 40 $dy 5 45 $phColor | Out-Null
    Add-TextBox $s8 $p.phase 50 ($dy+5) 80 32 14 $phColor $true 1 | Out-Null
    Add-TextBox $s8 $p.title 138 ($dy+5) 180 32 15 $WHITE $true 1 | Out-Null
    Add-TextBox $s8 $p.desc 325 ($dy+5) 590 32 13 0xB0C4DE $false 1 | Out-Null
    $dy += 50
}

# ═══════════════════════════════════════════
# 저장
# ═══════════════════════════════════════════
$presentation.SaveAs($OutputPath)
Write-Host "PPTX 저장 완료: $OutputPath"

# PDF 저장 (ppSaveAsPDF = 32)
$PdfPath = "c:\J_0\docs\OXR-SDK-AI-Ready.pdf"
$presentation.SaveAs($PdfPath, 32)
Write-Host "PDF 저장 완료: $PdfPath"

$presentation.Close()
$ppt.Quit()
