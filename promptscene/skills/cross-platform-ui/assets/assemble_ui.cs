// /cross-platform-ui — author a REUSABLE cross-platform pointing HUD onto <ROOM> and (XR modes) the XR world-click bridge.
// Procedure SSOT: build-studio-room.md §5 (World Space uGUI + billboard + dynamic OS font + SuppressWorldClick) + §6
// (XRI world-click: XRWorldClicker + SubmitExternalRay, shared Near-Far interactor). Contract §1 (5-layer / registry).
//
// Run via MCP script-execute (className=PS_AssembleUI, methodName=Run) AFTER `scene-open <ROOM> Single` AND after
// HudTheme.cs + CrossPlatformRoomHud.cs (+ XRWorldClicker.cs if absent) have compiled into App.HotUpdate
// (isCompiling==false).
//
// ── DESIGN TOKENS: this file authors NO literal colour and NO literal design px. ─────────────────────────────────
// Every colour, size, spacing, radius and weight is read from `PromptScene.Core.UI.HudTheme` (the frozen glass-v0
// theme, copied ONE-WAY from the plugin assets — see SKILL.md Phase 1b). script-execute compiles against the loaded
// App.HotUpdate assembly, so the tokens are *referenced*, not duplicated: change the theme and this file follows.
// If a value you need is not in HudTheme, do NOT write it here — propose a token and stop (SKILL.md ground rules).
//
// The ONLY numeric literals below are MEASURED GEOMETRY, not design values, and they are deliberately frozen:
//   HUD_SIZE / HUD_POS — the panel box + placement a human already click-verified with the XR sim controller.
//   HUD_SCALE is NOT a literal any more: it is derived as 1 / HudTheme.Legibility.PxPerMeter, so the measured
//   px-per-metre in the theme now *drives* the canvas scale instead of merely describing it (drift impossible).
//
// STRUCTURE (standard uGUI — canvas ≠ panel; borders are Image nesting, per the glass-v0 mockup):
//   RoomHud   (Canvas + CanvasScaler + GraphicRaycaster [+ TrackedDeviceGraphicRaycaster] + CrossPlatformRoomHud)
//     └ Panel        Image = Hairline  (rounded, Sliced)                  <- OUTER 1px border
//         ├ PanelFill  Image = PanelTint (rounded, Sliced), inset BorderW <- the glass tint. NO TEXT ON THIS.
//         ├ PanelEdge  Image = HairlineLit, top strip h = BorderW         <- fake specular (top edge only)
//         └ Content    VerticalLayoutGroup(padding Space3, spacing Space2)
//             ├ TitleCard  Image = Card  → Title (FontMd, TextHi)
//             ├ Buttons    VerticalLayoutGroup(spacing Space2) + ContentSizeFitter
//             │   └ ButtonTemplate  Image = Hairline + Button + LayoutElement h = Space6   (INACTIVE)
//             │       └ RowFill     Image = Card, inset BorderW
//             │           ├ Bar     Image = Accent (alpha 0 when OFF), w = BarW   <- the ONE accent meaning
//             │           └ Label   Text  (FontSm)
//             ├ CountCard  Image = Card  → Count (FontSm, TextLo)
//             └ HintCard   Image = Card  → Hint  (FontSm, TextLo)
//   Text ALWAYS sits on a Card (alpha .92), never on PanelTint (alpha .62) — that is what keeps contrast from
//   collapsing against a bright or busy room background (verify U7 "text plate alpha").
//   The panel is a CHILD so a Screen Space Overlay ROOT canvas (which Unity drives to full-screen) is NOT itself the
//   background — the panel stays a small corner box.
//
// ⚠ Rounded sprites are procedural (HudSprites) with HideFlags.HideAndDontSave → they do NOT serialize into the saved
//   scene. We assign them here for immediate editor preview AND the binder re-assigns them every Start, so the
//   RUNTIME (the thing we verify) always has the radius even after a scene reload. Colours/alpha do persist.
//
// MODE (set below) — matches the add-component §6 options; all are cross-platform-READY structure, live-verified to
// desktop mouse + XR Interaction Simulator CONTROLLER (real devices = V2):
//   "PC"    PC검증용(World Space)   — World Space Canvas + GraphicRaycaster only.
//   "PCSS"  PC검증용(Screen Space)  — Screen Space Overlay + GraphicRaycaster only (desktop-only 2D, no billboard/XR).
//   "PCXR"  PC+XR(sim)             — World Space + TrackedDeviceGraphicRaycaster + XRWorldClicker.
//   "CROSS" 크로스플랫폼 대비          — identical to PCXR (shared Near-Far interactor → hand covered in code); framing only.
//
// REUSABLE: reads nothing room-specific — the binder binds itself from RoomCore's registry at runtime. Idempotent:
// re-running replaces the skill's own "CrossPlatformRoomHud" object and never touches other UI.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

    // ── MEASURED GEOMETRY (frozen: a human click-verified this box with the XR sim controller). NOT design tokens. ──
    static readonly Vector2 HUD_SIZE = new Vector2(360f, 300f);          // canvas px  → 0.936 m x 0.780 m
    static readonly Vector3 HUD_POS  = new Vector3(0f, 1.6f, 2.5f);      // world placement
    // metres per canvas unit, DERIVED from the measured token (STEP 0): 1 / 384.6 = 0.0026
    static float HudScale => 1f / HudTheme.Legibility.PxPerMeter;
    // Screen Space Overlay panel (PCSS) — small top-left box, height auto-fit. Width = the same measured panel width.
    static float SsPanelWidth => HUD_SIZE.x;

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
    /// <summary>Stretch a RectTransform over its parent, inset by `pad` px on every side.</summary>
    static RectTransform Stretch(GameObject go, float pad = 0f){
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        return rt;
    }

    /// <summary>An Image tinted with a theme colour. `radius` &lt; 0 = square (no sprite).</summary>
    static Image MkImage(string name, GameObject parent, Color tint, int radius){
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = tint;
        if (radius >= 0) { img.sprite = HudSprites.RoundedRect(radius); img.type = Image.Type.Sliced; }
        return img;
    }

    /// <summary>
    /// A Card plate — the ONLY surface text is allowed on.
    /// The card carries its own VerticalLayoutGroup (padding = Space2) and NO fixed height, so it reports a
    /// preferred height derived from its wrapped text and the parent Content group sizes it to fit.
    /// ⚠ Why not a fixed height: a fixed-height card lets a 2-line label spill OUT of the plate onto PanelTint —
    /// the U7 "text plate" rule then passes on hierarchy while the pixels say otherwise (caught 2026-07-30 in the
    /// U8 capture: the wrapped hint's 2nd line rendered on bare tint). Content-driven height makes the plate rule
    /// geometrically true, and U7 now also asserts containment.
    /// </summary>
    static GameObject MkCard(string name, GameObject parent, int minHeightToken){
        var img = MkImage(name, parent, HudTheme.Card, HudTheme.Radius);
        var vlg = img.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(HudTheme.Space2, HudTheme.Space2, HudTheme.Space2, HudTheme.Space2);
        vlg.spacing = 0;
        vlg.childControlWidth = true;  vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var le = img.gameObject.AddComponent<LayoutElement>();
        le.minHeight = minHeightToken;              // floor only — the card grows past it when the text wraps
        return img.gameObject;
    }

    /// <summary>
    /// Legacy uGUI Text (build-studio-room §5: studio ships no Korean TMP asset → dynamic OS font at runtime).
    /// fontStyle is ALWAYS Normal — faux-bold is banned by the theme; emphasis is colour (+ real 600 weight once a
    /// PyeojinGothic FontAsset exists, which is a baked-base item outside this skill).
    /// </summary>
    /// <param name="stretch">
    /// true  = pin the text over its parent (used inside a fixed-height row, where the parent supplies the padding).
    /// false = leave it a plain LAYOUT CHILD so its wrapped preferred height drives the enclosing Card's height.
    /// </param>
    static Text MkText(string name, GameObject parent, int fontToken, Color color, TextAnchor anchor, bool stretch){
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // replaced by dynamic OS font at runtime
        txt.fontSize = fontToken;
        txt.fontStyle = FontStyle.Normal;
        txt.color = color;
        txt.alignment = anchor;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        if (stretch) Stretch(go, HudTheme.Space2);   // inner padding = one space step, never a raw number
        return txt;
    }

    public static void Run(){
        var sb = new StringBuilder();
        var scn = SceneManager.GetSceneByName(ROOM);
        if(!scn.IsValid() || !scn.isLoaded){ Debug.LogError("[PS_AssembleUI] scene not open Single: "+ROOM); return; }
        if(MODE!="PC" && MODE!="PCSS" && MODE!="PCXR" && MODE!="CROSS"){ Debug.LogError("[PS_AssembleUI] bad MODE: "+MODE); return; }

        GameObject Root(string n) => scn.GetRootGameObjects().FirstOrDefault(g=>g.name==n);
        GameObject Header(string n){ var e=Root(n); if(e!=null) return e; var go=new GameObject(n); SceneManager.MoveGameObjectToScene(go,scn); return go; }
        var ui      = Header("===== UI =====");
        var systems = Header("===== SYSTEMS =====");

        // --- idempotent: drop any prior copy of OUR hud (never touch other UI objects) ---
        var prior = ui.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.gameObject.name==HUD_NAME);
        if(prior!=null) UnityEngine.Object.DestroyImmediate(prior.gameObject);

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
            hudRT.sizeDelta = HUD_SIZE;
            hudRT.position = HUD_POS;
            hudRT.localScale = Vector3.one * HudScale;      // derived from HudTheme.Legibility.PxPerMeter
        }
        hud.AddComponent<CanvasScaler>();
        hud.AddComponent<GraphicRaycaster>();                            // desktop mouse (InputSystemUIInputModule)
        if(WantsXR) AddByType(hud, "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster"); // XR ray/poke

        // ── PANEL = the small visible box. Image nesting makes the 1px border (STEP 2.3):
        //    Panel(Hairline) > PanelFill(PanelTint, inset BorderW) + PanelEdge(HairlineLit, top strip)
        var panelImg = MkImage("Panel", hud, HudTheme.Hairline, HudTheme.Radius);
        var panel = panelImg.gameObject;
        var panelRT = (RectTransform)panel.transform;
        if(ScreenSpace){
            // small box pinned top-left, height auto-fit to content
            panelRT.anchorMin = new Vector2(0f,1f); panelRT.anchorMax = new Vector2(0f,1f); panelRT.pivot = new Vector2(0f,1f);
            panelRT.sizeDelta = new Vector2(SsPanelWidth, 0f);
            panelRT.anchoredPosition = new Vector2(HudTheme.Space3, -HudTheme.Space3);
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        } else {
            Stretch(panel);                                  // fill the World Space canvas box
        }

        // glass tint, inset by the border width so the outer Hairline reads as a 1px rim
        var fill = MkImage("PanelFill", panel, HudTheme.PanelTint, HudTheme.Radius);
        Stretch(fill.gameObject, HudTheme.BorderW);

        // top edge only = fake specular. Drawn after PanelFill so it sits on top.
        var edge = MkImage("PanelEdge", panel, HudTheme.HairlineLit, -1);
        var edgeRT = (RectTransform)edge.transform;
        edgeRT.anchorMin = new Vector2(0f,1f); edgeRT.anchorMax = new Vector2(1f,1f); edgeRT.pivot = new Vector2(0.5f,1f);
        edgeRT.offsetMin = new Vector2(HudTheme.Radius, -HudTheme.BorderW*2);
        edgeRT.offsetMax = new Vector2(-HudTheme.Radius, -HudTheme.BorderW);

        // ── CONTENT (the layout column). Kept separate from Panel so PanelFill/PanelEdge are not layout children. ──
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);
        Stretch(content, HudTheme.BorderW);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(HudTheme.Space3, HudTheme.Space3, HudTheme.Space3, HudTheme.Space3);
        vlg.spacing = HudTheme.Space2;
        vlg.childControlWidth = true;  vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        if(ScreenSpace) content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title — the one place FontMd appears (2 sizes per panel max: FontMd + FontSm)
        var titleCard = MkCard("TitleCard", content, HudTheme.Space5);
        MkText("Title", titleCard, HudTheme.FontMd, HudTheme.TextHi, TextAnchor.MiddleLeft, false).text = "PromptScene — 도구";

        // Buttons container (runtime rows cloned in here by the binder)
        var buttons = new GameObject("Buttons", typeof(RectTransform));
        buttons.transform.SetParent(content.transform, false);
        var bvlg = buttons.AddComponent<VerticalLayoutGroup>();
        bvlg.spacing = HudTheme.Space2;
        bvlg.childControlWidth=true; bvlg.childControlHeight=true;
        bvlg.childForceExpandWidth=true; bvlg.childForceExpandHeight=false;
        buttons.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── ButtonTemplate (INACTIVE — binder clones one per toggleable content) ──
        //    Outer = Hairline border, RowFill = Card (the text plate), Bar = the single accent meaning.
        var tmplImg = MkImage("ButtonTemplate", buttons, HudTheme.Hairline, HudTheme.Radius);
        var tmpl = tmplImg.gameObject;
        var tBtn = tmpl.AddComponent<Button>();
        var tLE  = tmpl.AddComponent<LayoutElement>();
        tLE.minHeight = HudTheme.Space6; tLE.preferredHeight = HudTheme.Space6;   // 48px tap target (44 is off-scale)

        var rowFill = MkImage("RowFill", tmpl, HudTheme.Card, HudTheme.Radius);
        Stretch(rowFill.gameObject, HudTheme.BorderW);
        tBtn.targetGraphic = rowFill;                       // tint the card, not the 1px rim

        // accent bar: left edge, width BarW, alpha 0 until the feature is enabled (binder drives it)
        var bar = MkImage("Bar", rowFill.gameObject, HudTheme.Accent, HudTheme.BarW);
        var barRT = (RectTransform)bar.transform;
        barRT.anchorMin = new Vector2(0f,0f); barRT.anchorMax = new Vector2(0f,1f); barRT.pivot = new Vector2(0f,0.5f);
        barRT.offsetMin = new Vector2(HudTheme.Space2, HudTheme.Space2);
        barRT.offsetMax = new Vector2(HudTheme.Space2 + HudTheme.BarW, -HudTheme.Space2);
        var barCol = HudTheme.Accent; barCol.a = 0f; bar.color = barCol;           // OFF by default

        var tLbl = MkText("Label", rowFill.gameObject, HudTheme.FontSm, HudTheme.TextLo, TextAnchor.MiddleLeft, true);
        tLbl.text = "…";
        // leave room for the bar on the left
        var lblRT = (RectTransform)tLbl.transform;
        lblRT.offsetMin = new Vector2(HudTheme.Space2 + HudTheme.BarW + HudTheme.Space2, HudTheme.Space1);
        lblRT.offsetMax = new Vector2(-HudTheme.Space2, -HudTheme.Space1);
        tmpl.SetActive(false);

        // Count (Ruler-only; the binder hides the whole card when no Ruler is present) + Hint
        var countCard = MkCard("CountCard", content, HudTheme.Space4);
        MkText("Count", countCard, HudTheme.FontSm, HudTheme.TextLo, TextAnchor.MiddleLeft, false).text = "공유 측정: 0 개";
        var hintCard = MkCard("HintCard", content, HudTheme.Space5);   // floor; grows to fit the wrapped 2 lines
        MkText("Hint",  hintCard, HudTheme.FontSm, HudTheme.TextLo, TextAnchor.UpperLeft, false).text = "도구 ON → 포인팅/클릭으로 사용하고 공유됩니다.";

        // the reusable hot binder on the ROOT canvas (added by reflection — the type name carries no namespace)
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
            UnityEngine.Object.DestroyImmediate(priorClicker.gameObject); // PC mode: remove a stale bridge if re-applied
        }

        EditorSceneManager.MarkSceneDirty(scn);
        bool saved = EditorSceneManager.SaveScene(scn);

        // ── read-back ──────────────────────────────────────────────────────
        Canvas.ForceUpdateCanvases();
        var tdgrType = FindType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
        var expectMode = ScreenSpace ? RenderMode.ScreenSpaceOverlay : RenderMode.WorldSpace;
        bool modeOk = canvas.renderMode == expectMode;
        bool rootHasNoImage = hud.GetComponent<Image>()==null;   // root canvas must NOT be a background
        float pxPerMeter = ScreenSpace ? HudTheme.Legibility.PxPerMeter : 1f / canvas.transform.lossyScale.x;
        bool scaleOk = ScreenSpace || Mathf.Abs(pxPerMeter - HudTheme.Legibility.PxPerMeter) <= HudTheme.Legibility.PxPerMeter * 0.05f;

        sb.AppendLine("MODE="+MODE+" saved="+saved);
        sb.AppendLine("hud '"+HUD_NAME+"' under UI="+(hud.transform.parent==ui.transform));
        sb.AppendLine("canvas.renderMode="+canvas.renderMode+" (expect "+expectMode+")  rootHasNoBgImage="+rootHasNoImage);
        sb.AppendLine("Panel bg Image="+(panel.GetComponent<Image>()!=null)+"  panelPx="+panelRT.rect.width.ToString("F0")+"x"+panelRT.rect.height.ToString("F0"));
        sb.AppendLine("PHASE 2.5 measured pxPerMeter="+pxPerMeter.ToString("F1")+" vs token "+HudTheme.Legibility.PxPerMeter+" (±5% → "+(scaleOk?"OK":"STOP & REPORT")+")");
        sb.AppendLine("border nesting: PanelFill="+(panel.transform.Find("PanelFill")!=null)+" PanelEdge="+(panel.transform.Find("PanelEdge")!=null)+" Content="+(panel.transform.Find("Content")!=null));
        sb.AppendLine("cards: TitleCard/CountCard/HintCard="+(content.transform.Find("TitleCard")!=null)+"/"+(content.transform.Find("CountCard")!=null)+"/"+(content.transform.Find("HintCard")!=null));
        sb.AppendLine("GraphicRaycaster="+(hud.GetComponent<GraphicRaycaster>()!=null));
        sb.AppendLine("TrackedDeviceGraphicRaycaster="+(tdgrType!=null && hud.GetComponent(tdgrType)!=null)+" (expect "+WantsXR+")");
        sb.AppendLine("CrossPlatformRoomHud comp="+(hud.GetComponent(FindType("CrossPlatformRoomHud"))!=null));
        sb.AppendLine("row: ButtonTemplate(active="+tmpl.activeSelf+" expect False) h="+HudTheme.Space6+" RowFill="+(tmpl.transform.Find("RowFill")!=null)
                     +" Bar="+(rowFill.transform.Find("Bar")!=null)+" Label="+(rowFill.transform.Find("Label")!=null));
        sb.AppendLine("fonts: Title="+HudTheme.FontMd+" body="+HudTheme.FontSm+" (2 sizes max)  tapTarget="+HudTheme.Legibility.CapArcmin(HudTheme.FontSm).ToString("F0")+"' cap");
        sb.AppendLine("XRWorldClicker under SYSTEMS="+xrClickerPlaced+" (expect "+WantsXR+")");
        bool ok = (hud.GetComponent(FindType("CrossPlatformRoomHud"))!=null) && (WantsXR==xrClickerPlaced) && modeOk && rootHasNoImage && saved && scaleOk;
        sb.AppendLine("=== ASSEMBLE-UI: "+(ok?"OK":"CHECK")+" ===");
        Debug.Log("[PS_AssembleUI]\n"+sb);
    }
}
