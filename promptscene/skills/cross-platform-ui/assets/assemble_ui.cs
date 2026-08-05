// /cross-platform-ui — author a REUSABLE cross-platform pointing HUD onto <ROOM> and (XR modes) the XR world-click bridge.
// Procedure SSOT: build-studio-room.md §5 (World Space uGUI + billboard + dynamic OS font + SuppressWorldClick) + §6
// (XRI world-click: XRWorldClicker + SubmitExternalRay, shared Near-Far interactor). Contract §1 (5-layer / registry).
//
// Run via MCP script-execute (className=PS_AssembleUI, methodName=Run) AFTER `scene-open <ROOM> Single` AND after
// HudTheme / HudIcons / HudPager / KeyBadge / CrossPlatformRoomHud have compiled (isCompiling==false).
//
// ── DESIGN TOKENS: this file authors NO literal colour and NO literal design px. ─────────────────────────────────
// Every colour, size, spacing, radius and weight is read from `PromptScene.Core.UI.HudTheme` (glass v6, copied ONE-WAY
// from the plugin assets — SKILL.md Phase 1b). script-execute compiles against the loaded App.HotUpdate assembly, so
// the tokens are *referenced*, not duplicated. If a value you need is not in HudTheme, do NOT write it here — propose
// a token and stop (SKILL.md ground rules).
//
// The ONLY literal below is HUD_POS (where the panel hangs in the room). Even the panel WIDTH is derived now
// (HudTheme.PanelW = 4 hit boxes + 2 derived paddings), and HUD_SCALE = 1/HudTheme.Legibility.PxPerMeter — so the
// mockup's 1200 px/m transfers 1:1 and Phase 2.5 re-measures it every run.
//
// ══════════════════════════════════════════════════════════════════════════════════════════════════════════════
// glass v6 STRUCTURE — 제목도 문구도 없다. 원 4개/페이지 + 넘길 때만 보이는 점.
// ══════════════════════════════════════════════════════════════════════════════════════════════════════════════
//   RoomHud   (Canvas + CanvasScaler + GraphicRaycaster [+ TrackedDeviceGraphicRaycaster] + CrossPlatformRoomHud)
//     └ Panel              Image = Scrim (rounded) + VerticalLayoutGroup(padding PadX/PadY) + ContentSizeFitter
//         ├ PanelFrame     Image = Rim    (round FRAME 1장, ignoreLayout)   <- 테두리는 형제 한 장. 상자를 겹치지 않는다
//         ├ PanelEdge      Image = RimLit (상단 스트립, ignoreLayout)
//         ├ TopSpacer      높이 DotsRowH                                    <- 점 줄과 같은 높이의 위쪽 **미러** 여백
//         ├ Viewport       RectMask2D + 투명 Image(레이 타깃) + HudPager     <- overflow:hidden
//         │   └ Track      가로로 이어붙인 페이지들
//         │       └ PageTemplate (INACTIVE)  폭 = 4 * HitD
//         │           └ IconButtonTemplate (INACTIVE, Button)  히트박스 HitD
//         │               ├ Disc   Image = Film (원 CircleD)  [Button.targetGraphic]
//         │               │   ├ Glyph  Text  GlyphPx / GlyphDark   <- 대비 스택 = [Scrim, Film] (조상 체인)
//         │               │   └ Icon   Image (Meta.Icon, 기본 OFF)
//         │               ├ Ring   Image = RimTop (2톤 링 1장, 형제·장식)
//         │               └ Label  Text  FontFoot / TextLo + **불투명 아웃라인**
//         └ Dots           HorizontalLayoutGroup(높이 DotsRowH) + CanvasGroup
//             └ DotTemplate (INACTIVE)
//
//   ⭐ 상자 중첩 0: 그려지는 판은 Panel 하나뿐이고, 원은 컨트롤이다. Card는 없다.
//   ⭐ 상태 텍스트(`": ON"`) 없음. ON = 원의 Accent 채움 + 라벨 강조.
//   ⭐ 간격 산술(v6): InnerGap = HitPad*2 = 24, OuterMargin = InnerGap*2 = 48.
//      패널 padding은 손으로 넣지 않고 **유도한다**: PadX = Outer−HitPad = 36, PadY = Outer−HitPad−DotsRowH = 12.
//      바깥이 안쪽보다 넓어야 4개가 "한 덩어리"로 읽힌다(근접성 원리).
//
// ⚠ 절차적 스프라이트(HudSprites)는 HideAndDontSave → 저장된 씬에 직렬화되지 않는다. 여기서 에디터 프리뷰용으로
//   넣고, 바인더가 매 Start 다시 넣는다. 그래서 **검증 대상인 런타임**은 항상 모양을 갖는다.
//
// MODE: "PC" | "PCSS" | "PCXR" | "CROSS"  (add-component §6의 선택지와 1:1)
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using PromptScene.Core.UI;            // HudTheme / HudSprites — the token SSOT

public class PS_AssembleUI {
    const string ROOM = "AssembleRoom";        // <-- target room leaf (scene must be open Single)
    const string MODE = "CROSS";               // "PC" | "PCSS" | "PCXR" | "CROSS"
    const string HUD_NAME = "CrossPlatformRoomHud";

    // 사람이 정한 배치. v6에서 z를 2.5 → 1.5로 당겼다: 1200px/m에서 16px 라벨이 2.5m에서는 13.2'로 하한(20')
    // 아래이고 1.5m에서 22.0'로 통과하기 때문이다. 게이트가 아니라 배치를 옮겨 맞췄다.
    static readonly Vector3 HUD_POS = new Vector3(0f, 1.6f, 1.5f);
    static float HudScale => 1f / HudTheme.Legibility.PxPerMeter;

    static bool WantsXR      => MODE == "PCXR" || MODE == "CROSS";
    static bool ScreenSpace  => MODE == "PCSS";

    static Type FindType(string full){
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){
            var t=a.GetType(full); if(t!=null) return t;
            foreach(var tt in Safe(a)) if(tt.Name==full) return tt;
        }
        return null;
    }
    static Type[] Safe(Assembly a){ try{ return a.GetTypes(); }catch{ return Array.Empty<Type>(); } }
    static Component AddByType(GameObject go, string typeName){
        var t = FindType(typeName);
        if(t==null){ Debug.LogError("[PS_AssembleUI] type not found (not compiled?): "+typeName); return null; }
        var existing = go.GetComponent(t);
        return existing != null ? existing : go.AddComponent(t);
    }

    // ── token-only builders ───────────────────────────────────────────────────────────────────────
    static RectTransform Stretch(GameObject go, float pad = 0f){
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        return rt;
    }

    static Image MkImage(string name, Transform parent, Color tint, Sprite sprite, Image.Type type){
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = tint;
        if (sprite != null) { img.sprite = sprite; img.type = type; }
        return img;
    }

    /// <summary>
    /// Legacy uGUI Text (build-studio-room §5: studio ships no Korean TMP asset → dynamic OS font at runtime).
    /// fontStyle is ALWAYS Normal — faux-bold is banned; emphasis is colour (+ the real 600 weight once a
    /// PyeojinGothic FontAsset exists, which is a baked-base item outside this skill).
    /// </summary>
    static Text MkText(string name, Transform parent, int fontPx, Color color, TextAnchor anchor){
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // replaced by the dynamic OS font at runtime
        txt.fontSize = fontPx;
        txt.fontStyle = FontStyle.Normal;
        txt.color = color;
        txt.alignment = anchor;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    /// <summary>불투명 아웃라인 — 얇은 Scrim 위에서 라벨 대비를 만드는 기계(U7 아웃라인 절).</summary>
    static void Outline(Text t){
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = HudTheme.TextOutline;
        o.effectDistance = new Vector2(HudTheme.OutlineW, HudTheme.OutlineW);
        o.useGraphicAlpha = false;
    }

    static LayoutElement Fixed(GameObject go, float w, float h){
        var le = go.AddComponent<LayoutElement>();
        if(w>0){ le.minWidth = w; le.preferredWidth = w; }
        if(h>0){ le.minHeight = h; le.preferredHeight = h; }
        return le;
    }

    public static void Run(){
        var sb = new StringBuilder();
        var scn = SceneManager.GetSceneByName(ROOM);
        if(!scn.IsValid() || !scn.isLoaded){ Debug.LogError("[PS_AssembleUI] scene not open Single: "+ROOM); return; }
        if(MODE!="PC" && MODE!="PCSS" && MODE!="PCXR" && MODE!="CROSS"){ Debug.LogError("[PS_AssembleUI] bad MODE: "+MODE); return; }
        if(HudTheme.PadY < 0){ Debug.LogError("[PS_AssembleUI] PadY < 0 — 점 줄이 바깥 여백보다 크다. 토큰을 고쳐야 한다."); return; }

        GameObject Root(string n) => scn.GetRootGameObjects().FirstOrDefault(g=>g.name==n);
        GameObject Header(string n){ var e=Root(n); if(e!=null) return e; var go=new GameObject(n); SceneManager.MoveGameObjectToScene(go,scn); return go; }
        var ui      = Header("===== UI =====");
        var systems = Header("===== SYSTEMS =====");

        var prior = ui.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.gameObject.name==HUD_NAME);
        if(prior!=null) UnityEngine.Object.DestroyImmediate(prior.gameObject);

        int cellH = HudTheme.HitPad*2 + HudTheme.CircleD + HudTheme.Space2 + HudTheme.LabelBoxH;  // 172
        int pageW = HudTheme.GridColumns * HudTheme.HitD;                                          // 576

        // ── ROOT canvas (NO graphic — a root Screen Space canvas is full-screen; the visible box is the Panel child) ──
        var hud = new GameObject(HUD_NAME, typeof(RectTransform));
        hud.transform.SetParent(ui.transform, false);
        var canvas = hud.AddComponent<Canvas>();
        var hudRT = (RectTransform)hud.transform;
        if(ScreenSpace){
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
        } else {
            canvas.renderMode = RenderMode.WorldSpace;
            hudRT.sizeDelta = new Vector2(HudTheme.PanelW, cellH + HudTheme.DotsRowH*2 + HudTheme.PadY*2);
            hudRT.position = HUD_POS;
            hudRT.localScale = Vector3.one * HudScale;
        }
        hud.AddComponent<CanvasScaler>();
        hud.AddComponent<GraphicRaycaster>();                            // desktop mouse (InputSystemUIInputModule)
        if(WantsXR) AddByType(hud, "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster"); // XR ray/poke

        // ── PANEL = Scrim + 세로 레이아웃. 유일하게 그려지는 판이다 ──
        var panelImg = MkImage("Panel", hud.transform, HudTheme.Scrim, HudSprites.RoundedRect(HudTheme.Radius), Image.Type.Sliced);
        var panel = panelImg.gameObject;
        var panelRT = (RectTransform)panel.transform;
        if(ScreenSpace){
            panelRT.anchorMin = new Vector2(0f,1f); panelRT.anchorMax = new Vector2(0f,1f); panelRT.pivot = new Vector2(0f,1f);
            panelRT.anchoredPosition = new Vector2(HudTheme.Space4, -HudTheme.Space4);
        } else {
            panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f,0.5f); panelRT.pivot = new Vector2(0.5f,0.5f);
            panelRT.anchoredPosition = Vector2.zero;
        }
        panelRT.sizeDelta = new Vector2(HudTheme.PanelW, 0f);
        // ⚠ ContentSizeFitter는 **같은 오브젝트의 LayoutGroup**에서 preferred 높이를 얻는다. 레이아웃을 자식에
        //   두면 preferred가 -1이 되어 패널이 안 자란다(v3에서 실제로 밟은 함정: 패널이 48px에 머물렀다).
        var pvlg = panel.AddComponent<VerticalLayoutGroup>();
        pvlg.padding = new RectOffset(HudTheme.PadX, HudTheme.PadX, HudTheme.PadY, HudTheme.PadY);
        pvlg.spacing = 0;
        pvlg.childControlWidth = true;  pvlg.childControlHeight = true;
        pvlg.childForceExpandWidth = false; pvlg.childForceExpandHeight = false;
        pvlg.childAlignment = TextAnchor.UpperCenter;
        panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 테두리 = 프레임 스프라이트 한 장 (ignoreLayout이라 레이아웃 자식이 아니다)
        var frame = MkImage("PanelFrame", panel.transform, HudTheme.Rim, HudSprites.RoundedFrame(HudTheme.Radius, HudTheme.BorderW), Image.Type.Sliced);
        Stretch(frame.gameObject); frame.raycastTarget = false;
        frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var edge = MkImage("PanelEdge", panel.transform, HudTheme.RimLit, null, Image.Type.Simple);
        edge.raycastTarget = false;
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var edgeRT = (RectTransform)edge.transform;
        edgeRT.anchorMin = new Vector2(0f,1f); edgeRT.anchorMax = new Vector2(1f,1f); edgeRT.pivot = new Vector2(0.5f,1f);
        edgeRT.offsetMin = new Vector2(HudTheme.Radius, -HudTheme.BorderW*2);
        edgeRT.offsetMax = new Vector2(-HudTheme.Radius, -HudTheme.BorderW);

        // ── 위쪽 미러 여백: 점 줄과 같은 높이. 패널 padding만 키우지 않는 이유 = 점 줄 높이가 바뀌면 위도 따라와야 한다
        var topSpacer = new GameObject("TopSpacer", typeof(RectTransform));
        topSpacer.transform.SetParent(panel.transform, false);
        Fixed(topSpacer, 0, HudTheme.DotsRowH);

        // ── VIEWPORT: overflow:hidden + 드래그/휠 수신. 투명 Image가 레이 타깃이 된다(토큰의 알파0 은닉) ──
        var vpTint = HudTheme.Scrim; vpTint.a = 0f;
        var viewport = MkImage("Viewport", panel.transform, vpTint, null, Image.Type.Simple);
        Fixed(viewport.gameObject, pageW, cellH);
        viewport.gameObject.AddComponent<RectMask2D>();
        AddByType(viewport.gameObject, "HudPager");

        var track = new GameObject("Track", typeof(RectTransform));
        track.transform.SetParent(viewport.transform, false);
        var trackRT = (RectTransform)track.transform;
        trackRT.anchorMin = new Vector2(0f,0f); trackRT.anchorMax = new Vector2(0f,1f); trackRT.pivot = new Vector2(0f,0.5f);
        trackRT.sizeDelta = new Vector2(pageW, 0f); trackRT.anchoredPosition = Vector2.zero;
        var thlg = track.AddComponent<HorizontalLayoutGroup>();
        thlg.spacing = 0; thlg.childControlWidth = true; thlg.childControlHeight = true;
        thlg.childForceExpandWidth = false; thlg.childForceExpandHeight = true;
        track.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── PageTemplate (INACTIVE — 바인더가 페이지마다 하나씩 복제) ──
        var page = new GameObject("PageTemplate", typeof(RectTransform));
        page.transform.SetParent(track.transform, false);
        Fixed(page, pageW, cellH);
        var phlg = page.AddComponent<HorizontalLayoutGroup>();
        phlg.spacing = 0; phlg.childControlWidth = true; phlg.childControlHeight = true;
        phlg.childForceExpandWidth = false; phlg.childForceExpandHeight = false;
        phlg.childAlignment = TextAnchor.UpperLeft;

        // ── IconButtonTemplate (INACTIVE — 토글러블/액션 1개당 1개 복제). 히트박스 144, 시각 원 120 ──
        // ⚠ PageTemplate **밖**(Panel 직속)에 둔다. 안에 두면 페이지를 복제할 때 템플릿이 따라 복제되어
        //   각 페이지마다 유령 셀이 하나씩 생긴다(2026-07-30 실측: U6가 이름 없는 Glyph 48px 2개를 잡아냈다).
        var cell = new GameObject("IconButtonTemplate", typeof(RectTransform));
        cell.transform.SetParent(panel.transform, false);
        cell.AddComponent<LayoutElement>().ignoreLayout = true;
        var cBtn = cell.AddComponent<Button>();
        Fixed(cell, HudTheme.HitD, cellH);
        var cvlg = cell.AddComponent<VerticalLayoutGroup>();
        cvlg.padding = new RectOffset(HudTheme.HitPad, HudTheme.HitPad, HudTheme.HitPad, HudTheme.HitPad);
        cvlg.spacing = HudTheme.Space2;
        cvlg.childControlWidth = true; cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = false; cvlg.childForceExpandHeight = false;
        cvlg.childAlignment = TextAnchor.UpperCenter;

        var disc = MkImage("Tmpl"+HudTheme.Roles.Disc, cell.transform, HudTheme.Film, HudSprites.Circle(HudTheme.CircleD), Image.Type.Simple);
        Fixed(disc.gameObject, HudTheme.CircleD, HudTheme.CircleD);
        cBtn.targetGraphic = disc;

        // Glyph는 Disc의 **자식**이다 → 조상 체인 [Scrim, Film]이 곧 U7의 대비 스택이 된다.
        var glyph = MkText("Tmpl"+HudTheme.Roles.Glyph, disc.transform, HudTheme.GlyphPx, HudTheme.GlyphDark, TextAnchor.MiddleCenter);
        glyph.text = "?"; Stretch(glyph.gameObject);
        var icon = MkImage("Tmpl"+HudTheme.Roles.Icon, disc.transform, HudTheme.GlyphDark, null, Image.Type.Simple);
        var iconRT = (RectTransform)icon.transform;
        iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f,0.5f); iconRT.pivot = new Vector2(0.5f,0.5f);
        iconRT.sizeDelta = Vector2.one * HudTheme.GlyphPx;
        icon.preserveAspect = true; icon.raycastTarget = false;
        icon.gameObject.SetActive(false);

        // Ring = 형제·장식. Disc와 같은 자리에 놓되 레이아웃에서 뺀다.
        var ring = MkImage("Tmpl"+HudTheme.Roles.Ring, disc.transform, HudTheme.RimTop,
                           HudSprites.RingGraded(HudTheme.CircleD, HudTheme.RimW, HudTheme.RimBot.a/HudTheme.RimTop.a), Image.Type.Simple);
        Stretch(ring.gameObject); ring.raycastTarget = false;

        // Label = 원 밖 아래, 1줄 고정. 대비는 불투명 아웃라인이 만든다.
        var lbl = MkText("Tmpl"+HudTheme.Roles.Label, cell.transform, HudTheme.FontFoot, HudTheme.TextLo, TextAnchor.MiddleCenter);
        lbl.text = "…";
        Fixed(lbl.gameObject, 0, HudTheme.LabelBoxH);
        cell.SetActive(false);
        page.SetActive(false);

        // ── DOTS: 자리는 항상 차지하고 alpha만 바뀐다(나타날 때 패널이 안 흔들리게) ──
        var dots = new GameObject("Dots", typeof(RectTransform));
        dots.transform.SetParent(panel.transform, false);
        Fixed(dots, 0, HudTheme.DotsRowH);
        var dhlg = dots.AddComponent<HorizontalLayoutGroup>();
        dhlg.spacing = HudTheme.Space2; dhlg.childAlignment = TextAnchor.MiddleCenter;
        dhlg.childControlWidth = true; dhlg.childControlHeight = true;
        dhlg.childForceExpandWidth = false; dhlg.childForceExpandHeight = false;
        dots.AddComponent<CanvasGroup>().alpha = 0f;

        var dot = MkImage("DotTemplate", dots.transform, HudTheme.Dot, HudSprites.Circle(HudTheme.Space2), Image.Type.Simple);
        Fixed(dot.gameObject, HudTheme.Space2, HudTheme.Space2);
        dot.raycastTarget = false;
        dot.gameObject.SetActive(false);

        AddByType(hud, "CrossPlatformRoomHud");

        // ── XR world-click bridge under SYSTEMS (XR-capable modes only) ────
        bool xrClickerPlaced = false;
        var priorClicker = systems.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.gameObject.name=="XRWorldClicker");
        if(WantsXR){
            GameObject clicker = priorClicker!=null ? priorClicker.gameObject : new GameObject("XRWorldClicker");
            if(priorClicker==null) clicker.transform.SetParent(systems.transform, false);
            AddByType(clicker, "XRWorldClicker");
            xrClickerPlaced = clicker.GetComponent(FindType("XRWorldClicker"))!=null;
        } else if(priorClicker!=null){
            UnityEngine.Object.DestroyImmediate(priorClicker.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(scn);
        bool saved = EditorSceneManager.SaveScene(scn);

        // ── read-back ──────────────────────────────────────────────────────
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRT);
        var tdgrType = FindType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
        var expectMode = ScreenSpace ? RenderMode.ScreenSpaceOverlay : RenderMode.WorldSpace;
        bool modeOk = canvas.renderMode == expectMode;
        bool rootHasNoImage = hud.GetComponent<Image>()==null;
        float pxPerMeter = ScreenSpace ? HudTheme.Legibility.PxPerMeter : 1f / canvas.transform.lossyScale.x;
        bool scaleOk = ScreenSpace || Mathf.Abs(pxPerMeter - HudTheme.Legibility.PxPerMeter) <= HudTheme.Legibility.PxPerMeter * 0.05f;
        float d = HudTheme.Legibility.PlacementDistanceM;

        sb.AppendLine("MODE="+MODE+" saved="+saved);
        sb.AppendLine("hud '"+HUD_NAME+"' under UI="+(hud.transform.parent==ui.transform));
        sb.AppendLine("canvas.renderMode="+canvas.renderMode+" (expect "+expectMode+")  rootHasNoBgImage="+rootHasNoImage);
        sb.AppendLine("panelPx="+panelRT.rect.width.ToString("F0")+"x"+panelRT.rect.height.ToString("F0")
                     +"  (derived PanelW="+HudTheme.PanelW+", height content-driven)");
        sb.AppendLine("PHASE 2.5 measured pxPerMeter="+pxPerMeter.ToString("F1")+" vs token "+HudTheme.Legibility.PxPerMeter
                     +" (±5% → "+(scaleOk?"OK":"STOP & REPORT")+")");
        sb.AppendLine("spacing 산술: HitPad="+HudTheme.HitPad+" InnerGap="+HudTheme.InnerGap+" OuterMargin="+HudTheme.OuterMargin
                     +" → PadX="+HudTheme.PadX+" PadY="+HudTheme.PadY+" (유도값, 손으로 넣지 않음)");
        sb.AppendLine("cell="+HudTheme.HitD+"x"+cellH+" (원 "+HudTheme.CircleD+" + 라벨 "+HudTheme.LabelBoxH+")  page="+pageW+"x"+cellH);
        sb.AppendLine("Card count=0  그려지는 판=Panel 1개 (중첩 0)");
        sb.AppendLine("templates: PageTemplate(active="+page.activeSelf+") IconButtonTemplate(active="+cell.activeSelf
                     +") DotTemplate(active="+dot.gameObject.activeSelf+")  [expect all False]");
        sb.AppendLine("parts(역할 접미사로 authoring): "+disc.name+" / "+glyph.name+" / "+icon.name+" / "+ring.name+" / "+lbl.name);
        sb.AppendLine("Viewport RectMask2D="+(viewport.GetComponent<RectMask2D>()!=null)
                     +" HudPager="+(viewport.GetComponent(FindType("HudPager"))!=null)+"  Dots CanvasGroup="+(dots.GetComponent<CanvasGroup>()!=null));
        sb.AppendLine("GraphicRaycaster="+(hud.GetComponent<GraphicRaycaster>()!=null)
                     +"  TrackedDeviceGraphicRaycaster="+(tdgrType!=null && hud.GetComponent(tdgrType)!=null)+" (expect "+WantsXR+")");
        sb.AppendLine("CrossPlatformRoomHud comp="+(hud.GetComponent(FindType("CrossPlatformRoomHud"))!=null));
        sb.AppendLine("angular @"+d+"m: panel="+HudTheme.Legibility.Deg(HudTheme.PanelW,d).ToString("F1")+"deg"
                     +"  circle="+HudTheme.Legibility.Deg(HudTheme.CircleD,d).ToString("F2")+"deg"
                     +"  hit="+HudTheme.Legibility.Deg(HudTheme.HitD,d).ToString("F2")+"deg"
                     +"  label cap="+HudTheme.Legibility.CapArcmin(HudTheme.FontFoot,HudTheme.Legibility.PxPerMeter,d).ToString("F1")+"'"
                     +"  glyph cap="+HudTheme.Legibility.CapArcmin(HudTheme.GlyphPx,HudTheme.Legibility.PxPerMeter,d).ToString("F0")+"'");
        sb.AppendLine("XRWorldClicker under SYSTEMS="+xrClickerPlaced+" (expect "+WantsXR+")");
        bool ok = (hud.GetComponent(FindType("CrossPlatformRoomHud"))!=null) && (WantsXR==xrClickerPlaced) && modeOk
                  && rootHasNoImage && saved && scaleOk;
        sb.AppendLine("=== ASSEMBLE-UI: "+(ok?"OK":"CHECK")+" ===");
        Debug.Log("[PS_AssembleUI]\n"+sb);
    }
}
