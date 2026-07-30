// /cross-platform-ui — QuickTest verify of the authored World Space HUD. SSOT: build-studio-room.md §4 (QuickTest) + §5/§6.
// Run via MCP script-execute (className=PS_VerifyUI). Same host-QuickTest driver shape as assemble-room's verify_quicktest:
//   0) MCP: scene-open Assets/App/Scenes/QuickStart.unity Single
//   1) PS_VerifyUI.Setup    — snapshot QuickTestStarter, set startAsServer+hostMode+roomSceneKey=<ROOM>
//   2) (isPlaying=true), wait ~12-15s (server start → Addressables room load → spawn → RoomCore up → binder wires)
//   3) PS_VerifyUI.Check     — writes UI signals to <project>/Temp/ps_ui_result.txt (Read it)
//   4) (isPlaying=false)
//   5) PS_VerifyUI.Teardown  — restore QuickTestStarter (in-memory; QuickStart disk untouched)
//
// What Check proves:
//   U1..U5  the HUD is authored & cross-platform-wired, the binder self-wired from the REGISTRY, an injected
//           button-click drives the content's SetEnabled, and existing UI is intact.
//   U6..U7  ⭐ THE DESIGN FLOOR, now machine-judged: type discipline (sizes on scale, angular size above the
//           legibility floor, ≤2 sizes per panel, no faux-bold) and colour/spacing/contrast discipline (spacing on
//           the space scale, ZERO literal colours, accent = exactly one meaning, every text on a ≥.85 alpha plate,
//           tap targets above the angular floor). These walk the REAL HUD in Play — a stronger signal than a static
//           lint, because it judges what actually rendered.
//   U8      a capture. EVIDENCE ONLY — deliberately NOT part of PASS/FAIL.
//
// Honesty: injected onClick.Invoke() proves the onClick→SetEnabled path; it is NOT a real pointer/interactor event
// (that = desktop mouse by a human + XR Simulator controller by a human; §5 caveat). And the floor is not taste:
// U6/U7 prove the HUD OBEYS the frozen theme; whether it looks GOOD is unverified and stays a human/vision call.
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.SceneManagement;
using PromptScene.Core.UI;                // HudTheme — the token SSOT the gates judge against

public class PS_VerifyUI {
    const string ROOM = "AssembleRoom";       // leaf == Addressables address == roomSceneKey (match assemble_ui.cs)
    const bool EXPECT_XR = true;              // true for MODE PCXR/CROSS, false for PC/PCSS
    const bool EXPECT_SCREENSPACE = false;    // true for MODE PCSS (ScreenSpaceOverlay), false for World Space modes
    const string CLEARABLE_ID = "ruler";      // the toggleable used for the click-injection path proof
    const float EPS = 1f / 255f;              // colour comparison tolerance (one 8-bit step)

    static string TmpDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
    static string OrigF  => Path.Combine(TmpDir, "ps_ui_orig.txt");
    static string OutF   => Path.Combine(TmpDir, "ps_ui_result.txt");
    static string CapF   => Path.Combine(TmpDir, "ps_ui_capture.png");

    static Type FindType(string full){
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){
            var t=a.GetType(full); if(t!=null) return t;
            foreach(var tt in Safe(a)) if(tt.Name==full) return tt;
        }
        return null;
    }
    static Type[] Safe(Assembly a){ try{ return a.GetTypes(); }catch{ return Array.Empty<Type>(); } }
    static object Prop(object o,string n){ if(o==null) return null; var p=o.GetType().GetProperty(n, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); return p?.GetValue(o); }
    static object Field(object o,string n){ if(o==null) return null; var f=o.GetType().GetField(n, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); return f?.GetValue(o); }
    static UnityEngine.Object FindStarter(){ var t=FindType("QuickTestStarter"); if(t==null) return null; var a=UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None); return a.Length>0?(UnityEngine.Object)a[0]:null; }

    // ── the theme, by reflection: any token added to HudTheme is picked up with no edit here ──────────────
    static List<KeyValuePair<string,Color>> ThemeColors(){
        var list=new List<KeyValuePair<string,Color>>();
        foreach(var f in typeof(HudTheme).GetFields(BindingFlags.Static|BindingFlags.Public))
            if(f.FieldType==typeof(Color)) list.Add(new KeyValuePair<string,Color>(f.Name,(Color)f.GetValue(null)));
        return list;
    }
    static int[] AllowedFontPx(){
        var list=new List<int>();
        foreach(var f in typeof(HudTheme).GetFields(BindingFlags.Static|BindingFlags.Public))
            if(f.FieldType==typeof(int) && f.Name.StartsWith("Font")) list.Add((int)f.GetValue(null));
        return list.Distinct().OrderBy(x=>x).ToArray();
    }
    static bool RgbNear(Color a, Color b) =>
        Mathf.Abs(a.r-b.r)<=EPS && Mathf.Abs(a.g-b.g)<=EPS && Mathf.Abs(a.b-b.b)<=EPS;
    static bool ColorNear(Color a, Color b) => RgbNear(a,b) && Mathf.Abs(a.a-b.a)<=EPS;

    /// <summary>
    /// A colour is token-compliant if it equals a theme token, OR it is a theme token's RGB deliberately hidden at
    /// alpha 0 (that is how an OFF accent bar is expressed — same token, no second colour introduced).
    /// </summary>
    static string MatchToken(Color c){
        foreach(var kv in ThemeColors()) if(ColorNear(c,kv.Value)) return kv.Key;
        if(c.a<=EPS) foreach(var kv in ThemeColors()) if(RgbNear(c,kv.Value)) return kv.Key+"(alpha0)";
        return null;
    }

    /// <summary>Nearest ancestor Image that actually contributes contrast (alpha &gt; 0 — a fully transparent Image is a raycast target, not a plate).</summary>
    static Image PlateOf(Transform t){
        for(var p=t.parent; p!=null; p=p.parent){
            var img=p.GetComponent<Image>();
            if(img!=null && img.color.a>0.01f) return img;
        }
        return null;
    }

    /// <summary>
    /// Is the text GEOMETRICALLY on its plate? The alpha rule alone is a hierarchy check, and hierarchy lies:
    /// a wrapped 2-line label inside a fixed-height card renders its 2nd line OUTSIDE the card, on bare PanelTint,
    /// while "nearest ancestor Image alpha = .92" still reports fine. (Found 2026-07-30 by LOOKING at the U8 capture —
    /// which is exactly why the capture exists even though it is not a verdict.)
    /// Two ways it can escape: the text's own rect sticking out of the plate, or the text overflowing its own rect.
    /// </summary>
    static string PlateFit(Text t, Image plate, float tol = 1f){
        if(plate==null) return "no plate";
        var inner = t.rectTransform; var outer = plate.rectTransform;
        var corners = new Vector3[4]; inner.GetWorldCorners(corners);
        var r = outer.rect;
        for(int i=0;i<4;i++){
            var p = outer.InverseTransformPoint(corners[i]);
            if(p.x < r.xMin-tol || p.x > r.xMax+tol || p.y < r.yMin-tol || p.y > r.yMax+tol)
                return "rect escapes plate";
        }
        // the text can also overflow its OWN rect (verticalOverflow = Overflow, which we need for CJK wrapping)
        float need = LayoutUtility.GetPreferredHeight(inner);
        if(need > inner.rect.height + tol)
            return "text overflows own rect (needs "+need.ToString("F0")+"px, has "+inner.rect.height.ToString("F0")+"px)";
        return null;
    }

    // ---- 1) Setup ----
    public static void Setup(){
        var starter=FindStarter();
        if(starter==null){ Debug.LogError("[PS_VerifyUI] QuickTestStarter not in scene — is QuickStart open?"); return; }
        var so=new SerializedObject(starter);
        string orig=string.Join("\n", new[]{
            "startAsServer="+so.FindProperty("startAsServer").boolValue,
            "hostMode="+so.FindProperty("hostMode").boolValue,
            "roomSceneKey="+so.FindProperty("roomSceneKey").stringValue });
        Directory.CreateDirectory(TmpDir); File.WriteAllText(OrigF, orig);
        so.FindProperty("startAsServer").boolValue=true;
        so.FindProperty("hostMode").boolValue=true;
        so.FindProperty("roomSceneKey").stringValue=ROOM;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[PS_VerifyUI] Setup: host + roomSceneKey="+ROOM);
    }

    // ---- 3) Check ----
    public static void Check(){
        var sb=new StringBuilder();
        var warn=new List<string>();

        // find the HUD binder instance (reusable type, no namespace)
        var hudType=FindType("CrossPlatformRoomHud");
        var hudComp = hudType!=null ? UnityEngine.Object.FindObjectsByType(hudType, FindObjectsSortMode.None).FirstOrDefault() as Component : null;
        bool hudExists = hudComp!=null;
        sb.AppendLine("U1 HUD 'CrossPlatformRoomHud' present="+hudExists);
        bool u1=false, u2=false, u3=false, u4=false, u6=false, u7=false;
        object reg = null;

        if(hudExists){
            var hud=hudComp.gameObject;
            var canvas=hud.GetComponent<Canvas>();
            var gr=hud.GetComponent<GraphicRaycaster>();
            var tdgrType=FindType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
            bool hasTDGR = tdgrType!=null && hud.GetComponent(tdgrType)!=null;
            var expectMode = EXPECT_SCREENSPACE ? RenderMode.ScreenSpaceOverlay : RenderMode.WorldSpace;
            bool modeOk = canvas!=null && canvas.renderMode==expectMode;
            sb.AppendLine("   canvas.renderMode="+(canvas!=null?canvas.renderMode.ToString():"<none>")+" (expect "+expectMode+")");
            sb.AppendLine("   GraphicRaycaster="+(gr!=null)+"  TrackedDeviceGraphicRaycaster="+hasTDGR+" (expect "+EXPECT_XR+")");
            u1 = modeOk && gr!=null && (hasTDGR==EXPECT_XR);

            // U2 — binder self-wired
            bool wired = Field(hudComp,"_wired") is bool b && b;
            sb.AppendLine("U2 binder self-wired (_wired)="+wired);
            u2 = wired;

            // U3 — rows == registry toggleables (registry-driven, not hardcoded)
            var rcType=FindType("PromptScene.Core.RoomCore");
            object inst = rcType!=null? rcType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(null):null;
            reg = Prop(inst,"Contents");
            var toggleable = Prop(reg,"Toggleable") as IEnumerable;
            var toggles = new List<object>(); if(toggleable!=null) foreach(var t in toggleable) toggles.Add(t);
            int toggleCount=toggles.Count;
            var buttonsTr = hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Buttons");
            var rows = buttonsTr!=null ? Enumerable.Range(0,buttonsTr.childCount).Select(i=>buttonsTr.GetChild(i))
                         .Where(c=>c.gameObject.activeSelf && c.name.StartsWith("Btn_")).ToList() : new List<Transform>();
            int rowCount=rows.Count;
            sb.AppendLine("U3 registry Toggleable count="+toggleCount+"  generated rows="+rowCount+" (rows >= toggleables, +1 if a Ruler clear row)");
            u3 = toggleCount>=0 && rowCount>=toggleCount && rowCount>0;
            sb.AppendLine("   rows: ["+string.Join(",", rows.Select(r=>r.name))+"]");

            // U4 — inject a button click → content SetEnabled path
            object ruler = null;
            if(reg!=null){ var m=reg.GetType().GetMethod("GetById"); ruler = m?.Invoke(reg, new object[]{CLEARABLE_ID}); }
            var rulerRow = rows.FirstOrDefault(r=>r.name=="Btn_"+CLEARABLE_ID);
            if(ruler!=null && rulerRow!=null){
                bool before = Prop(ruler,"IsEnabled") is bool eb && eb;
                var btn = rulerRow.GetComponent<Button>();
                if(btn!=null){
                    btn.onClick.Invoke();
                    bool afterOn = Prop(ruler,"IsEnabled") is bool ea && ea;
                    btn.onClick.Invoke();
                    bool back = Prop(ruler,"IsEnabled") is bool eb2 && eb2;
                    sb.AppendLine("U4 inject Btn_"+CLEARABLE_ID+".onClick: IsEnabled "+before+" -> "+afterOn+" -> "+back+" (expect flips then restores)");
                    u4 = (afterOn != before) && (back == before);
                } else sb.AppendLine("U4 no Button on Btn_"+CLEARABLE_ID);
            } else {
                // fall back to ANY toggleable row so the onClick→SetEnabled path is still proven in a Ruler-less room
                var anyRow = rows.FirstOrDefault(r=>r.name!="Btn_clear");
                object any = null;
                if(anyRow!=null && reg!=null){
                    var m=reg.GetType().GetMethod("GetById");
                    any = m?.Invoke(reg, new object[]{ anyRow.name.Substring("Btn_".Length) });
                }
                var anyBtn = anyRow!=null ? anyRow.GetComponent<Button>() : null;
                if(any!=null && anyBtn!=null){
                    bool before = Prop(any,"IsEnabled") is bool xb && xb;
                    anyBtn.onClick.Invoke();
                    bool afterOn = Prop(any,"IsEnabled") is bool xa && xa;
                    anyBtn.onClick.Invoke();
                    bool back = Prop(any,"IsEnabled") is bool xb2 && xb2;
                    sb.AppendLine("U4 no '"+CLEARABLE_ID+"' in this room → injected "+anyRow.name+".onClick instead: IsEnabled "+before+" -> "+afterOn+" -> "+back);
                    u4 = (afterOn != before) && (back == before);
                } else {
                    sb.AppendLine("U4 skipped — no toggleable row to inject (registry-driven, so absence is valid)");
                    u4 = true;
                }
            }

            // ══════════ U6 — TYPE DISCIPLINE ══════════════════════════════════════════════════
            int[] allowedPx = AllowedFontPx();
            var texts = hud.GetComponentsInChildren<Text>(true).ToList();
            var badPx = texts.Where(t=>!allowedPx.Contains(t.fontSize)).ToList();
            var badArc = texts.Where(t=>HudTheme.Legibility.CapArcmin(t.fontSize) < HudTheme.Legibility.MinCapArcmin).ToList();
            var fauxBold = texts.Where(t=>t.fontStyle==FontStyle.Bold || t.fontStyle==FontStyle.BoldAndItalic).ToList();
            var panelTr = hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Panel");
            var panelSizes = panelTr!=null
                ? panelTr.GetComponentsInChildren<Text>(true).Select(t=>t.fontSize).Distinct().OrderBy(x=>x).ToList()
                : new List<int>();

            // TMP is present in the project; if anything ever puts a TMP label in the HUD, hold it to the same rules.
            var tmpType = FindType("TMPro.TMP_Text");
            int tmpCount=0, tmpBad=0;
            if(tmpType!=null){
                foreach(var c in hud.GetComponentsInChildren(tmpType, true)){
                    tmpCount++;
                    var fs = Prop(c,"fontSize");
                    if(fs is float fv && !allowedPx.Contains(Mathf.RoundToInt(fv))) tmpBad++;
                    var st = Prop(c,"fontStyle");
                    if(st!=null && (Convert.ToInt32(st) & 1) != 0) tmpBad++;   // FontStyles.Bold == 1
                }
            }

            bool fontFallback = Prop(hudComp,"FontFallback") is bool ff && ff;
            int realisedEmph = Prop(hudComp,"RealisedEmphWeight") is int rw ? rw : HudTheme.WeightBody;
            bool weightsOk = HudTheme.AllowedWeights.Contains(realisedEmph);

            sb.AppendLine("U6 TYPE DISCIPLINE");
            sb.AppendLine("   allowed sizes (from HudTheme Font*) = ["+string.Join(",",allowedPx)+"]; texts="+texts.Count
                          +"  offScaleSizes="+badPx.Count+(badPx.Count>0?" ["+string.Join(",",badPx.Select(t=>t.name+":"+t.fontSize))+"]":""));
            sb.AppendLine("   CapArcmin >= "+HudTheme.Legibility.MinCapArcmin+"' at "+HudTheme.Legibility.PxPerMeter+" px/m & "
                          +HudTheme.Legibility.DistanceM+"m: violations="+badArc.Count
                          +"  (18px="+HudTheme.Legibility.CapArcmin(HudTheme.FontSm).ToString("F0")+"' 24px="+HudTheme.Legibility.CapArcmin(HudTheme.FontMd).ToString("F0")+"')");
            sb.AppendLine("   distinct sizes under Panel="+panelSizes.Count+" ["+string.Join(",",panelSizes)+"] (max 2)");
            sb.AppendLine("   faux-bold (FontStyle.Bold) count="+fauxBold.Count+" (expect 0)");
            sb.AppendLine("   TMP labels in HUD="+tmpCount+" violations="+tmpBad+" (expect 0)");
            sb.AppendLine("   weights: realised emphasis="+realisedEmph+" allowed=["+string.Join(",",HudTheme.AllowedWeights)+"] ok="+weightsOk);
            u6 = badPx.Count==0 && badArc.Count==0 && panelSizes.Count<=2 && fauxBold.Count==0 && tmpBad==0 && weightsOk;
            if(fontFallback)
                warn.Add("U6 font fallback: no PyeojinGothic Font asset → dynamic OS font, ONE weight only. HudTheme.WeightEmph("
                         +HudTheme.WeightEmph+") renders as "+HudTheme.WeightBody+" and emphasis is colour-only. "
                         +"Bundling the 400/600 pair is a baked-base item OUTSIDE this skill (separate queue). WARN, not FAIL.");

            // ══════════ U7 — COLOUR / SPACING / CONTRAST DISCIPLINE ═══════════════════════════
            // spacing + padding must sit on the space scale (0 = "none", always allowed)
            var groups = hud.GetComponentsInChildren<LayoutGroup>(true).ToList();
            var badSpace = new List<string>();
            foreach(var g in groups){
                var pad=g.padding;
                foreach(var kv in new[]{ ("padL",pad.left),("padR",pad.right),("padT",pad.top),("padB",pad.bottom) })
                    if(kv.Item2!=0 && !HudTheme.SpaceScale.Contains(kv.Item2)) badSpace.Add(g.name+"."+kv.Item1+"="+kv.Item2);
                if(g is HorizontalOrVerticalLayoutGroup hv){
                    int sp=Mathf.RoundToInt(hv.spacing);
                    if(sp!=0 && !HudTheme.SpaceScale.Contains(sp)) badSpace.Add(g.name+".spacing="+sp);
                }
            }

            // ZERO literal colours: every Graphic under the HUD must resolve to a theme token
            var graphics = hud.GetComponentsInChildren<Graphic>(true).ToList();
            var literals = new List<string>();
            var accentBearers = new List<Graphic>();
            foreach(var gfx in graphics){
                string tok = MatchToken(gfx.color);
                if(tok==null) literals.Add(gfx.name+"="+ColorToHex(gfx.color));
                if(RgbNear(gfx.color, HudTheme.Accent) && gfx.color.a>0.01f) accentBearers.Add(gfx);
            }

            // ACCENT = ONE MEANING: visible accent only on a `…__bar` / `…__state` of an ENABLED feature
            var accentViolations = new List<string>();
            foreach(var gfx in accentBearers){
                string n = gfx.name;
                bool named = n.EndsWith("__bar") || n.EndsWith("__state");
                if(!named){ accentViolations.Add(n+" (not a __bar/__state state object)"); continue; }
                string rowName = gfx.transform.parent!=null
                    ? FindRowName(gfx.transform) : null;
                string id = rowName!=null && rowName.StartsWith("Btn_") ? rowName.Substring("Btn_".Length) : null;
                object feat = null;
                if(id!=null && reg!=null){ var m=reg.GetType().GetMethod("GetById"); feat = m?.Invoke(reg,new object[]{id}); }
                bool on = Prop(feat,"IsEnabled") is bool fe && fe;
                if(!on) accentViolations.Add(n+" visible but feature '"+(id??"?")+"' IsEnabled=false");
            }

            // TEXT PLATE: no text is allowed to sit on the translucent tint — by ALPHA *and* by GEOMETRY.
            var thinPlates = new List<string>();
            var offPlates  = new List<string>();
            foreach(var t in texts){
                var plate = PlateOf(t.transform);
                float a = plate!=null ? plate.color.a : 0f;
                if(a < HudTheme.Legibility.MinTextPlateAlpha)
                    thinPlates.Add(t.name+" on "+(plate!=null?plate.name:"<none>")+" alpha="+a.ToString("F2"));
                if(!t.gameObject.activeInHierarchy) continue;      // an inactive card cannot be mis-rendered
                var fit = PlateFit(t, plate);
                if(fit!=null) offPlates.Add(t.name+": "+fit);
            }

            // TAP TARGETS: angular size of each row's height
            var smallTargets = new List<string>();
            foreach(var r in rows){
                var rt = r as RectTransform;
                float h = rt!=null ? rt.rect.height : 0f;
                var le = r.GetComponent<LayoutElement>();
                if(h<=0f && le!=null) h = le.preferredHeight;
                float deg = h / HudTheme.Legibility.PxPerMeter / HudTheme.Legibility.DistanceM * Mathf.Rad2Deg;
                if(deg < HudTheme.Legibility.MinTargetDeg) smallTargets.Add(r.name+" h="+h.ToString("F0")+"px="+deg.ToString("F2")+"deg");
            }

            sb.AppendLine("U7 COLOUR / SPACING / CONTRAST DISCIPLINE");
            sb.AppendLine("   LayoutGroups="+groups.Count+" offScale spacing/padding="+badSpace.Count+(badSpace.Count>0?" ["+string.Join(",",badSpace)+"]":"")
                          +"  scale=["+string.Join(",",HudTheme.SpaceScale)+"]");
            sb.AppendLine("   graphics="+graphics.Count+"  LITERAL colours="+literals.Count+(literals.Count>0?" ["+string.Join(",",literals)+"]":" (all resolve to HudTheme tokens)"));
            sb.AppendLine("   visible accent bearers="+accentBearers.Count+" ["+string.Join(",",accentBearers.Select(g=>g.name))+"]  violations="+accentViolations.Count
                          +(accentViolations.Count>0?" ["+string.Join(",",accentViolations)+"]":""));

            // ── accent POSITIVE CASE (self-exercising) ────────────────────────────────────────────────
            // Every feature resting OFF means zero visible accent, and the rule above then passes TRIVIALLY.
            // A floor gate that is only green while the feature is unused is worthless — so drive one row ON
            // through its real wired Button, assert the accent landed on exactly that row's `__bar` and nowhere
            // else, then restore. (This is how the FindRowName suffix bug was caught on 2026-07-30.)
            string accentPositive;
            var probeRow = rows.FirstOrDefault(r=>r.name!="Btn_clear");
            var probeBtn = probeRow!=null ? probeRow.GetComponent<Button>() : null;
            if(probeBtn!=null){
                probeBtn.onClick.Invoke();
                var lit = hud.GetComponentsInChildren<Graphic>(true)
                             .Where(g=>RgbNear(g.color,HudTheme.Accent) && g.color.a>0.01f).ToList();
                string wantBar = probeRow.name + "__bar";
                bool exactlyOne = lit.Count==1 && lit[0].name==wantBar;
                string id = probeRow.name.Substring("Btn_".Length);
                object feat = null;
                if(reg!=null){ var m=reg.GetType().GetMethod("GetById"); feat = m?.Invoke(reg,new object[]{id}); }
                bool on = Prop(feat,"IsEnabled") is bool pe && pe;
                accentPositive = "drove "+probeRow.name+" ON → visible=["+string.Join(",",lit.Select(g=>g.name))+"] expect ["+wantBar+"]"
                                 +" featureIsEnabled="+on+" → "+((exactlyOne && on)?"PROVEN":"VIOLATION");
                if(!(exactlyOne && on)) accentViolations.Add("positive case: "+accentPositive);
                probeBtn.onClick.Invoke();      // restore the resting state
            } else accentPositive = "no toggleable row to drive (registry-driven room) — skipped";
            sb.AppendLine("   accent POSITIVE case: "+accentPositive);
            sb.AppendLine("   text plate alpha >= "+HudTheme.Legibility.MinTextPlateAlpha+": violations="+thinPlates.Count
                          +(thinPlates.Count>0?" ["+string.Join(",",thinPlates)+"]":""));
            sb.AppendLine("   text plate GEOMETRY (text stays on its card): violations="+offPlates.Count
                          +(offPlates.Count>0?" ["+string.Join(",",offPlates)+"]":""));
            sb.AppendLine("   tap targets >= "+HudTheme.Legibility.MinTargetDeg+"deg: violations="+smallTargets.Count
                          +(smallTargets.Count>0?" ["+string.Join(",",smallTargets)+"]":"")
                          +"  (row h="+HudTheme.Space6+"px="+(HudTheme.Space6/HudTheme.Legibility.PxPerMeter/HudTheme.Legibility.DistanceM*Mathf.Rad2Deg).ToString("F2")+"deg)");
            u7 = badSpace.Count==0 && literals.Count==0 && accentViolations.Count==0 && thinPlates.Count==0 && offPlates.Count==0 && smallTargets.Count==0;

            // ══════════ U8 — CAPTURE (evidence, NOT a verdict) ════════════════════════════════
            string cap = Capture(hud);
            sb.AppendLine("U8 CAPTURE (evidence only — deliberately NOT in PASS/FAIL; 미감은 사람/비전 몫) → "+cap);
        }

        // U5 — existing UI intact + SYSTEMS unbroken (avatar spawned)
        var canvasNames=new List<string>();
        for(int i=0;i<SceneManager.sceneCount;i++){ var s=SceneManager.GetSceneAt(i); if(!s.isLoaded) continue;
            foreach(var r in s.GetRootGameObjects()) foreach(var c in r.GetComponentsInChildren<Canvas>(true)) canvasNames.Add(c.gameObject.name); }
        bool avatar=false;
        for(int i=0;i<SceneManager.sceneCount;i++){ var s=SceneManager.GetSceneAt(i); if(!s.isLoaded) continue;
            foreach(var r in s.GetRootGameObjects()) foreach(var tr in r.GetComponentsInChildren<Transform>(true)) if(tr.gameObject.name=="Desktop(Clone)") avatar=true; }
        sb.AppendLine("U5 existing UI intact — canvases=["+string.Join(",", canvasNames.Distinct())+"]  avatar Desktop(Clone)="+avatar+" (SYSTEMS unbroken)");

        foreach(var w in warn) sb.AppendLine("WARN "+w);

        bool pass = u1 && u2 && u3 && u4 && avatar && u6 && u7;
        sb.AppendLine("--- U1="+u1+" U2="+u2+" U3="+u3+" U4="+u4+" U5(avatar)="+avatar+" U6="+u6+" U7="+u7+" | WARN="+warn.Count+" | U8=evidence");
        sb.AppendLine("=== §5/§6 CROSS-PLATFORM-UI VERDICT: "+(pass?"PASS":"FAIL")+" ===");
        Directory.CreateDirectory(TmpDir); File.WriteAllText(OutF, sb.ToString());
        Debug.Log("[PS_VerifyUI]\n"+sb);
    }

    /// <summary>
    /// Walk up to the `Btn_*` row that owns this graphic (rows are the accent's only legal home).
    /// ⚠ The state object is itself named `<row>__bar`, which ALSO starts with "Btn_" — so a naive walk returns
    /// "Btn_chat__bar" and the id becomes "chat__bar", GetById misses, and the gate reports a FALSE violation the
    /// moment any feature is ON. (Caught 2026-07-30 only by exercising the ON state; the OFF state passed trivially
    /// with zero visible accent. A floor gate that is green only while the feature is unused is worthless.)
    /// Strip the state suffix before returning.
    /// </summary>
    static string FindRowName(Transform t){
        for(var p=t; p!=null; p=p.parent){
            if(!p.name.StartsWith("Btn_")) continue;
            var n = p.name;
            foreach(var suf in new[]{ "__bar", "__state" })
                if(n.EndsWith(suf)) n = n.Substring(0, n.Length - suf.Length);
            return n;
        }
        return null;
    }

    static string ColorToHex(Color c) =>
        "#"+ColorUtility.ToHtmlStringRGBA(c);

    /// <summary>
    /// U8 — render the HUD head-on from the design distance into a PNG. This is EVIDENCE for a human (or a vision
    /// model) to compare against hud-glass-v0.html; it is not a judgement. Aesthetics stay unverified by design.
    /// The panel billboards toward the active camera, so we sit on the panel's own viewing axis (-forward).
    /// </summary>
    static string Capture(GameObject hud){
        try{
            var panel = hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Panel") ?? hud.transform;
            var rt = panel as RectTransform;
            float wPx = rt!=null ? rt.rect.width  : 360f;
            float hPx = rt!=null ? rt.rect.height : 300f;
            int texW = Mathf.Clamp(Mathf.RoundToInt(wPx*2f), 64, 4096);   // 2x for legible text in the PNG
            int texH = Mathf.Clamp(Mathf.RoundToInt(hPx*2f), 64, 4096);

            float d = HudTheme.Legibility.DistanceM;
            // Isolate the HUD on a spare layer. Without this the capture is useless for the mockup-diff loop: the
            // avatar and the room's other world canvases sit between the camera and the panel (observed 2026-07-30).
            // Layers are restored before returning, so the scene is left exactly as found.
            int spare = SpareLayer(hud);
            var stash = new List<KeyValuePair<GameObject,int>>();
            foreach(var tr in hud.GetComponentsInChildren<Transform>(true)){
                stash.Add(new KeyValuePair<GameObject,int>(tr.gameObject, tr.gameObject.layer));
                tr.gameObject.layer = spare;
            }

            var camGo = new GameObject("__ps_ui_capture_cam");            // untagged: must NOT become Camera.main
            var cam = camGo.AddComponent<Camera>();
            cam.cullingMask = 1 << spare;
            cam.transform.position = hud.transform.position - hud.transform.forward * d;
            cam.transform.rotation = Quaternion.LookRotation(hud.transform.forward, hud.transform.up);
            cam.orthographic = true;
            cam.orthographicSize = (hPx / HudTheme.Legibility.PxPerMeter) * 0.5f;   // frame the panel exactly
            cam.nearClipPlane = 0.01f; cam.farClipPlane = d*2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = HudTheme.PanelTint;                     // token, not a literal
            cam.enabled = false;

            var target = new RenderTexture(texW, texH, 24, RenderTextureFormat.ARGB32){ antiAliasing = 8 };
            cam.targetTexture = target;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = target;
            var png = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            png.ReadPixels(new Rect(0,0,texW,texH), 0, 0);
            png.Apply(false);
            RenderTexture.active = prev;

            Directory.CreateDirectory(TmpDir);
            File.WriteAllBytes(CapF, png.EncodeToPNG());

            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(png);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(camGo);
            foreach(var kv in stash) if(kv.Key!=null) kv.Key.layer = kv.Value;   // restore: scene left as found
            return CapF+" ("+texW+"x"+texH+", ortho "+d.ToString("F2")+"m head-on, HUD isolated on layer "+spare+")";
        } catch(Exception e){
            return "capture failed (not a FAIL — evidence only): "+e.Message;
        }
    }

    /// <summary>Highest layer index used by nothing outside the HUD — so the isolated capture frames only the panel.</summary>
    static int SpareLayer(GameObject hud){
        var hudSet = new HashSet<GameObject>(hud.GetComponentsInChildren<Transform>(true).Select(t=>t.gameObject));
        var used = new HashSet<int>();
        foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if(!hudSet.Contains(r.gameObject)) used.Add(r.gameObject.layer);
        foreach(var g in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            if(!hudSet.Contains(g.gameObject)) used.Add(g.gameObject.layer);
        for(int i=31;i>=8;i--) if(!used.Contains(i)) return i;
        return 31;
    }

    // ---- 5) Teardown ----
    public static void Teardown(){
        var starter=FindStarter();
        if(starter==null || !File.Exists(OrigF)){ Debug.Log("[PS_VerifyUI] Teardown: nothing to restore"); return; }
        var map=File.ReadAllLines(OrigF).Select(l=>l.Split(new[]{'='},2)).Where(a=>a.Length==2).ToDictionary(a=>a[0],a=>a[1]);
        var so=new SerializedObject(starter);
        if(map.ContainsKey("startAsServer")) so.FindProperty("startAsServer").boolValue=map["startAsServer"]=="True";
        if(map.ContainsKey("hostMode"))      so.FindProperty("hostMode").boolValue=map["hostMode"]=="True";
        if(map.ContainsKey("roomSceneKey"))  so.FindProperty("roomSceneKey").stringValue=map["roomSceneKey"];
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[PS_VerifyUI] Teardown: QuickTestStarter restored");
    }
}
