// ContentLogic/PromptScene/UI/HudTheme.cs
//
// 성역. 토큰의 SSOT. LLM은 이 파일을 수정하지 않는다 — 새 토큰이 필요하면 먼저 제안하고 승인받는다.
// ScriptableObject를 쓰지 않는 이유: §3b(hot 뷰는 직렬 필드 0, 런타임 배선) — 토큰이 코드에 있으면
// hot-update 경계 안에서 값만 바꿔 컴파일하면 끝이고, 직렬화 지뢰를 밟지 않는다.
//
// 폰트: 펴진고딕(PyeojinGothic, SIL OFL) 400/600. 아이콘은 Material Symbols Outlined(Apache-2.0)를
//   **정적 인스턴스로 구운 4글리프 서브셋**. 둘 다 baked base(Assets/Resources/Fonts)에 둔다.
//
// ══════════════════════════════════════════════════════════════════════════════════════════════════
// glass v6 — 밝은 유리 원 + 어두운 글리프 + 페이지 그리드
// ══════════════════════════════════════════════════════════════════════════════════════════════════
// 승인 목업: assets/hud-glass-v6.html. 제목·각주·ActionRow가 없다. 패널은 아이콘 원 4개/페이지와
// 페이지 점만 담는다. "측정 지우기" 같은 파괴적 액션도 2페이지의 아이콘 버튼이 된다.
//
// ── 잉크 방향의 산술 (이게 v6에서 가장 중요한 결정이다) ──────────────────────────────────────────
// 합성 배경 휘도 L이 **가운데**에 있으면 어떤 잉크색도 4.5:1을 못 낸다:
//     흰 잉크가 되려면 L <= 0.162 / 어두운 잉크가 되려면 L >= ~0.62
// v6 초안(panel α.42 + film α.28)의 원 안 배경은 L = 0.069(검은 환경) ~ 0.45(흰 환경)로 그 사이에
// 걸쳐 있어서, 어두운 글리프가 **검은 환경에서 2.24:1**로 떨어졌다(v2가 밝은 환경에서 무너진 것의 정확한 거울상).
// 정지 규칙대로 게이트가 아니라 디자인을 고쳤다 — 오너 결정은 "어두운 글리프 유지":
//     · Film      α.28 → **α.60**  (어두운 글리프 2.24:1 → 7.17:1). 실측 스윕: .28=2.24 / .45=4.27 / .60=7.01
//     · FilmHover α.38 → **α.70**
//     · Scrim     α.42 → **α.78**  (라벨 1.72 → 6.09. 아래 "라벨" 문단 참고)
// 즉 원은 "반투명 유리"에서 **"밝은 반불투명 판"**으로 이동했다. 배경이 40%만 비친다. 이게 어두운
// 글리프가 성립하기 위한 최소 조건이고, iOS도 어두운 글리프는 **채워진** 원에서만 쓴다.
//
// ── 라벨: 산술을 통과했지만 눈으로 보고 되돌린 것 ────────────────────────────────────────────────
// v6 초안은 패널 α.42 위의 TextLo(1.72:1)를 **불투명 아웃라인**(--tmp-outline)으로 구제했고, 그건 실제로
// 산술을 통과했다(자기 아웃라인 대비 11.8:1). 그런데 **U8 캡처를 눈으로 보니** 16px 한글에 2px 아웃라인이
// 사방으로 깔리면서 획이 서로 먹혀 글자가 뭉갰다 — 대비 게이트가 통과시킨 것을 사람 눈이 잡은 두 번째 사례다
// (첫 번째는 카드 밖으로 흘러나온 줄바꿈 2번째 줄). 판정에 안 넣는 증거를 왜 만드는지에 대한 답이 또 나왔다.
// 산술로 다시 풀어 보니 어두운 글리프의 대비는 **Scrim 알파와 거의 무관하고**(film .60 기준 α.42→7.01 /
// α.78→7.17) 라벨만 Scrim에 민감했다. 그래서 패널을 어둡게 하고 아웃라인을 **걷어냈다**:
//     Scrim α.78 → 라벨 6.09:1 (아웃라인 0개), 어두운 글리프 7.17:1
// v6 데모가 스스로 제공하던 α 토글(.42 ⇄ .76)의 위쪽 값과 사실상 같은 지점이다.
// TextOutline/OutlineW 토큰과 U7의 아웃라인 절은 남겨 둔다(유효한 기계다) — 다만 현재 사용처는 0이다.
//
// ── 픽셀 밀도 ────────────────────────────────────────────────────────────────────────────────────
// v6의 모든 px는 **1200 px/m**에서 그려졌고(패널 648px = 0.54m → 1.5m에서 20.5°), 서로 물려 있다.
// 그래서 PxPerMeter를 1200으로 **선택**하고 캔버스 스케일을 1/PxPerMeter로 유도한다 — 목업 px가 1:1로
// 옮겨오고, Phase 2.5 실측이 매번 이 값을 재확인한다(어긋나면 정지).
// 동시에 HUD 배치를 z=2.5m → **1.5m**로 당긴다: 1200px/m에서 16px 라벨은 2.5m에서 13.2'로 하한(20')
// 아래지만 1.5m에서 22.0'로 통과한다. v6가 스스로 "1.5m에서 20.5°"라고 적어둔 설계 거리다.
// 결과적으로 설계 거리와 실배치 거리가 같아져 두 거리를 따로 판정할 이유가 사라졌다.

using System.Collections.Generic;
using UnityEngine;

namespace PromptScene.Core.UI
{
    public static class HudTheme
    {
        // ── 간격: 7단 (v6에서 12가 추가됐다 — 히트박스 패딩이자 안쪽 간격의 절반) ────────────────
        public const int Space1 = 4;
        public const int Space2 = 8;
        public const int Space3 = 12;
        public const int Space4 = 16;
        public const int Space5 = 24;
        public const int Space6 = 32;
        public const int Space7 = 48;
        public static readonly int[] SpaceScale = { Space1, Space2, Space3, Space4, Space5, Space6, Space7 };

        // ── 타입 램프: 16 / 24 / 32. v6 패널에는 라벨(16)만 나온다 ────────────────────────────
        public const int FontFoot  = 16;   // 라벨·배지 라벨
        public const int FontBody  = 24;
        public const int FontTitle = 32;
        public const int FontMinPx = 16;

        // ── 역할 크기: 램프 **밖**. 임의 예외 금지 — Roles 화이트리스트에 등록된 역할만 쓴다 ─────
        public const int GlyphPx  = 48;   // IconButton 글리프 — 원 지름의 0.40
        public const int KeycapPx = 48;   // KeyBadge 키캡 — 배지 지름의 0.40

        /// <summary>
        /// 역할 예외 화이트리스트. 이 접미사를 가진 노드만 램프 크기 집계에서 빠진다.
        /// 새 역할을 추가하려면 토큰을 제안하고 승인받는다(= 임의 예외가 불가능한 이유).
        /// </summary>
        public static class Roles
        {
            public const string Glyph  = "__glyph";   // 아이콘 글리프 (GlyphPx)
            public const string Keycap = "__keycap";  // KeyBadge 키캡 (KeycapPx)
            public const string Icon   = "__icon";    // Meta.Icon 스프라이트
            public const string Disc   = "__disc";    // Film 컨트롤 표면(원판/알약)
            public const string Ring   = "__ring";    // 테두리 링 — 장식, 대비 판정 대상 아님
            public const string Label  = "__label";   // IconButton 라벨 (FontFoot, 아웃라인)
            public const string Dot    = "__dot";     // 페이지 점 — 장식
            public static readonly string[] SizeExempt    = { Glyph, Keycap };
            public static readonly string[] AccentBearing = { Disc, Ring };
            public static readonly string[] Decorative    = { Ring, Dot };
        }

        // ── 웨이트: 400/600 두 개만. faux-bold 금지(FontStyles는 항상 Normal) ────────────────────
        public const int WeightBody = 400;
        public const int WeightEmph = 600;
        public static readonly int[] AllowedWeights = { WeightBody, WeightEmph };

        // ── 색 ──────────────────────────────────────────────────────────────────────────────────
        /// <summary>패널 = dim. 라벨 대비가 전부 여기서 나온다(α.78 → TextLo 6.09:1). 원의 Film 대비에는 거의 영향이 없다.</summary>
        public static readonly Color Scrim     = New(0x0A, 0x0D, 0x12, 0.78f);
        /// <summary>원판. **α.28에서 올렸다** — 어두운 글리프가 검은 환경에서 2.24:1로 죽었다(위 산술 참고).</summary>
        public static readonly Color Film      = New(0xFF, 0xFF, 0xFF, 0.60f);
        /// <summary>레이 조준 피드백(hover). 장식이 아니라 "지금 이걸 겨누고 있다"는 유일한 신호다.</summary>
        public static readonly Color FilmHover = New(0xFF, 0xFF, 0xFF, 0.70f);
        /// <summary>링 위쪽(밝은 스펙큘러). 링 스프라이트가 위→아래 알파 그라데이션을 들고 있어 한 장으로 2톤을 낸다.</summary>
        public static readonly Color RimTop    = New(0xFF, 0xFF, 0xFF, 0.55f);
        /// <summary>링 아래쪽. RimTop 대비 비율로만 쓰인다(스프라이트 알파 하한).</summary>
        public static readonly Color RimBot    = New(0xFF, 0xFF, 0xFF, 0.12f);
        /// <summary>패널 테두리.</summary>
        public static readonly Color Rim       = New(0xFF, 0xFF, 0xFF, 0.14f);
        public static readonly Color RimLit    = New(0xFF, 0xFF, 0xFF, 0.26f);
        public static readonly Color TextHi    = New(0xF2, 0xF4, 0xF8, 1.00f);
        public static readonly Color TextLo    = New(0xC3, 0xCA, 0xD6, 1.00f);
        /// <summary>액센트는 "활성 상태" 한 가지 의미에만 쓴다.</summary>
        public static readonly Color Accent    = New(0x5A, 0xC8, 0xFA, 1.00f);
        /// <summary>글리프 잉크. v6에서는 **OFF/ON 양쪽 모두** 어둡다 — 그래서 Film이 불투명해야 했다.</summary>
        public static readonly Color GlyphDark = New(0x0A, 0x0D, 0x12, 1.00f);
        /// <summary>
        /// 라벨 아웃라인. **불투명이어야** U7의 아웃라인 절이 적용된다(반투명 아웃라인은 대비를 보장 못 한다).
        /// ⚠ 현재 사용처 0 — Scrim α.78이면 라벨이 아웃라인 없이 6.09:1이고, 16px 한글에 아웃라인을 깔면 획이 뭉갠다.
        /// </summary>
        public static readonly Color TextOutline = New(0x0A, 0x0D, 0x12, 1.00f);
        public static readonly Color Dot       = New(0xFF, 0xFF, 0xFF, 0.28f);
        public static readonly Color DotOn     = New(0xF2, 0xF4, 0xF8, 1.00f);

        // ── 형태 ────────────────────────────────────────────────────────────────────────────────
        public const int Radius  = 24;         // 패널 코너
        public const int BorderW = 1;          // 패널 테두리
        public const int RimW    = 2;          // 원 링 / 배지 링
        public const int OutlineW = 2;         // 라벨 아웃라인 두께 (v6 --tmp-outline)
        /// <summary>시각 원 지름.</summary>
        public const int CircleD = 120;
        /// <summary>히트박스 = 원 + HitPad*2. 패딩이 곧 원 사이 간격의 절반이다.</summary>
        public const int HitPad  = Space3;                 // 12
        public static int HitD => CircleD + HitPad * 2;    // 144
        /// <summary>원끼리 간격 = 히트박스 패딩 두 개.</summary>
        public static int InnerGap => HitPad * 2;          // 24
        /// <summary>바깥 여백 = 안쪽 간격의 **2배**. 이게 4개를 "한 덩어리"로 읽히게 만든다(근접성 원리).</summary>
        public static int OuterMargin => InnerGap * 2;     // 48
        public const int DotsRowH = Space5;                // 24 — 점 줄 높이 = 위쪽 미러 여백 높이
        public const int LabelBoxH = 20;                   // 라벨 1줄 고정(페이지 간 높이 튐 방지)
        public const int GridColumns = 4;

        /// <summary>
        /// 패널 padding은 손으로 넣지 않고 **유도한다** — 그래야 다시 어긋날 수 없다.
        /// padX = Outer − HitPad, padY = Outer − HitPad − DotsRowH.
        /// 이 둘은 간격 스케일의 원소가 아니라 스케일에서 **파생된** 값이고, U7이 그렇게 판정한다.
        /// </summary>
        public static int PadX => OuterMargin - HitPad;                 // 36
        public static int PadY => OuterMargin - HitPad - DotsRowH;      // 12  (음수면 안 된다)
        /// <summary>U7이 간격 스케일과 함께 허용하는 파생 간격.</summary>
        public static int[] DerivedSpacings => new[] { PadX, PadY };
        /// <summary>패널 폭 = 4열 히트박스 + 좌우 padX. (4*144 + 36*2 = 648)</summary>
        public static int PanelW => GridColumns * HitD + PadX * 2;

        // ── 가독성 기하 ─────────────────────────────────────────────────────────────────────────
        public static class Legibility
        {
            /// <summary>
            /// **선택값**이다(v0~v3의 384.6 실측을 대체). v6의 모든 px가 1200px/m에 물려 있어서
            /// 목업을 1:1로 옮기려면 캔버스를 그 밀도로 맞춰야 한다. HUD_SCALE = 1/PxPerMeter로
            /// 유도되므로 이 숫자가 곧 스케일이고, Phase 2.5 실측이 매 실행마다 재확인한다(±5% 밖이면 정지).
            /// </summary>
            public const float PxPerMeter = 1200f;
            /// <summary>설계 관찰 거리(m). v6가 "1.5m에서 20.5°"로 그려졌다.</summary>
            public const float DistanceM = 1.5f;
            /// <summary>
            /// 실배치 거리(m). v6에서 설계 거리와 **일치시켰다** — 1200px/m에서 16px 라벨은 2.5m에서
            /// 13.2'(하한 미달)이지만 1.5m에서 22.0'로 통과하기 때문이다. 배치를 옮겨 게이트를 만족시켰지
            /// 게이트를 옮기지 않았다.
            /// </summary>
            public const float PlacementDistanceM = DistanceM;
            /// <summary>캡높이 각크기 하한(arcmin). 경험 하한 — 실기기에서 재보정할 값.</summary>
            public const float MinCapArcmin = 20f;
            /// <summary>탭 타깃 각크기 하한(도).</summary>
            public const float MinTargetDeg = 1.5f;

            public static float CapArcmin(int fontPx, float pxPerMeter = PxPerMeter, float distanceM = DistanceM)
                => 0.72f * fontPx / pxPerMeter / distanceM * Mathf.Rad2Deg * 60f;

            public static float Deg(float px, float distanceM = DistanceM)
                => px / PxPerMeter / distanceM * Mathf.Rad2Deg;

            public static bool Passes(int fontPx) => CapArcmin(fontPx) >= MinCapArcmin;
        }

        // ── 월드 프롬프트 배지 (F4) ─────────────────────────────────────────────────────────────
        /// <summary>
        /// KeyBadge가 유지하는 목표 각크기(도). 거리와 무관하게 이 시야각을 차지한다 — 월드 스케일 고정
        /// 텍스트가 보는 위치에 따라 벽만큼 커지던 결함(`"E 키로 앉기"`)을 구조적으로 막는다.
        /// </summary>
        public const float BadgeTargetDeg = 3f;
        /// <summary>이 거리 이내에서만 배지에 라벨(`앉기`)을 동반 표시한다. 원거리 = 배지만.</summary>
        public const float BadgeLabelDistanceM = 2f;
        public static float BadgeBaseDiameterM => CircleD / Legibility.PxPerMeter;
        /// <summary>각크기 고정이라 배지 글자의 캡 각크기는 거리와 무관하게 상수다.</summary>
        public static float BadgeCapArcmin => 0.72f * KeycapPx / CircleD * BadgeTargetDeg * 60f;

        // ── 페이징 ──────────────────────────────────────────────────────────────────────────────
        /// <summary>한 페이지에 담기는 아이콘 수.</summary>
        public const int PageSize = GridColumns;
        /// <summary>드래그가 "넘김"으로 인정되는 페이지 폭 비율.</summary>
        public const float PageFlickFraction = 0.2f;
        /// <summary>이 픽셀 이상 움직이면 클릭이 아니라 드래그로 본다(버튼 토글이 죽지 않게).</summary>
        public const float DragThresholdPx = 8f;
        /// <summary>점이 보였다가 사라지기까지(초). 넘길 때만 보인다.</summary>
        public const float DotsRevealSeconds = 1.2f;

        // ── 대비: F0의 산술을 코드로 (VerifyUI U7이 이 함수로 판정한다) ──────────────────────────
        public static class Contrast
        {
            /// <summary>본문·글리프 공통 하한.</summary>
            public const float MinText = 4.5f;

            /// <summary>최악 배경 후보: 환경이 무엇이든 이 둘 사이에 있다.</summary>
            public static readonly Color[] WorstEnvironments = { Color.white, Color.black };

            static float Lin(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

            public static float Luminance(Color c) => 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);

            public static Color Over(Color src, Color dst) => new Color(
                src.r * src.a + dst.r * (1f - src.a),
                src.g * src.a + dst.g * (1f - src.a),
                src.b * src.a + dst.b * (1f - src.a), 1f);

            public static Color Composite(Color env, IList<Color> stackRootFirst)
            {
                var acc = new Color(env.r, env.g, env.b, 1f);
                if (stackRootFirst != null)
                    for (int i = 0; i < stackRootFirst.Count; i++) acc = Over(stackRootFirst[i], acc);
                return acc;
            }

            public static float Ratio(Color fg, Color opaqueBg)
            {
                float a = Luminance(fg), b = Luminance(opaqueBg);
                if (a < b) { var t = a; a = b; b = t; }
                return (a + 0.05f) / (b + 0.05f);
            }

            /// <summary>최악 배경(흰·검 양쪽)을 가정한 최소 대비.</summary>
            public static float WorstRatio(Color fg, IList<Color> stackRootFirst)
            {
                float worst = float.MaxValue;
                foreach (var env in WorstEnvironments)
                {
                    var bg = Composite(env, stackRootFirst);
                    var f  = fg.a >= 0.999f ? fg : Over(fg, bg);
                    worst = Mathf.Min(worst, Ratio(f, bg));
                }
                return worst;
            }

            /// <summary>
            /// 불투명 아웃라인이 있는 글자는 **자기 아웃라인**을 배경으로 읽힌다. 배경 스택이 어떻든
            /// 글자 둘레가 항상 같은 색이기 때문이다. 이건 게이트 완화가 아니라 다른 기계다 —
            /// 대신 아웃라인이 불투명해야 하고(반투명이면 배경이 새어 들어와 보장이 깨진다), 두께가
            /// 있어야 한다. 두 조건은 U7이 따로 단정한다.
            /// </summary>
            public static float OutlinedRatio(Color fg, Color outline) => Ratio(fg, outline);
        }

        static Color New(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);
    }

    /// <summary>
    /// 라운드 코너·원·링·프레임을 에셋 0개로 얻는다. 런타임에 스프라이트를 그려 캐시하므로
    /// Addressables·baked 경계 질문이 아예 발생하지 않고, 반경이 토큰 숫자로 남는다.
    /// 색은 Image.color로 틴트한다 — 스프라이트는 알파만 들고 있다.
    ///
    /// 테두리를 "바깥 Image + 안쪽 inset Image"로 만들지 않는 이유: 그러면 상자가 한 겹씩 늘어난다
    /// (v2 결함 D2 "상자 3중 중첩"). 링/프레임은 알파만 있는 **한 장**이라 테두리가 형제 노드 하나로 끝난다.
    /// </summary>
    public static class HudSprites
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite RoundedRect(int radius = HudTheme.Radius) => Rounded(radius, 0);

        public static Sprite RoundedFrame(int radius = HudTheme.Radius, int borderW = HudTheme.BorderW)
            => Rounded(radius, Mathf.Max(1, borderW));

        public static Sprite Circle(int d = HudTheme.CircleD) => Disc(d, 0, 1f);

        public static Sprite Ring(int d = HudTheme.CircleD, int bandW = HudTheme.RimW)
            => Disc(d, Mathf.Max(1, bandW), 1f);

        /// <summary>
        /// 위→아래로 알파가 떨어지는 링. v6의 2톤 테두리(위 α.55 / 아래 α.12)를 **한 장**으로 낸다:
        /// Image.color = RimTop 이고 스프라이트가 그 비율(bottom/top)까지 알파를 깎는다.
        /// </summary>
        public static Sprite RingGraded(int d = HudTheme.CircleD, int bandW = HudTheme.RimW, float bottomRatio = 0f)
            => Disc(d, Mathf.Max(1, bandW), Mathf.Clamp01(bottomRatio));

        // ── 구현 ─────────────────────────────────────────────────────────────────────────
        static Sprite Rounded(int radius, int band)
        {
            radius = Mathf.Max(1, radius);
            string key = "rr_" + radius + "_" + band;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int size = radius * 2 + 2;               // 가운데 2x2가 늘어나는 영역
            var tex = NewTex(size, $"HudRounded_{radius}_{band}");
            float half = size * 0.5f;
            float inner = half - radius;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - inner;
                float qy = Mathf.Abs(y + 0.5f - half) - inner;
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - radius;
                px[y * size + x] = new Color32(255, 255, 255, Alpha(d, band, 1f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            var sprite = Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,    // Sliced에는 FullRect 필수
                new Vector4(radius, radius, radius, radius));
            Name(sprite, tex);
            _cache[key] = sprite;
            return sprite;
        }

        // 원/링은 Sliced로 늘릴 수 없다(늘리면 타원이 아니라 뭉개진 알약이 된다) → Simple로 쓰고
        // 텍스처를 실제 지름으로 굽는다. 지름은 토큰이라 캐시 엔트리는 사실상 1~2개다.
        static Sprite Disc(int d, int band, float bottomRatio)
        {
            d = Mathf.Max(4, d);
            string key = "ci_" + d + "_" + band + "_" + bottomRatio.ToString("F2");
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = NewTex(d, $"HudCircle_{d}_{band}");
            float c = d * 0.5f, r = c - 0.5f;
            var px = new Color32[d * d];
            for (int y = 0; y < d; y++)
            {
                // y=0 이 아래. 위(y=d-1)에서 1.0, 아래에서 bottomRatio.
                float grade = Mathf.Lerp(bottomRatio, 1f, d > 1 ? (float)y / (d - 1) : 1f);
                for (int x = 0; x < d; x++)
                {
                    float dist = new Vector2(x + 0.5f - c, y + 0.5f - c).magnitude - r;
                    px[y * d + x] = new Color32(255, 255, 255, Alpha(dist, band, grade));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Name(sprite, tex);
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>SDF 거리 d(음수=안쪽) → 알파. band>0 이면 [-band, 0] 구간만(테두리). grade는 세로 감쇠.</summary>
        static byte Alpha(float d, int band, float grade)
        {
            float a = Mathf.Clamp01(0.5f - d);                                  // 바깥 경계 1px AA
            if (band > 0) a = Mathf.Min(a, Mathf.Clamp01(0.5f + (d + band)));   // 안쪽 경계 1px AA
            return (byte)(a * grade * 255f);
        }

        static Texture2D NewTex(int size, string name) => new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        static void Name(Sprite s, Texture2D t) { s.name = t.name; s.hideFlags = HideFlags.HideAndDontSave; }
    }
}
