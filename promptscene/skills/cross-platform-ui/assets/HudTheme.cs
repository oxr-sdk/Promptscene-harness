// ContentLogic/PromptScene/UI/HudTheme.cs
//
// 성역. 토큰의 SSOT. LLM은 이 파일을 수정하지 않는다 — 새 토큰이 필요하면 먼저 제안하고 승인받는다.
// ScriptableObject를 쓰지 않는 이유: §3b(hot 뷰는 직렬 필드 0, 런타임 배선) — 토큰이 코드에 있으면
// hot-update 경계 안에서 값만 바꿔 컴파일하면 끝이고, 직렬화 지뢰를 밟지 않는다.
//
// 폰트: 펴진고딕(PyeojinGothic, SIL OFL). 7웨이트를 제공하지만 우리는 400/600 두 개만 채택한다.
//   FontAsset은 딱 두 개만 만든다 — 동적 SDF 아틀라스는 웨이트마다 텍스처 메모리를 따로 먹는다.
//   Atlas Population Mode = Dynamic + Multi Atlas Textures (한글 11,172자 + 임의 채팅 텍스트).
//   폰트 에셋은 baked base에 둔다(모든 룸이 공유하는 플랫폼성 에셋).
//
// 값의 근거는 대부분 기하학이다. 아래 Legibility 상수 참고.

using System.Collections.Generic;
using UnityEngine;

namespace PromptScene.Core.UI
{
    public static class HudTheme
    {
        // ── 간격: 4px 기준 6단. 이 여섯 개 밖의 수는 HudSanityCheck에서 FAIL ──────────
        public const int Space1 = 4;
        public const int Space2 = 8;
        public const int Space3 = 16;
        public const int Space4 = 24;
        public const int Space5 = 32;
        public const int Space6 = 48;
        public static readonly int[] SpaceScale = { Space1, Space2, Space3, Space4, Space5, Space6 };

        // ── 타입: 크기는 한 패널에 2개까지 ────────────────────────────────────────
        public const int FontSm = 18;   // 본문·라벨·버튼
        public const int FontMd = 24;   // 패널 제목
        public const int FontLg = 32;   // 배너·승자 공지 등 전체화면급에서만
        public const int FontMinPx = 16; // 가독성 하한 (아래 Legibility에서 유도)

        // ── 웨이트: 펴진고딕은 300~900을 주지만 우리는 두 개만 쓴다 ─────────────────
        // 400과 600 사이(=500)를 강조로 쓰지 않는 이유: 18px/25′ 근처에서 400과의 차이가
        // 안 읽혀 강조로 기능하지 않는다. Light 300은 SDF+원거리에서 획이 흔들려 금지.
        public const int WeightBody = 400;
        public const int WeightEmph = 600;
        public static readonly int[] AllowedWeights = { WeightBody, WeightEmph };
        // faux-bold(TMP FontStyles.Bold) 금지 — 실제 SemiBold FontAsset이 있으므로 쓸 이유가 없고,
        // SDF 팽창으로 엣지가 뭉개진다. FontStyles는 항상 Normal.

        // ── 색 ────────────────────────────────────────────────────────────────────
        // 규율: 텍스트는 Card 위에만 올린다. PanelTint 위에 직접 올리면 배경에 따라 대비가 무너진다.
        public static readonly Color PanelTint  = New(0x0E, 0x11, 0x16, 0.62f); // 틴트(=유리). 텍스트 금지
        public static readonly Color Card       = New(0x12, 0x16, 0x1C, 0.92f); // 텍스트 판
        public static readonly Color Hairline   = New(0xFF, 0xFF, 0xFF, 0.16f);
        public static readonly Color HairlineLit= New(0xFF, 0xFF, 0xFF, 0.28f); // 상단 엣지 = 가짜 스펙큘러
        public static readonly Color TextHi     = New(0xF2, 0xF4, 0xF8, 1.00f);
        public static readonly Color TextLo     = New(0x9A, 0xA3, 0xB2, 1.00f);
        public static readonly Color RowHover   = New(0xFF, 0xFF, 0xFF, 0.05f);

        /// <summary>액센트는 "활성 상태" 한 가지 의미에만 쓴다. 다른 의미에 재사용하면 저점이 무너진다.</summary>
        public static readonly Color Accent     = New(0x5A, 0xC8, 0xFA, 1.00f);

        // ── 형태 ──────────────────────────────────────────────────────────────────
        public const int Radius  = 16;
        public const int BorderW = 1;
        public const int BarW    = 3;   // 활성 행의 액센트 바

        // ── 가독성 기하 (HudSanityCheck가 이 값으로 판정한다) ──────────────────────
        public static class Legibility
        {
            /// <summary>
            /// 실측값(2026-07-30, AssembleRoom / MODE=CROSS). 가정하지 않는다 —
            /// `1f / canvas.transform.lossyScale.x`로 재서 넣은 수다.
            /// 패널 360px = 0.936m → 384.6 px/m (HUD_SCALE 0.0026 m/canvas-unit).
            /// 이 값이 틀리면 아래 각크기 판정 전체가 거짓이 되므로, 패널 스케일을 바꾸면 반드시 다시 잰다
            /// (SKILL.md Phase 2.5 = ±5% 밖이면 정지·보고).
            /// 이전 값 1200f는 "패널 720px = 0.60m" 가정이었고 실측과 3.12배 어긋났다.
            /// </summary>
            public const float PxPerMeter = 384.6f;
            /// <summary>
            /// 설계 기준 관찰 거리(m). 판정은 이 값으로 한다.
            /// 참고: 현행 HUD 배치는 (0,1.6,2.5) = 실 관찰거리 ~2.5m로 기준보다 멀다. 2.5m에서도
            /// CapArcmin(16)=41′ ≥ 20′로 통과하므로 판정 결과는 뒤집히지 않는다(추천사항: 배치를 1.5m로
            /// 당기거나 이 토큰을 2.5f로 올려 기준과 실물을 일치시키는 것 — 둘 다 승인 사항).
            /// </summary>
            public const float DistanceM = 1.5f;
            /// <summary>캡높이 각크기 하한(arcmin). 경험 하한 — 실기기에서 재보정할 값.</summary>
            public const float MinCapArcmin = 20f;
            /// <summary>탭 타깃 각크기 하한(도). 레이 조준 가능 여부.</summary>
            public const float MinTargetDeg = 1.5f;
            /// <summary>텍스트 판의 알파 하한. 이보다 투명하면 배경이 대비를 먹는다.</summary>
            public const float MinTextPlateAlpha = 0.85f;

            /// <summary>폰트 px → 캡높이 각크기(arcmin). 실측 384.6px/m·1.5m에서 16px≈69′ / 18px≈77′ / 24px≈103′ / 32px≈137′.</summary>
            public static float CapArcmin(int fontPx, float pxPerMeter = PxPerMeter, float distanceM = DistanceM)
                => 0.72f * fontPx / pxPerMeter / distanceM * Mathf.Rad2Deg * 60f;

            public static bool Passes(int fontPx) => CapArcmin(fontPx) >= MinCapArcmin;
        }

        static Color New(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);
    }

    /// <summary>
    /// 라운드 코너를 에셋 0개로 얻는다. 런타임에 9-slice 스프라이트를 그려 캐시하므로
    /// Addressables·baked 경계 질문이 아예 발생하지 않고, 반경이 토큰 숫자로 남는다.
    /// 스프라이트는 흰색 단색 — 색은 Image.color로 틴트한다.
    /// 테두리는 이 스프라이트를 쓴 Image를 겹쳐서 만든다(바깥=테두리색, 안쪽=1px inset 채움색).
    /// </summary>
    public static class HudSprites
    {
        static readonly Dictionary<int, Sprite> _cache = new Dictionary<int, Sprite>();

        public static Sprite RoundedRect(int radius = HudTheme.Radius)
        {
            radius = Mathf.Max(1, radius);
            if (_cache.TryGetValue(radius, out var cached) && cached != null) return cached;

            int size = radius * 2 + 2;               // 가운데 2x2가 늘어나는 영역
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = $"HudRounded_{radius}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float half = size * 0.5f;
            float inner = half - radius;             // 라운드 사각형 SDF의 직선부 반폭
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - inner;
                float qy = Mathf.Abs(y + 0.5f - half) - inner;
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - radius;
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);   // 1px 안티에일리어싱
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);                  // CPU 사본 해제

            var sprite = Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,    // Sliced에는 FullRect 필수
                new Vector4(radius, radius, radius, radius));
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            _cache[radius] = sprite;
            return sprite;
        }
    }
}
