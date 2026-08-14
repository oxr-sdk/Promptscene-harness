# 아이콘 폰트 굽기 — Material Symbols 가변 폰트 → 정적 인스턴스 + 우리가 쓰는 코드포인트만 subset.
# SKILL.md Phase 1c 의 실행 가능한 형태. 결과를 studio 의
#   Assets/Resources/Fonts/MaterialSymbolsOutlined-PS.ttf 에 **같은 파일명으로 덮으면**
# .meta/GUID가 유지되어 코드도 씬도 손대지 않아도 된다.
#
# 소스(10.6MB, Apache-2.0):
#   https://raw.githubusercontent.com/google/material-design-icons/master/variablefont/
#     MaterialSymbolsOutlined%5BFILL%2CGRAD%2Copsz%2Cwght%5D.ttf
#
# ⭐ 굽는 조건(2026-08-13 확정 — 이걸 적어두지 않아 면적 대조로 역산해야 했다):
#     wght=400 · GRAD=0 · **opsz=48** · FILL = 0(선) 또는 **1(채움, 현행)**
#   opsz는 획 두께를 바꾼다(24면 48보다 굵다). 최초 배포본이 opsz=48이었고, 그걸 모른 채 기본값(24)로
#   구우면 "채우기만" 하려던 변경에 획 굵기 회귀가 조용히 딸려온다.
#   검산: 같은 조건이면 upem(960)·advance(1.000em)·bbox가 이전 폰트와 **완전히 일치**해야 한다.
#
# 사용:  python bake_icon_font.py <소스.ttf> <출력.ttf> <FILL 0|1> [opsz]
import sys
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from fontTools import subset

SRC, OUT = sys.argv[1], sys.argv[2]
FILL = float(sys.argv[3])
OPSZ = float(sys.argv[4]) if len(sys.argv) > 4 else 48.0   # 배포본과 같은 획 두께를 유지하는 값

# HudIcons.cs 의 표와 **같은** 코드포인트. U11이 Font.HasCharacter 로 전수 단정한다.
CODEPOINTS = [0xE0C9, 0xEFED, 0xE41C, 0xE764, 0xE39E, 0xEBD0, 0xEA28, 0xE92E]

f = TTFont(SRC)
axes = {a.axisTag: (a.minValue, a.defaultValue, a.maxValue) for a in f["fvar"].axes}
print("축:", axes)

pin = {"FILL": FILL, "wght": 400, "GRAD": 0}
if "opsz" in axes:
    pin["opsz"] = OPSZ                     # ⚠ 기본값(24)이 아니라 배포본과 같은 48
static = instancer.instantiateVariableFont(f, pin, inplace=False)
print("고정한 축:", pin, "| fvar 남음:", "fvar" in static)

opts = subset.Options()
opts.layout_features = []
opts.notdef_outline = True
opts.recalc_bounds = True
opts.drop_tables += ["DSIG"]
s = subset.Subsetter(options=opts)
s.populate(unicodes=CODEPOINTS)
s.subset(static)
static.save(OUT)

chk = TTFont(OUT)
cmap = chk.getBestCmap()
missing = [hex(c) for c in CODEPOINTS if c not in cmap]
print("subset 코드포인트:", len(CODEPOINTS), "| 누락:", missing or "없음")
print("glyf 있는가:", "glyf" in chk, "| 파일:", OUT)
