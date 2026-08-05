using System.Collections.Generic;
using UnityEngine;

namespace PromptScene.Core.UI
{
    /// <summary>어떤 단계로 아이콘이 결정됐는지. `Letter`는 실패가 아니라 설계된 최종 폴백이다.</summary>
    public enum HudIconTier { None = 0, Sprite = 1, Glyph = 2, Letter = 3 }

    /// <summary>Material Symbols 코드포인트 한 칸. 리가처 이름은 **사람이 읽기 위한 것**이다 — Unity는 리가처를 못 쓴다.</summary>
    public struct HudIconCode
    {
        public string LigatureName;   // 목업·문서에서 부르는 이름 (예: "chair")
        public int Codepoint;         // 실제로 렌더에 쓰는 것 (예: 0xEFED)
        public HudIconCode(string n, int cp) { LigatureName = n; Codepoint = cp; }
    }

    /// <summary>어떤 아이콘을 어떻게 그릴지 정해진 결과.</summary>
    public struct HudIconPick
    {
        public HudIconTier Tier;
        public Sprite Sprite;     // Tier.Sprite
        public string Text;       // Tier.Glyph(코드포인트 1글자) / Tier.Letter(DisplayName 첫 글자)
        public int Codepoint;     // Tier.Glyph
        /// <summary>null이 아니면 **정지·보고 사유**다. 조용히 폴백하지 않기 위해 결과에 실어 보낸다.</summary>
        public string Error;
    }

    /// <summary>
    /// `ContentMeta.Icon`의 첫 소비자(계약 변경 0 — 필드는 처음부터 있었고 아무도 안 썼다).
    ///
    /// 폴백 체인 (전부 런타임, 직렬 필드 0):
    ///   ① Meta.Icon != null            → 스프라이트
    ///   ② 코드포인트 매핑이 있으면       → 아이콘 폰트 글리프
    ///   ③ 둘 다 없으면                  → DisplayName 첫 글자
    ///
    /// ⛔ 정지 규칙: ②의 매핑은 있는데 폰트에 그 코드포인트가 **없으면**, 폴백으로 조용히 넘기지 않는다.
    ///    `Error`를 채워 돌려주고 바인더가 LogError + 목록에 적고 U11이 FAIL 시킨다. 그러지 않으면
    ///    "왜 아이콘이 안 나오는지 영원히 모르는" 상태가 된다.
    ///
    /// 아이콘 폰트는 Material Symbols Outlined(Apache-2.0)의 **정적 인스턴스를 4글리프로 서브셋한 2KB TTF**다.
    /// 가변(variable) ttf를 쓰지 않는 이유는 SKILL.md F3 참조 — npm `@material-symbols/font-400`조차 fvar를
    /// 들고 있어서(축: FILL) 축을 고정해 정적으로 굽는 단계를 반드시 거쳐야 한다.
    /// 폰트 에셋은 본문 폰트와 같은 이유로 **baked base**(Assets/Resources/Fonts)에 둔다.
    /// </summary>
    public static class HudIcons
    {
        public const string FontResourcePath = "Fonts/MaterialSymbolsOutlined-PS";

        /// <summary>
        /// 콘텐츠 id → 코드포인트. 여기 없는 id는 결함이 아니라 ③ 첫글자 폴백 대상이다.
        /// 목록을 늘리면 **서브셋 폰트를 다시 구워야 한다** — U11이 "매핑은 있는데 아틀라스에 없음"을 FAIL로 잡는다.
        /// </summary>
        public static readonly Dictionary<string, HudIconCode> ByContentId = new Dictionary<string, HudIconCode>
        {
            { "chat",            new HudIconCode("chat",           0xE0C9) },
            { "chair-sit",       new HudIconCode("chair",          0xEFED) },
            { "ruler",           new HudIconCode("straighten",     0xE41C) },
            { "grabbable-props", new HudIconCode("back_hand",      0xE764) },
            { "target-props",    new HudIconCode("adjust",         0xE39E) },
            { "score-hud",       new HudIconCode("scoreboard",     0xEBD0) },
            { "darts",           new HudIconCode("sports_esports", 0xEA28) },
        };

        /// <summary>토글이 아닌 액션 버튼의 아이콘(예: 측정 지우기). 액센트를 절대 입지 않는다.</summary>
        public static readonly Dictionary<string, HudIconCode> ByActionId = new Dictionary<string, HudIconCode>
        {
            { "clear", new HudIconCode("delete", 0xE92E) },
        };

        static Font _font;
        static bool _tried;

        /// <summary>서브셋 아이콘 폰트. 없으면 null(② 단계가 사라지고 ③으로 내려간다 — WARN, FAIL 아님).</summary>
        public static Font Font
        {
            get
            {
                if (!_tried) { _tried = true; _font = Resources.Load<Font>(FontResourcePath); }
                return _font;
            }
        }

        /// <summary>폰트에 코드포인트가 실제로 있는가. 굽고 나서 검증하지 않으면 조용히 빈 글리프가 나온다.</summary>
        public static bool HasCodepoint(int cp)
        {
            var f = Font;
            return f != null && f.HasCharacter((char)cp);
        }

        /// <summary>
        /// 폴백 체인을 결정적으로 실행한다. 계약 타입(ContentMeta)에 의존하지 않도록 필요한 값만 받는다 —
        /// 그래서 게이트가 합성 케이스(스프라이트만 있는 것 / 매핑 없는 것)를 같은 함수로 실증할 수 있다.
        /// </summary>
        public static HudIconPick Resolve(Sprite icon, string displayName, string id)
            => Resolve(icon, displayName, id, ByContentId);

        /// <summary>같은 체인을 임의의 매핑 표에 대해 실행한다(액션 버튼은 ByActionId를 쓴다).</summary>
        public static HudIconPick Resolve(Sprite icon, string displayName, string id, Dictionary<string, HudIconCode> table)
        {
            var pick = new HudIconPick();

            if (icon != null) { pick.Tier = HudIconTier.Sprite; pick.Sprite = icon; return pick; }

            if (id != null && table != null && table.TryGetValue(id, out var code))
            {
                if (HasCodepoint(code.Codepoint))
                {
                    pick.Tier = HudIconTier.Glyph;
                    pick.Codepoint = code.Codepoint;
                    pick.Text = char.ConvertFromUtf32(code.Codepoint);
                    return pick;
                }
                // 매핑은 있는데 폰트에 없다 = 서브셋을 잘못 구웠거나 폰트가 안 실렸다. **보고 대상**.
                pick.Error = Font == null
                    ? $"icon font not loaded (Resources/{FontResourcePath}) — id '{id}' maps to {code.LigatureName} U+{code.Codepoint:X4}"
                    : $"codepoint MISSING from atlas: id '{id}' → {code.LigatureName} U+{code.Codepoint:X4}";
            }

            pick.Tier = HudIconTier.Letter;
            pick.Text = FirstLetter(displayName, id);
            return pick;
        }

        static string FirstLetter(string displayName, string id)
        {
            string s = !string.IsNullOrEmpty(displayName) ? displayName : (id ?? "?");
            return s.Length > 0 ? s.Substring(0, 1) : "?";
        }
    }
}
