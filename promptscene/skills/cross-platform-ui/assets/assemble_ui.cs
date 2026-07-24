// /cross-platform-ui — author a REUSABLE cross-platform pointing HUD onto <ROOM> and (XR modes) the XR world-click bridge.
// Procedure SSOT: build-studio-room.md §5 (World Space uGUI + billboard + dynamic OS font + SuppressWorldClick) + §6
// (XRI world-click: XRWorldClicker + SubmitExternalRay, shared Near-Far interactor). Contract §1 (5-layer / registry).
//
// Run via MCP script-execute (className=PS_AssembleUI, methodName=Run) AFTER `scene-open <ROOM> Single` AND after
// CrossPlatformRoomHud.cs (+ XRWorldClicker.cs if absent) have compiled into App.HotUpdate (isCompiling==false).
//
// STRUCTURE (standard uGUI — canvas ≠ panel):
//   RoomHud   (Canvas + CanvasScaler + GraphicRaycaster [+ TrackedDeviceGraphicRaycaster] + CrossPlatformRoomHud binder)
//     └ Panel (Image bg + VerticalLayoutGroup [+ ContentSizeFitter for Screen Space])   <- the small visible box
//         ├ Title (Text) / Buttons (container → ButtonTemplate, INACTIVE) / Count (Text) / Hint (Text)
//   The panel is a CHILD so a Screen Space Overlay ROOT canvas (which Unity drives to full-screen) is NOT itself the
//   background — the panel stays a small corner box. Button sizes match the room's existing toggles (~44px h / 22pt).
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

public class PS_AssembleUI {
    const string ROOM = "PromptSceneRoom_1";   // <-- target room leaf (scene must be open Single)
    const string MODE = "CROSS";               // "PC" | "PCSS" | "PCXR" | "CROSS"
    const string HUD_NAME = "CrossPlatformRoomHud";

    // World Space canvas placement (PC / PCXR / CROSS)
    static readonly Vector3 HUD_POS   = new Vector3(0f, 1.6f, 2.5f);
    static readonly Vector2 HUD_SIZE  = new Vector2(360f, 300f);
    const float HUD_SCALE = 0.0026f;            // metres per canvas unit → ~0.94m x 0.78m
    // Screen Space Overlay panel (PCSS) — small top-left box, height auto-fit
    const float SS_PANEL_WIDTH = 320f;
    static readonly Vector2 SS_PANEL_PAD = new Vector2(16f, 16f);
    // compact look, matched to the room's existing toggles (PlayerList/Leave = 45px h, 24pt)
    const float BTN_H = 44f;  const int BTN_FONT = 22;
    const int TITLE_FONT = 22; const int COUNT_FONT = 15; const int HINT_FONT = 14;

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
            hudRT.localScale = Vector3.one * HUD_SCALE;
        }
        hud.AddComponent<CanvasScaler>();
        hud.AddComponent<GraphicRaycaster>();                            // desktop mouse (InputSystemUIInputModule)
        if(WantsXR) AddByType(hud, "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster"); // XR ray/poke

        // ── PANEL (the small visible box) ──
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(hud.transform, false);
        var panelRT = (RectTransform)panel.transform;
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.07f, 0.10f, 0.86f);
        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12,12,12,12); vlg.spacing = 6;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        if(ScreenSpace){
            // small box pinned top-left, height auto-fit to content
            panelRT.anchorMin = new Vector2(0f,1f); panelRT.anchorMax = new Vector2(0f,1f); panelRT.pivot = new Vector2(0f,1f);
            panelRT.sizeDelta = new Vector2(SS_PANEL_WIDTH, 0f);
            panelRT.anchoredPosition = new Vector2(SS_PANEL_PAD.x, -SS_PANEL_PAD.y);
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        } else {
            // stretch to fill the World Space canvas box
            panelRT.anchorMin = Vector2.zero; panelRT.anchorMax = Vector2.one; panelRT.offsetMin = Vector2.zero; panelRT.offsetMax = Vector2.zero;
        }

        Text MkText(string name, GameObject parent, int size, FontStyle style, float minH){
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // replaced by dynamic OS font at runtime
            txt.fontSize = size; txt.fontStyle = style; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft; txt.horizontalOverflow = HorizontalWrapMode.Wrap; txt.verticalOverflow = VerticalWrapMode.Overflow;
            var le = go.AddComponent<LayoutElement>(); le.minHeight = minH;
            return txt;
        }

        MkText("Title", panel, TITLE_FONT, FontStyle.Bold, 28f).text = "PromptScene — 도구";

        // Buttons container (runtime rows cloned in here by the binder)
        var buttons = new GameObject("Buttons", typeof(RectTransform));
        buttons.transform.SetParent(panel.transform, false);
        var bvlg = buttons.AddComponent<VerticalLayoutGroup>();
        bvlg.spacing = 6; bvlg.childControlWidth=true; bvlg.childControlHeight=true;
        bvlg.childForceExpandWidth=true; bvlg.childForceExpandHeight=false;
        buttons.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ButtonTemplate (INACTIVE — binder clones one per toggleable content), compact ~44px
        var tmpl = new GameObject("ButtonTemplate", typeof(RectTransform));
        tmpl.transform.SetParent(buttons.transform, false);
        var tImg = tmpl.AddComponent<Image>(); tImg.color = new Color(0.18f,0.36f,0.62f,1f);
        var tBtn = tmpl.AddComponent<Button>(); tBtn.targetGraphic = tImg;
        var tLE  = tmpl.AddComponent<LayoutElement>(); tLE.minHeight = BTN_H; tLE.preferredHeight = BTN_H;
        var tLbl = MkText("Label", tmpl, BTN_FONT, FontStyle.Bold, BTN_H);
        tLbl.alignment = TextAnchor.MiddleCenter; tLbl.text = "…";
        var lblRT = (RectTransform)tLbl.transform; lblRT.anchorMin=Vector2.zero; lblRT.anchorMax=Vector2.one; lblRT.offsetMin=Vector2.zero; lblRT.offsetMax=Vector2.zero;
        tmpl.SetActive(false);

        MkText("Count", panel, COUNT_FONT, FontStyle.Normal, 20f).text = "공유 측정: 0 개";
        MkText("Hint",  panel, HINT_FONT, FontStyle.Italic, 32f).text = "도구 ON → 포인팅/클릭으로 사용하고 공유됩니다.";

        // the reusable hot binder on the ROOT canvas (added by reflection — this editor script can't compile-ref App.HotUpdate)
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
        var tdgrType = FindType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
        var expectMode = ScreenSpace ? RenderMode.ScreenSpaceOverlay : RenderMode.WorldSpace;
        bool modeOk = canvas.renderMode == expectMode;
        bool rootHasNoImage = hud.GetComponent<Image>()==null;   // root canvas must NOT be a background
        sb.AppendLine("MODE="+MODE+" saved="+saved);
        sb.AppendLine("hud '"+HUD_NAME+"' under UI="+(hud.transform.parent==ui.transform));
        sb.AppendLine("canvas.renderMode="+canvas.renderMode+" (expect "+expectMode+")  rootHasNoBgImage="+rootHasNoImage);
        sb.AppendLine("Panel bg Image="+(panel.GetComponent<Image>()!=null)+"  panel width="+SS_PANEL_WIDTH+(ScreenSpace?" (ScreenSpace, height auto-fit)":" (WorldSpace stretch)"));
        sb.AppendLine("GraphicRaycaster="+(hud.GetComponent<GraphicRaycaster>()!=null));
        sb.AppendLine("TrackedDeviceGraphicRaycaster="+(tdgrType!=null && hud.GetComponent(tdgrType)!=null)+" (expect "+WantsXR+")");
        sb.AppendLine("CrossPlatformRoomHud comp="+(hud.GetComponent(FindType("CrossPlatformRoomHud"))!=null));
        sb.AppendLine("children under Panel: Title="+(panel.transform.Find("Title")!=null)+" Buttons="+(panel.transform.Find("Buttons")!=null)
                     +" ButtonTemplate="+(buttons.transform.Find("ButtonTemplate")!=null)+"(active="+tmpl.activeSelf+" expect False, h="+BTN_H+")"
                     +" Count="+(panel.transform.Find("Count")!=null)+" Hint="+(panel.transform.Find("Hint")!=null));
        sb.AppendLine("XRWorldClicker under SYSTEMS="+xrClickerPlaced+" (expect "+WantsXR+")");
        bool ok = (hud.GetComponent(FindType("CrossPlatformRoomHud"))!=null) && (WantsXR==xrClickerPlaced) && modeOk && rootHasNoImage && saved;
        sb.AppendLine("=== ASSEMBLE-UI: "+(ok?"OK":"CHECK")+" ===");
        Debug.Log("[PS_AssembleUI]\n"+sb);
    }
}
