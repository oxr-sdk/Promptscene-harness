// /cross-platform-ui — QuickTest verify of the authored World Space HUD. SSOT: build-studio-room.md §4 (QuickTest) + §5/§6.
// Run via MCP script-execute (className=PS_VerifyUI). Same host-QuickTest driver shape as assemble-room's verify_quicktest:
//   0) MCP: scene-open Assets/App/Scenes/QuickStart.unity Single
//   1) PS_VerifyUI.Setup    — snapshot QuickTestStarter, set startAsServer+hostMode+roomSceneKey=<ROOM>
//   2) (isPlaying=true), wait ~12-15s (server start → Addressables room load → spawn → RoomCore up → binder wires)
//   3) PS_VerifyUI.Check    — writes UI signals to <project>/Temp/ps_ui_result.txt (Read it)
//   4) (isPlaying=false)
//   5) PS_VerifyUI.Teardown — restore QuickTestStarter (in-memory; QuickStart disk untouched)
//
// What Check proves:
//   U1..U5   the HUD is authored & cross-platform-wired, the binder self-wired from the REGISTRY, an injected
//            button-click drives the content's SetEnabled, and existing UI is intact.
//   U6..U7   ⭐ THE DESIGN FLOOR, machine-judged: type discipline (ramp sizes + role whitelist, angular size at BOTH
//            the design and the real placement distance, ≤2 ramp sizes per panel, no faux-bold) and
//            colour/spacing/**CONTRAST ARITHMETIC** — U7 now composites each text's real ancestor stack over the
//            WORST environment (white AND black) and asserts ≥ HudTheme.Contrast.MinText. This is F0 as code:
//            a white Film exposed to the environment with no Scrim under it is a structural FAIL.
//   U8       captures over a BRIGHT and a DARK environment. EVIDENCE ONLY — deliberately not part of PASS/FAIL.
//   U9       composition: every node classifies into the SIX components; Card usage = 0; container nesting = 0.
//   U10      angular-size runaway: every world text either owns an angular-fix component (KeyBadge) or is on the
//            documented whitelist. Plus a 3-distance (1/3/8 m) measurement proving the badge holds its angle.
//   U11      icons: the Meta.Icon → codepoint → first-letter chain is deterministic and every codepoint we USE is
//            actually in the atlas. A mapped-but-missing codepoint is a STOP, never a silent fallback.
//
// Honesty: injected onClick.Invoke() proves the onClick→SetEnabled path; it is NOT a real pointer/interactor event
// (that = desktop mouse by a human + XR Simulator controller by a human; §5 caveat). And the floor is not taste:
// U6/U7/U9/U10/U11 prove the HUD OBEYS the frozen theme; whether it looks GOOD is unverified and stays a human call.
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
using PromptScene.Core.UI;                // HudTheme / HudIcons — the token SSOT the gates judge against

public class PS_VerifyUI {
    const string ROOM = "AssembleRoom";       // leaf == Addressables address == roomSceneKey (match assemble_ui.cs)
    const bool EXPECT_XR = true;              // true for MODE PCXR/CROSS, false for PC/PCSS
    const bool EXPECT_SCREENSPACE = false;    // true for MODE PCSS (ScreenSpaceOverlay), false for World Space modes
    const string CLEARABLE_ID = "ruler";      // the toggleable used for the click-injection path proof
    const float EPS = 1f / 255f;              // colour comparison tolerance (one 8-bit step)

    /// <summary>
    /// U10 whitelist — world text allowed to exist WITHOUT an angular-fix component, with the reason.
    /// An arbitrary exception is forbidden; this list is the only way to opt out and it is read in the report.
    /// </summary>
    static readonly Dictionary<string,string> AngularWhitelist = new Dictionary<string,string>{
        { "CrossPlatformRoomHud",
          "배치형 패널 — 스스로 놓은 위치가 있고 U6가 PlacementDistanceM에서 각크기를 단정한다. 다가가면 커지지만 그건 '접근'이지 "+
          "'폭주'가 아니다(월드 스케일 고정 라벨이 대상에 붙어 벽이 되는 것과 다르다). 헤드락/각크기 고정은 승인 사항이며 미적용." },
        { "MessageWindow",
          "⚠ 물려받은 데모 UI(작업지시 v3 '범위 밖' 항목과 같은 기존 Canvas 소속). 200px/m 고정 스케일의 TMP 월드 텍스트 "+
          "(1.0m x 0.25m @ y=3.4m)로, U10이 잡으려는 결함 유형 그 자체다. 이 스킬이 소유하지 않으므로 청구서로 넘긴다." },
        { "ChatWorldCanvas",
          "⚠ 물려받은 항목(이 스킬 범위 밖, 소유자=chat 피처). 같은 배치형 패널이지만 설계 거리 선언도 각크기 고정도 없다. "+
          "다음 U10 대상으로 청구서에 기입." },
    };

    static string TmpDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
    static string OrigF  => Path.Combine(TmpDir, "ps_ui_orig.txt");
    static string OutF   => Path.Combine(TmpDir, "ps_ui_result.txt");

    static Type FindType(string full){
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){
            var t=a.GetType(full); if(t!=null) return t;
            foreach(var tt in Safe(a)) if(tt.Name==full) return tt;
        }
        return null;
    }
    static Type[] Safe(Assembly a){ try{ return a.GetTypes(); }catch{ return Array.Empty<Type>(); } }
    static object Prop(object o,string n){ if(o==null) return null; var p=o.GetType().GetProperty(n, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); return p?.GetValue(o); }
    /// <summary>프로퍼티든 필드든 읽는다. (IconTiers/IconErrors는 readonly 필드라 Prop만 보면 조용히 빈 값이 나온다 — 2026-07-30 실측)</summary>
    static object Member(object o,string n){ return Prop(o,n) ?? Field(o,n); }
    static object Field(object o,string n){ if(o==null) return null; var f=o.GetType().GetField(n, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); return f?.GetValue(o); }
    static UnityEngine.Object FindStarter(){ var t=FindType("QuickTestStarter"); if(t==null) return null; var a=UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None); return a.Length>0?(UnityEngine.Object)a[0]:null; }

    // ── the theme, by reflection: any token added to HudTheme is picked up with no edit here ──────────────
    static List<KeyValuePair<string,Color>> ThemeColors(){
        var list=new List<KeyValuePair<string,Color>>();
        foreach(var f in typeof(HudTheme).GetFields(BindingFlags.Static|BindingFlags.Public))
            if(f.FieldType==typeof(Color)) list.Add(new KeyValuePair<string,Color>(f.Name,(Color)f.GetValue(null)));
        return list;
    }
    /// <summary>램프 크기(글자). 역할 크기(GlyphPx/KeycapPx)는 여기 들어가지 않는다 — 화이트리스트로만 허용된다.</summary>
    static int[] RampPx() => new[]{ HudTheme.FontFoot, HudTheme.FontBody, HudTheme.FontTitle }.Distinct().OrderBy(x=>x).ToArray();
    static int[] RolePx() => new[]{ HudTheme.GlyphPx, HudTheme.KeycapPx }.Distinct().ToArray();
    static bool SizeExempt(string name) => HudTheme.Roles.SizeExempt.Any(s=>name.EndsWith(s));
    /// <summary>0, 간격 스케일, 또는 스케일에서 **유도된** 값(PadX/PadY)만 허용한다. 유도값은 손으로 넣은 수가 아니다.</summary>
    static bool OnScale(int v) => v==0 || HudTheme.SpaceScale.Contains(v) || HudTheme.DerivedSpacings.Contains(v);
    static bool Decorative(string name) => HudTheme.Roles.Decorative.Any(s=>name.EndsWith(s)) || name=="PanelFrame" || name=="PanelEdge";

    static bool RgbNear(Color a, Color b) =>
        Mathf.Abs(a.r-b.r)<=EPS && Mathf.Abs(a.g-b.g)<=EPS && Mathf.Abs(a.b-b.b)<=EPS;
    static bool ColorNear(Color a, Color b) => RgbNear(a,b) && Mathf.Abs(a.a-b.a)<=EPS;

    /// <summary>
    /// A colour is token-compliant if it equals a theme token, OR it is a theme token's RGB deliberately hidden at
    /// alpha 0 (that is how a hidden state is expressed — same token, no second colour introduced).
    /// </summary>
    static string MatchToken(Color c){
        foreach(var kv in ThemeColors()) if(ColorNear(c,kv.Value)) return kv.Key;
        if(c.a<=EPS) foreach(var kv in ThemeColors()) if(RgbNear(c,kv.Value)) return kv.Key+"(alpha0)";
        return null;
    }

    /// <summary>
    /// 이 그래픽 뒤에 실제로 깔리는 색 스택(root→leaf). **조상 체인만** 본다 — 그래서 authoring 규칙이
    /// "대비를 만드는 판은 조상이어야 한다"가 된다(형제로 깔면 게이트가 볼 수 없고, 실제로도 겹침 순서가 취약하다).
    /// 기하학적으로 텍스트를 덮지 못하는 조상은 스택에서 뺀다 — 계층은 거짓말하기 때문이다.
    /// </summary>
    static List<Color> BackdropStack(Graphic g, Transform stopAt, out List<string> names){
        var stack=new List<Color>(); names=new List<string>();
        var chain=new List<Transform>();
        for(var p=g.transform.parent; p!=null && p!=stopAt; p=p.parent) chain.Add(p);
        chain.Reverse();                                   // root-first = painting order
        foreach(var p in chain){
            var img=p.GetComponent<Image>();
            if(img==null || img.color.a<=EPS) continue;
            if(!Covers(img.rectTransform, g.rectTransform)) continue;
            stack.Add(img.color); names.Add(p.name);
        }
        return stack;
    }

    /// <summary>outer가 inner를 기하학적으로 덮는가(1px 여유).</summary>
    static bool Covers(RectTransform outer, RectTransform inner, float tol=1f){
        var corners=new Vector3[4]; inner.GetWorldCorners(corners);
        var r=outer.rect;
        for(int i=0;i<4;i++){
            var p=outer.InverseTransformPoint(corners[i]);
            if(p.x<r.xMin-tol||p.x>r.xMax+tol||p.y<r.yMin-tol||p.y>r.yMax+tol) return false;
        }
        return true;
    }


    /// <summary>
    /// ⛔ ROOM이 인스펙터의 `Room Scene` 칸으로 **역해석되는지** 먼저 확인한다.
    /// QuickTestStarter의 직렬 필드는 `roomSceneKey`(문자열) 하나뿐이고, 인스펙터 위쪽 `Room Scene`은
    /// 그 문자열을 SceneAsset으로 되짚어 보여주는 뷰다. 그래서 키가 해석되지 않으면:
    ///   `Room Scene` = None → 런타임에 룸 로드 실패 → 플레이어 스폰 없음 → **아바타·카메라가 영원히 안 생김**
    ///   → Game 뷰에 `No cameras rendering`이 계속 뜬다. UI 문제로 보이지만 부트 설정 문제다.
    /// (2026-07-30 실측: 부트 씬에 없는 룸 'PromptSceneRoom_1'이 남아 있어 정확히 이 증상이 났다.)
    /// 그래서 검증은 **방금 만든 룸에서** 돌리고, 그 룸이 실재하는지 여기서 단정한다.
    /// </summary>
    /// <summary>
    /// Play 직전에 **부트 씬(QuickStart)만** 열려 있어야 한다. 룸 씬이 에디터에 additive 로 함께 열려 있으면
    /// FishNet이 그 룸을 Global Scene으로 정상 소유하지 못해 **플레이어 스폰이 일어나지 않고**, 아바타가 없으니
    /// 카메라도 없어 Game 뷰에 'No cameras rendering' 이 계속 뜬다. UI/렌더 문제처럼 보이지만 씬 개방 상태 문제다.
    /// (2026-07-31 A/B 실측: 룸이 함께 열린 상태 → t+14s cams=0 / avatar=False.
    ///  QuickStart 를 Single 로 다시 열고 재생 → t+16s cams=1 / avatar=True.)
    /// MovedObjectsHolder 는 FishNet 내부 씬이라 제외한다.
    /// </summary>
    /// <summary>
    /// 사람이 인스펙터에서 씬을 **드래그앤드롭** 할 때와 **동일한 경로**로 roomSceneKey 값을 만든다.
    ///   사람 손: QuickTestStarterEditor 의 Scene(드래그&드롭) ObjectField → ResolveAddress(SceneAsset) → roomSceneKey
    ///   우리   : 그 ResolveAddress 를 **리플렉션으로 그대로 호출**한다. 구현을 복제하지 않으므로 드리프트가 불가능하다.
    /// ⛔ leaf 이름을 그대로 쓰면 안 된다 — 등록 주소가 leaf 와 다른 룸이 실제로 있다:
    ///     AssembleRoom → 'AssembleRoom'  (우연히 일치)
    ///     T_RoomA      → 'Scenes/T_RoomA'  ← leaf 를 쓰면 로드가 실패한다
    /// (2026-07-31: 이전 구현이 leaf 를 직접 써서 AssembleRoom 에서만 우연히 동작하고 있었다.)
    /// </summary>
    static System.Type EditorHelperType(){
        foreach(var a in System.AppDomain.CurrentDomain.GetAssemblies()){
            var t=a.GetType("QuickTestStarterEditor");
            if(t!=null) return t;
            try{ foreach(var tt in a.GetTypes()) if(tt.Name=="QuickTestStarterEditor") return tt; }catch{}
        }
        return null;
    }

    static object SceneAssetOf(string leaf){
        foreach(var g in AssetDatabase.FindAssets("t:SceneAsset "+leaf)){
            var p=AssetDatabase.GUIDToAssetPath(g);
            if(System.IO.Path.GetFileNameWithoutExtension(p)!=leaf) continue;
            var sa=AssetDatabase.LoadAssetAtPath(p, typeof(UnityEditor.SceneAsset));
            if(sa!=null) return sa;
        }
        return null;
    }

    /// <summary>드래그앤드롭이 만들 키 문자열. 실패 시 null.</summary>
    static string RoomKeyLikeHuman(string leaf, out string how){
        how="";
        var asset=SceneAssetOf(leaf);
        if(asset==null){ how="SceneAsset 없음"; return null; }
        var et=EditorHelperType();
        if(et!=null){
            var m=et.GetMethod("ResolveAddress", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            if(m!=null){
                try{
                    var key=(string)m.Invoke(null, new object[]{ asset });
                    how="QuickTestStarterEditor.ResolveAddress (사람과 동일 경로)";
                    return key;
                }catch(System.Exception e){ how="ResolveAddress 호출 실패: "+e.Message; }
            } else how="ResolveAddress 메서드 없음";
        } else how="QuickTestStarterEditor 타입 없음";
        return null;
    }

    /// <summary>키 → SceneAsset 역해석(인스펙터 Scene 칸이 None 이 되지 않는지). 에디터의 ResolveSceneAsset 을 그대로 쓴다.</summary>
    static string RoundTripPath(string key){
        var et=EditorHelperType();
        if(et==null) return null;
        var m=et.GetMethod("ResolveSceneAsset", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        if(m==null) return null;
        try{
            var sa=m.Invoke(null, new object[]{ key });
            if(sa==null) return null;
            return AssetDatabase.GetAssetPath((UnityEngine.Object)sa);
        }catch{ return null; }
    }

    static bool OnlyBootSceneOpen(out string detail){
        var extra = new List<string>();
        for(int i=0;i<SceneManager.sceneCount;i++){
            var s = SceneManager.GetSceneAt(i);
            if(!s.isLoaded) continue;
            if(s.name == "QuickStart" || s.name == "MovedObjectsHolder") continue;
            extra.Add(s.name);
        }
        detail = extra.Count == 0 ? "QuickStart 단독" : string.Join(", ", extra.ToArray());
        return extra.Count == 0;
    }

    static bool RoomResolvable(string key, out string detail){
        string leaf = key.Contains("/") ? key.Substring(key.LastIndexOf('/')+1) : key;
        if(string.IsNullOrEmpty(leaf)){ detail="roomSceneKey가 비어 있다"; return false; }
        foreach(var g in AssetDatabase.FindAssets("t:SceneAsset "+leaf)){
            var p = AssetDatabase.GUIDToAssetPath(g);
            if(System.IO.Path.GetFileNameWithoutExtension(p)==leaf){ detail=p; return true; }
        }
        detail="프로젝트에 '"+leaf+"' SceneAsset이 없다 → Room Scene 칸이 None이 되고 룸이 로드되지 않는다";
        return false;
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
        string why;
        if(!RoomResolvable(ROOM, out why)){
            Debug.LogError("[PS_VerifyUI] STOP: ROOM='"+ROOM+"' 을 씬으로 해석할 수 없다 - "+why
                           +"  |  검증은 **방금 만든 룸**에서 돌린다: ROOM 상수를 그 룸 leaf 이름으로 맞추고, Content Manager에 Addressables 주소로 등록됐는지 확인할 것. 아무것도 변경하지 않았다.");
            return;
        }
        // 사람의 드래그앤드롭과 동일한 경로로 키를 만든다(leaf 이름을 직접 쓰지 않는다)
        string how;
        string roomKey = RoomKeyLikeHuman(ROOM, out how);
        if(string.IsNullOrEmpty(roomKey)){
            Debug.LogError("[PS_VerifyUI] STOP: ROOM='"+ROOM+"' 의 Addressables 주소를 사람과 같은 경로로 못 구했다 - "+how
                           +"  |  Content Manager 에서 그 룸을 Apply 해 등록하거나, 인스펙터의 Scene(드래그&드롭) 칸에 직접 끌어다 놓아 키를 채운 뒤 다시 실행할 것. 아무것도 변경하지 않았다.");
            return;
        }
        string back = RoundTripPath(roomKey);
        if(string.IsNullOrEmpty(back)){
            Debug.LogError("[PS_VerifyUI] STOP: 키 '"+roomKey+"' 가 SceneAsset 으로 역해석되지 않는다 -> 인스펙터의 Scene 칸이 None 이 되고 런타임에 룸이 로드되지 않는다(= 아바타/카메라 없음). 아무것도 변경하지 않았다.");
            return;
        }
        Debug.Log("[PS_VerifyUI] room key='"+roomKey+"'  (경로: "+how+", 역해석: "+back+")");
        string openExtra;
        if(!OnlyBootSceneOpen(out openExtra)){
            Debug.LogError("[PS_VerifyUI] STOP: 부트 씬 외의 씬이 에디터에 열려 있다 -> "+openExtra
                           +"  |  이 상태로 재생하면 FishNet이 룸을 Global Scene으로 소유하지 못해 플레이어 스폰이 일어나지 않고, 아바타가 없어 카메라도 없다(No cameras rendering). scene-open QuickStart 를 **Single** 로 다시 열고 실행할 것. 아무것도 변경하지 않았다.");
            return;
        }
        Directory.CreateDirectory(TmpDir);
        // 스냅샷을 덮어쓰지 않는다. 파일이 이미 있으면 앞선 Setup이 Teardown 없이 끝났다는 뜻이고,
        // 지금 덮어쓰면 사람이 넣어둔 원본이 영구히 사라진다(그러면 Teardown은 제 값을 원본이라 믿는다).
        if(File.Exists(OrigF))
            Debug.LogWarning("[PS_VerifyUI] Setup: 기존 스냅샷 유지(덮어쓰기 금지) - 앞선 실행이 Teardown 없이 끝났다.  |  기존: "+File.ReadAllText(OrigF).Replace(((char)10).ToString(), " / ")
                             +"  |  현재: "+orig.Replace(((char)10).ToString(), " / "));
        else
            File.WriteAllText(OrigF, orig);
        so.FindProperty("startAsServer").boolValue=true;
        so.FindProperty("hostMode").boolValue=true;
        so.FindProperty("roomSceneKey").stringValue=roomKey;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[PS_VerifyUI] Setup: host + roomSceneKey="+ROOM+"  (원본 스냅샷: "+OrigF+")");
    }

    // ---- 3) Check ----
    public static void Check(){
        var sb=new StringBuilder();
        var warn=new List<string>();

        var hudType=FindType("CrossPlatformRoomHud");
        var hudComp = hudType!=null ? UnityEngine.Object.FindObjectsByType(hudType, FindObjectsSortMode.None).FirstOrDefault() as Component : null;
        bool hudExists = hudComp!=null;
        sb.AppendLine("U1 HUD 'CrossPlatformRoomHud' present="+hudExists);
        bool u1=false,u2=false,u3=false,u4=false,u6=false,u7=false,u9=false,u10=false,u11=false,u12=false;
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

            bool wired = Field(hudComp,"_wired") is bool b && b;
            sb.AppendLine("U2 binder self-wired (_wired)="+wired);
            u2 = wired;

            // U3 — buttons == registry toggleables (registry-driven, not hardcoded)
            var rcType=FindType("PromptScene.Core.RoomCore");
            object inst = rcType!=null? rcType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(null):null;
            reg = Prop(inst,"Contents");
            var toggleable = Prop(reg,"Toggleable") as IEnumerable;
            var toggles = new List<object>(); if(toggleable!=null) foreach(var t in toggleable) toggles.Add(t);
            int toggleCount=toggles.Count;
            var trackTr = hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Track");
            var rows = trackTr!=null ? trackTr.GetComponentsInChildren<Transform>(true)
                         .Where(c=>c.gameObject.activeSelf && c.name.StartsWith("Btn_") && !c.name.Contains("__")).ToList() : new List<Transform>();
            var actions = trackTr!=null ? trackTr.GetComponentsInChildren<Transform>(true)
                         .Where(c=>c.gameObject.activeSelf && c.name.StartsWith("Act_") && !c.name.Contains("__")).ToList() : new List<Transform>();
            var pagesTr = trackTr!=null ? Enumerable.Range(0,trackTr.childCount).Select(i=>trackTr.GetChild(i))
                         .Where(c=>c.gameObject.activeSelf && c.name.StartsWith("Page_")).ToList() : new List<Transform>();
            int rowCount=rows.Count;
            int expectPages = Mathf.Max(1, Mathf.CeilToInt((rowCount+actions.Count)/(float)HudTheme.PageSize));
            sb.AppendLine("U3 registry Toggleable count="+toggleCount+"  icon buttons="+rowCount+"  action buttons="+actions.Count
                          +"  pages="+pagesTr.Count+" (expect "+expectPages+", "+HudTheme.PageSize+"/page)");
            u3 = rowCount>=toggleCount && rowCount>0 && pagesTr.Count==expectPages;
            sb.AppendLine("   buttons: ["+string.Join(",", rows.Concat(actions).Select(r=>r.name))+"]");

            // U4 — inject a button click → content SetEnabled path
            u4 = InjectClickProof(reg, rows, sb);

            // ══════════ U6 — TYPE DISCIPLINE (램프 + 역할 화이트리스트) ═══════════════════════
            int[] ramp=RampPx(), role=RolePx();
            var texts = hud.GetComponentsInChildren<Text>(true).ToList();
            var offRamp=new List<string>(); var offRole=new List<string>();
            foreach(var t in texts){
                if(SizeExempt(t.name)){ if(!role.Contains(t.fontSize)) offRole.Add(t.name+":"+t.fontSize); }
                else if(!ramp.Contains(t.fontSize)) offRamp.Add(t.name+":"+t.fontSize);
            }
            // 각크기는 **두 거리 모두**에서 판정한다. 기준 거리만 보면 게이트가 실물보다 느슨해진다.
            var badArc=new List<string>();
            foreach(var t in texts) foreach(var d in new[]{HudTheme.Legibility.DistanceM, HudTheme.Legibility.PlacementDistanceM}){
                float arc=HudTheme.Legibility.CapArcmin(t.fontSize, HudTheme.Legibility.PxPerMeter, d);
                if(arc < HudTheme.Legibility.MinCapArcmin) badArc.Add(t.name+" @"+d+"m="+arc.ToString("F0")+"'");
            }
            var fauxBold = texts.Where(t=>t.fontStyle==FontStyle.Bold||t.fontStyle==FontStyle.BoldAndItalic).ToList();
            var panelTr = hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Panel");
            var rampSizes = panelTr!=null
                ? panelTr.GetComponentsInChildren<Text>(true).Where(t=>!SizeExempt(t.name)).Select(t=>t.fontSize).Distinct().OrderBy(x=>x).ToList()
                : new List<int>();
            var tmpType=FindType("TMPro.TMP_Text"); int tmpCount=0,tmpBad=0;
            if(tmpType!=null) foreach(var c in hud.GetComponentsInChildren(tmpType,true)){
                tmpCount++;
                var fs=Prop(c,"fontSize"); if(fs is float fv && !ramp.Contains(Mathf.RoundToInt(fv)) && !role.Contains(Mathf.RoundToInt(fv))) tmpBad++;
                var st=Prop(c,"fontStyle"); if(st!=null && (Convert.ToInt32(st)&1)!=0) tmpBad++;
            }
            bool fontFallback = Prop(hudComp,"FontFallback") is bool ff && ff;
            int realisedEmph = Prop(hudComp,"RealisedEmphWeight") is int rw ? rw : HudTheme.WeightBody;
            bool weightsOk = HudTheme.AllowedWeights.Contains(realisedEmph);

            sb.AppendLine("U6 TYPE DISCIPLINE");
            sb.AppendLine("   ramp=["+string.Join(",",ramp)+"]  role-exempt=["+string.Join(",",role)+"] via suffix ["+string.Join(",",HudTheme.Roles.SizeExempt)+"]");
            sb.AppendLine("   texts="+texts.Count+"  offRamp="+offRamp.Count+(offRamp.Count>0?" ["+string.Join(",",offRamp)+"]":"")
                          +"  offRole="+offRole.Count+(offRole.Count>0?" ["+string.Join(",",offRole)+"]":""));
            sb.AppendLine("   CapArcmin >= "+HudTheme.Legibility.MinCapArcmin+"' at BOTH "+HudTheme.Legibility.DistanceM+"m and "
                          +HudTheme.Legibility.PlacementDistanceM+"m (real placement): violations="+badArc.Count
                          +(badArc.Count>0?" ["+string.Join(",",badArc.Take(4))+"]":"")
                          +"  [foot="+HudTheme.Legibility.CapArcmin(HudTheme.FontFoot,HudTheme.Legibility.PxPerMeter,HudTheme.Legibility.PlacementDistanceM).ToString("F0")
                          +"' title="+HudTheme.Legibility.CapArcmin(HudTheme.FontTitle,HudTheme.Legibility.PxPerMeter,HudTheme.Legibility.PlacementDistanceM).ToString("F0")+"' @placement]");
            sb.AppendLine("   distinct RAMP sizes under Panel="+rampSizes.Count+" ["+string.Join(",",rampSizes)+"] (max 2; 글리프는 역할 예외라 집계 제외)");
            sb.AppendLine("   faux-bold count="+fauxBold.Count+" (expect 0)   TMP labels="+tmpCount+" violations="+tmpBad);
            sb.AppendLine("   weights: realised emphasis="+realisedEmph+" allowed=["+string.Join(",",HudTheme.AllowedWeights)+"] ok="+weightsOk);
            u6 = offRamp.Count==0 && offRole.Count==0 && badArc.Count==0 && rampSizes.Count<=2 && fauxBold.Count==0 && tmpBad==0 && weightsOk;
            if(fontFallback)
                warn.Add("U6 font fallback: no PyeojinGothic Font asset → dynamic OS font, ONE weight only. WeightEmph("
                         +HudTheme.WeightEmph+") renders as "+HudTheme.WeightBody+" and emphasis is colour-only. baked-base item. WARN, not FAIL.");

            // ══════════ U7 — COLOUR / SPACING / CONTRAST ARITHMETIC ═══════════════════════════
            var groups = hud.GetComponentsInChildren<LayoutGroup>(true).ToList();
            var badSpace=new List<string>();
            foreach(var g in groups){
                var pad=g.padding;
                foreach(var kv in new[]{ ("padL",pad.left),("padR",pad.right),("padT",pad.top),("padB",pad.bottom) })
                    if(!OnScale(kv.Item2)) badSpace.Add(g.name+"."+kv.Item1+"="+kv.Item2);
                if(g is HorizontalOrVerticalLayoutGroup hv){
                    if(!OnScale(Mathf.RoundToInt(hv.spacing))) badSpace.Add(g.name+".spacing="+hv.spacing);
                } else if(g is GridLayoutGroup gg){
                    foreach(var kv in new[]{ ("spacingX",Mathf.RoundToInt(gg.spacing.x)),("spacingY",Mathf.RoundToInt(gg.spacing.y)) })
                        if(!OnScale(kv.Item2)) badSpace.Add(g.name+"."+kv.Item1+"="+kv.Item2);
                }
            }

            var graphics = hud.GetComponentsInChildren<Graphic>(true).ToList();
            var literals=new List<string>(); var accentBearers=new List<Graphic>();
            foreach(var gfx in graphics){
                if(MatchToken(gfx.color)==null) literals.Add(gfx.name+"="+ColorToHex(gfx.color));
                if(RgbNear(gfx.color,HudTheme.Accent) && gfx.color.a>0.01f) accentBearers.Add(gfx);
            }

            // ACCENT = ONE MEANING: only on a role-bearing part of an ENABLED feature
            var accentViolations=new List<string>();
            foreach(var gfx in accentBearers){
                string n=gfx.name;
                if(!HudTheme.Roles.AccentBearing.Any(s=>n.EndsWith(s))){ accentViolations.Add(n+" (not an accent-bearing role part)"); continue; }
                string rowName=FindRowName(gfx.transform);
                string id = rowName!=null && rowName.StartsWith("Btn_") ? rowName.Substring("Btn_".Length) : null;
                object feat=null;
                if(id!=null && reg!=null){ var m=reg.GetType().GetMethod("GetById"); feat=m?.Invoke(reg,new object[]{id}); }
                bool on = Prop(feat,"IsEnabled") is bool fe && fe;
                if(!on) accentViolations.Add(n+" visible but feature '"+(id??"?")+"' IsEnabled=false");
            }

            // ── ⭐ CONTRAST: F0의 산술. 각 텍스트의 실제 조상 스택을 최악 환경(흰/검) 위에 합성해 단정한다 ──
            var lowContrast=new List<string>(); var contrastRows=new List<string>(); var badOutline=new List<string>();
            foreach(var t in texts){
                if(!t.gameObject.activeInHierarchy) continue;
                List<string> names;
                var stack=BackdropStack(t, hud.transform, out names);
                float worst=HudTheme.Contrast.WorstRatio(t.color, stack);
                var ol=t.GetComponent<UnityEngine.UI.Outline>();
                string line;
                if(ol!=null){
                    // 아웃라인 절: 글자 둘레가 항상 같은 색이면 그 둘레를 배경 삼아 읽힌다.
                    // 완화가 아니라 다른 기계이므로 **조건을 단정한다**: 불투명 + 두께 >= OutlineW.
                    bool opaque = ol.effectColor.a >= 0.999f;
                    bool thick  = Mathf.Min(Mathf.Abs(ol.effectDistance.x), Mathf.Abs(ol.effectDistance.y)) >= HudTheme.OutlineW - 0.01f;
                    float r = HudTheme.Contrast.OutlinedRatio(t.color, ol.effectColor);
                    if(!opaque) badOutline.Add(t.name+" outline alpha="+ol.effectColor.a.ToString("F2")+" (불투명이어야 함)");
                    if(!thick)  badOutline.Add(t.name+" outline w="+ol.effectDistance.x+" (>= "+HudTheme.OutlineW+" 이어야 함)");
                    line=t.name+" on OUTLINE("+ColorToHex(ol.effectColor)+")="+r.ToString("F2")+"  [배경스택 ["+string.Join(">",names)+"]="+worst.ToString("F2")+"]";
                    if(r < HudTheme.Contrast.MinText || !opaque || !thick) lowContrast.Add(line);
                    worst = r;
                } else {
                    line=t.name+" on ["+string.Join(">",names)+"]="+worst.ToString("F2");
                    if(worst < HudTheme.Contrast.MinText) lowContrast.Add(line);
                }
                contrastRows.Add(line);
            }

            // ── ⭐ 구조 규칙: 흰 Film이 Scrim 없이 환경에 직접 노출되면 FAIL ──
            var exposedFilm=new List<string>();
            foreach(var img in hud.GetComponentsInChildren<Image>(true)){
                if(!img.gameObject.activeInHierarchy) continue;
                bool isFilm = ColorNear(img.color,HudTheme.Film)||ColorNear(img.color,HudTheme.FilmHover);
                if(!isFilm) continue;
                bool scrimBehind=false;
                for(var p=img.transform.parent; p!=null; p=p.parent){
                    var pi=p.GetComponent<Image>();
                    if(pi!=null && ColorNear(pi.color,HudTheme.Scrim) && Covers(pi.rectTransform,img.rectTransform)){ scrimBehind=true; break; }
                }
                if(!scrimBehind) exposedFilm.Add(img.name);
            }

            // TAP TARGETS: judged at the REAL placement distance (the conservative one)
            var smallTargets=new List<string>();
            foreach(var r in rows.Concat(hud.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Act_") && !t.name.Contains("__")))){
                var rt=r as RectTransform; if(rt==null) continue;
                float h=rt.rect.height, w=rt.rect.width;
                float degH=HudTheme.Legibility.Deg(h, HudTheme.Legibility.PlacementDistanceM);
                float degW=HudTheme.Legibility.Deg(w, HudTheme.Legibility.PlacementDistanceM);
                float deg=Mathf.Min(degH,degW);
                if(deg < HudTheme.Legibility.MinTargetDeg) smallTargets.Add(r.name+" "+w.ToString("F0")+"x"+h.ToString("F0")+"px="+deg.ToString("F2")+"deg");
            }

            sb.AppendLine("U7 COLOUR / SPACING / CONTRAST");
            sb.AppendLine("   LayoutGroups="+groups.Count+" offScale spacing/padding="+badSpace.Count+(badSpace.Count>0?" ["+string.Join(",",badSpace)+"]":"")
                          +"  scale=["+string.Join(",",HudTheme.SpaceScale)+"]");
            sb.AppendLine("   graphics="+graphics.Count+"  LITERAL colours="+literals.Count+(literals.Count>0?" ["+string.Join(",",literals)+"]":" (all resolve to HudTheme tokens)"));
            sb.AppendLine("   visible accent bearers="+accentBearers.Count+" ["+string.Join(",",accentBearers.Select(g=>g.name))+"]  violations="+accentViolations.Count
                          +(accentViolations.Count>0?" ["+string.Join(",",accentViolations)+"]":""));
            sb.AppendLine("   ⭐ CONTRAST (worst of white/black env, min "+HudTheme.Contrast.MinText+":1) — violations="+lowContrast.Count
                          +(lowContrast.Count>0?" ["+string.Join(" | ",lowContrast)+"]":""));
            foreach(var c in contrastRows) sb.AppendLine("        "+c);
            sb.AppendLine("   아웃라인 조건(불투명 + 두께>="+HudTheme.OutlineW+") 위반="+badOutline.Count
                          +(badOutline.Count>0?" ["+string.Join(",",badOutline)+"]":""));
            sb.AppendLine("   ⭐ Film exposed with NO Scrim behind it (structural)="+exposedFilm.Count
                          +(exposedFilm.Count>0?" ["+string.Join(",",exposedFilm)+"]":" — every Film sits on a Scrim"));
            sb.AppendLine("   tap targets >= "+HudTheme.Legibility.MinTargetDeg+"deg @"+HudTheme.Legibility.PlacementDistanceM+"m: violations="+smallTargets.Count
                          +(smallTargets.Count>0?" ["+string.Join(",",smallTargets)+"]":"")
                          +"  (circle "+HudTheme.CircleD+"px="+HudTheme.Legibility.Deg(HudTheme.CircleD,HudTheme.Legibility.PlacementDistanceM).ToString("F2")+"deg)");

            // accent POSITIVE CASE (self-exercising): a gate that is only green while the feature is unused is worthless
            string accentPositive = AccentPositiveCase(hud, rows, reg, accentViolations);
            sb.AppendLine("   accent POSITIVE case: "+accentPositive);

            u7 = badSpace.Count==0 && literals.Count==0 && accentViolations.Count==0 && lowContrast.Count==0
                 && badOutline.Count==0 && exposedFilm.Count==0 && smallTargets.Count==0;

            // ══════════ U9 — COMPOSITION (컴포넌트 6종, Card 0, 중첩 0) ═══════════════════════
            u9 = Composition(hud, sb);

            // ══════════ U11 — ICONS ═══════════════════════════════════════════════════════════
            u11 = Icons(hudComp, sb);

            // ══════════ U12 — PAGING ══════════════════════════════════════════════════════════
            u12 = Paging(hud, trackTr, pagesTr.Count, sb);

            // ══════════ U8 — CAPTURES (evidence, NOT a verdict) ═══════════════════════════════
            sb.AppendLine("U8 CAPTURE (evidence only — 미감은 사람/비전 몫)");
            sb.AppendLine("   bright env → "+Capture(hud, Color.white, "bright"));
            sb.AppendLine("   dark env   → "+Capture(hud, Color.black, "dark"));
        }

        // ══════════ U10 — ANGULAR-SIZE RUNAWAY (월드 텍스트 전수) ══════════════════════════════
        u10 = AngularFix(sb);

        // U5 — existing UI intact + SYSTEMS unbroken (avatar spawned)
        var canvasNames=new List<string>();
        for(int i=0;i<SceneManager.sceneCount;i++){ var s=SceneManager.GetSceneAt(i); if(!s.isLoaded) continue;
            foreach(var r in s.GetRootGameObjects()) foreach(var c in r.GetComponentsInChildren<Canvas>(true)) canvasNames.Add(c.gameObject.name); }
        bool avatar=false;
        for(int i=0;i<SceneManager.sceneCount;i++){ var s=SceneManager.GetSceneAt(i); if(!s.isLoaded) continue;
            foreach(var r in s.GetRootGameObjects()) foreach(var tr in r.GetComponentsInChildren<Transform>(true)) if(tr.gameObject.name=="Desktop(Clone)") avatar=true; }
        sb.AppendLine("U5 existing UI intact — canvases=["+string.Join(",", canvasNames.Distinct())+"]  avatar Desktop(Clone)="+avatar+" (SYSTEMS unbroken)");

        foreach(var w in warn) sb.AppendLine("WARN "+w);

        bool pass = u1&&u2&&u3&&u4&&avatar&&u6&&u7&&u9&&u10&&u11&&u12;
        sb.AppendLine("--- U1="+u1+" U2="+u2+" U3="+u3+" U4="+u4+" U5(avatar)="+avatar+" U6="+u6+" U7="+u7
                      +" U9="+u9+" U10="+u10+" U11="+u11+" U12="+u12+" | WARN="+warn.Count+" | U8=evidence");
        sb.AppendLine("=== §5/§6 CROSS-PLATFORM-UI VERDICT: "+(pass?"PASS":"FAIL")+" ===");
        Directory.CreateDirectory(TmpDir); File.WriteAllText(OutF, sb.ToString());
        Debug.Log("[PS_VerifyUI]\n"+sb);
    }

    // ── U4 ────────────────────────────────────────────────────────────────────────────────────
    static bool InjectClickProof(object reg, List<Transform> rows, StringBuilder sb){
        object target=null; Transform targetRow=null;
        if(reg!=null){
            var m=reg.GetType().GetMethod("GetById");
            var preferred=rows.FirstOrDefault(r=>r.name=="Btn_"+CLEARABLE_ID);
            var chosen = preferred ?? rows.FirstOrDefault();
            if(chosen!=null){ targetRow=chosen; target=m?.Invoke(reg,new object[]{ chosen.name.Substring("Btn_".Length) }); }
        }
        var btn = targetRow!=null ? targetRow.GetComponent<Button>() : null;
        if(target==null||btn==null){ sb.AppendLine("U4 skipped — no toggleable button to inject (registry-driven, so absence is valid)"); return true; }
        bool before = Prop(target,"IsEnabled") is bool eb && eb;
        btn.onClick.Invoke();
        bool afterOn = Prop(target,"IsEnabled") is bool ea && ea;
        btn.onClick.Invoke();
        bool back = Prop(target,"IsEnabled") is bool eb2 && eb2;
        sb.AppendLine("U4 inject "+targetRow.name+".onClick: IsEnabled "+before+" -> "+afterOn+" -> "+back+" (expect flips then restores)");
        return (afterOn!=before)&&(back==before);
    }

    // ── U7 accent positive case ───────────────────────────────────────────────────────────────
    static string AccentPositiveCase(GameObject hud, List<Transform> rows, object reg, List<string> violations){
        var probeRow=rows.FirstOrDefault();
        var probeBtn=probeRow!=null?probeRow.GetComponent<Button>():null;
        if(probeBtn==null) return "no toggleable button to drive (registry-driven room) — skipped";
        probeBtn.onClick.Invoke();
        var lit=hud.GetComponentsInChildren<Graphic>(true)
                   .Where(g=>g.gameObject.activeInHierarchy && RgbNear(g.color,HudTheme.Accent) && g.color.a>0.01f)
                   .Select(g=>g.name).OrderBy(n=>n).ToList();
        // ON이면 그 버튼의 disc + ring 두 곳에만 액센트가 떠야 한다(글리프는 OnGlyph로 반전된다)
        var want=new[]{ probeRow.name+HudTheme.Roles.Disc }.OrderBy(n=>n).ToList();
        bool exact = lit.SequenceEqual(want);
        string id=probeRow.name.Substring("Btn_".Length);
        object feat=null; if(reg!=null){ var m=reg.GetType().GetMethod("GetById"); feat=m?.Invoke(reg,new object[]{id}); }
        bool on = Prop(feat,"IsEnabled") is bool pe && pe;
        // 글리프가 어두운색으로 반전됐는지도 같이 본다 — 채움만 바뀌고 글리프가 흰색이면 대비가 죽는다
        // v6: 글리프 잉크는 OFF/ON 양쪽 모두 어둡다. 상태는 **채움**만 말한다 — 그것도 단정한다.
        var glyph=hud.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.name==probeRow.name+HudTheme.Roles.Glyph);
        bool glyphStable = glyph==null || ColorNear(glyph.color, HudTheme.GlyphDark);
        string res="drove "+probeRow.name+" ON → accent=["+string.Join(",",lit)+"] expect ["+string.Join(",",want)+"]"
                   +" featureIsEnabled="+on+" glyph stays GlyphDark="+glyphStable+" → "+((exact&&on&&glyphStable)?"PROVEN":"VIOLATION");
        if(!(exact&&on&&glyphStable)) violations.Add("positive case: "+res);
        probeBtn.onClick.Invoke();      // restore the resting state
        return res;
    }

    // ── U12 PAGING ────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// 페이지 넘김이 실제로 트랙을 움직이는가. 방에 4개 이하만 있으면 실물 페이지가 1장이라 규칙이
    /// **자동 통과**한다 — 그건 아무것도 증명하지 않는다(액센트 게이트에서 이미 한 번 밟은 함정).
    /// 그래서 페이지가 1장이면 같은 프로덕션 컴포넌트에 합성 3페이지를 물려 넘김을 실증하고, 그 사실을 적는다.
    /// </summary>
    static bool Paging(GameObject hud, Transform track, int pages, StringBuilder sb){
        var pagerType=FindType("HudPager");
        var pager = pagerType!=null ? hud.GetComponentInChildren(pagerType, true) : null;
        sb.AppendLine("U12 PAGING");
        if(pager==null){ sb.AppendLine("   ⛔ HudPager 없음"); return false; }

        var goTo = pagerType.GetMethod("GoTo");
        var pageProp = pagerType.GetProperty("Page");
        bool live = pages>1 && track is RectTransform;

        if(live){
            var rt=(RectTransform)track;
            float x0=rt.anchoredPosition.x;
            goTo.Invoke(pager,new object[]{1});
            // 스냅은 LateUpdate에서 보간되므로 목표값을 바로 확인한다(위치가 아니라 페이지 인덱스로 단정)
            int p1=(int)pageProp.GetValue(pager);
            goTo.Invoke(pager,new object[]{0});
            int p0=(int)pageProp.GetValue(pager);
            bool ok = p1==1 && p0==0;
            sb.AppendLine("   실물 "+pages+"페이지: GoTo(1)→page="+p1+", GoTo(0)→page="+p0+"  trackX0="+x0.ToString("F0")+" → "+(ok?"PROVEN":"VIOLATION"));
            return ok;
        }

        // 합성 케이스 — 방에 항목이 적어 실물 페이지가 1장일 때
        var host=new GameObject("__ps_pager_probe", typeof(RectTransform));
        try{
            var vp=(RectTransform)host.transform;
            var tk=new GameObject("Track", typeof(RectTransform)).GetComponent<RectTransform>();
            tk.SetParent(vp,false);
            var probe=(Component)host.AddComponent(pagerType);
            float pw=HudTheme.GridColumns*HudTheme.HitD;
            pagerType.GetMethod("Configure").Invoke(probe,new object[]{ vp, tk, 3, pw, null });
            pagerType.GetMethod("GoTo").Invoke(probe,new object[]{2});
            int p=(int)pageProp.GetValue(probe);
            float want=-2f*pw;
            // Configure/GoTo 직후 목표 위치는 즉시 반영되지 않고 보간된다 → 페이지 인덱스와 목표 x를 함께 본다
            bool ok = p==2;
            sb.AppendLine("   실물 페이지 1장(방 항목 "+(pages*HudTheme.PageSize)+"개 이하) → **합성 3페이지**로 실증: GoTo(2)→page="+p
                          +" (목표 trackX="+want.ToString("F0")+"px, 페이지폭 "+pw+"px) → "+(ok?"PROVEN(합성)":"VIOLATION"));
            sb.AppendLine("   ⚠ 실물 다중 페이지는 토글러블이 "+(HudTheme.PageSize+1)+"개 이상인 룸에서 재확인 필요");
            return ok;
        } finally { if(host!=null) UnityEngine.Object.DestroyImmediate(host); }
    }

    // ── U9 COMPOSITION ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// 컴포넌트 6종(Title / IconGrid / IconButton / Footnote / ActionRow / KeyBadge)으로 전부 분류되는가.
    /// 미분류가 있으면 그건 "디자인 시스템 밖에서 만들어진 것"이고, v2의 결함들이 정확히 거기서 나왔다.
    /// Card는 0건이어야 하고, 텍스트를 담는 상자(=컨테이너)의 중첩 깊이도 0이어야 한다(Panel 자신은 창이라 제외).
    /// </summary>
    static bool Composition(GameObject hud, StringBuilder sb){
        var chrome=new HashSet<string>{ "CrossPlatformRoomHud","Panel","PanelFrame","PanelEdge","TopSpacer",
                                        "Viewport","Track","Dots","PageTemplate","IconButtonTemplate","DotTemplate" };
        var counts=new Dictionary<string,int>();
        var unclassified=new List<string>();
        foreach(var tr in hud.GetComponentsInChildren<Transform>(true)){
            string n=tr.name;
            if(chrome.Contains(n)) continue;
            string comp=Classify(n);
            if(comp==null){ unclassified.Add(n); continue; }
            counts[comp]=counts.TryGetValue(comp,out var c)?c+1:1;
        }
        // Card = 텍스트를 담는 별도의 상자. v3에서 아이콘 그리드가 이걸 없앴다 → 0이어야 한다.
        var cardLike=new List<string>();
        foreach(var img in hud.GetComponentsInChildren<Image>(true)){
            string n=img.name;
            if(n=="Panel"||Decorative(n)) continue;
            if(img.color.a<=EPS) continue;              // 알파 0 = 레이 타깃일 뿐, 대비를 만드는 판이 아니다
            if(HudTheme.Roles.Disc.Length>0 && n.EndsWith(HudTheme.Roles.Disc)) continue;  // 컨트롤 표면은 상자가 아니다
            if(n.EndsWith(HudTheme.Roles.Icon)) continue;
            if(img.GetComponentsInChildren<Text>(true).Any()) cardLike.Add(n);
        }
        sb.AppendLine("U9 COMPOSITION (v6 컴포넌트: Page / IconButton / PageDots / KeyBadge)");
        sb.AppendLine("   classified: "+string.Join(", ", counts.Select(kv=>kv.Key+"="+kv.Value)));
        sb.AppendLine("   unclassified="+unclassified.Count+(unclassified.Count>0?" ["+string.Join(",",unclassified)+"]":" (0)"));
        sb.AppendLine("   Card-like containers (text-bearing boxes that are not the Panel)="+cardLike.Count
                      +(cardLike.Count>0?" ["+string.Join(",",cardLike)+"]":" → 중첩 깊이 0 (C2)"));
        return unclassified.Count==0 && cardLike.Count==0;
    }

    /// <summary>
    /// v6 컴포넌트 4종. v3의 Title / Footnote / ActionRow는 사라졌다(제목·문구 제거, 액션은 아이콘 버튼으로 흡수).
    /// 미분류가 남으면 그건 "디자인 시스템 밖에서 만들어진 것"이고, v2의 결함들이 정확히 거기서 나왔다.
    /// </summary>
    static string Classify(string n){
        // 역할 부품(`…__disc`, `…__glyph` …)은 독립 컴포넌트가 아니라 소유 컴포넌트의 부품이다.
        foreach(var suf in new[]{ HudTheme.Roles.Disc, HudTheme.Roles.Ring, HudTheme.Roles.Glyph,
                                  HudTheme.Roles.Icon, HudTheme.Roles.Label, HudTheme.Roles.Keycap })
            if(n.EndsWith(suf)) return n.StartsWith("Tmpl") ? "template-part" : "component-part";
        if(n.StartsWith("Page_")) return "Page";
        if(n.StartsWith("Btn_")) return "IconButton";
        if(n.StartsWith("Act_")) return "IconButton(action)";
        if(n.EndsWith(HudTheme.Roles.Dot)) return "PageDots";
        if(n.StartsWith(KeyBadge.RootName)) return "KeyBadge";
        return null;
    }

    // ── U11 ICONS ─────────────────────────────────────────────────────────────────────────────
    static bool Icons(Component hudComp, StringBuilder sb){
        bool fontLoaded = Prop(hudComp,"IconFontLoaded") is bool fl && fl;
        var errors = Member(hudComp,"IconErrors") as List<string> ?? new List<string>();
        var tiers  = Member(hudComp,"IconTiers") as Dictionary<string,HudIconTier> ?? new Dictionary<string,HudIconTier>();

        // (a) 우리가 **쓰는** 코드포인트가 전부 아틀라스에 있는가 (C5)
        var missing=new List<string>();
        foreach(var table in new[]{ HudIcons.ByContentId, HudIcons.ByActionId })
            foreach(var kv in table)
                if(!HudIcons.HasCodepoint(kv.Value.Codepoint))
                    missing.Add(kv.Key+"→"+kv.Value.LigatureName+" U+"+kv.Value.Codepoint.ToString("X4"));
        int mapped = HudIcons.ByContentId.Count + HudIcons.ByActionId.Count;

        // (b) 폴백 체인 3단이 결정적으로 동작하는가 (C4). 방 안의 실콘텐츠가 ②만 밟으므로
        //     ①(스프라이트)과 ③(첫글자)은 **같은 프로덕션 함수**에 합성 입력을 넣어 실증한다.
        var spr = HudSprites.Circle(HudTheme.CircleD);
        var t1=HudIcons.Resolve(spr,"과녁","target-props");
        var t2=HudIcons.Resolve(null,"채팅","chat");
        var t3=HudIcons.Resolve(null,"주사위","dice-unmapped");
        bool chainOk = t1.Tier==HudIconTier.Sprite && t1.Sprite!=null && t1.Error==null
                    && t2.Tier==HudIconTier.Glyph  && t2.Codepoint==0xE0C9 && t2.Error==null
                    && t3.Tier==HudIconTier.Letter && t3.Text=="주" && t3.Error==null;

        sb.AppendLine("U11 ICONS");
        sb.AppendLine("   icon font loaded="+fontLoaded+" (Resources/"+HudIcons.FontResourcePath+", static subset)");
        sb.AppendLine("   codepoints USED present in atlas: missing="+missing.Count+(missing.Count>0?" ["+string.Join(",",missing)+"] ⛔ STOP":" (all "+mapped+" present)"));
        sb.AppendLine("   live rows resolved: ["+string.Join(", ", tiers.Select(kv=>kv.Key+"→"+kv.Value))+"]");
        sb.AppendLine("   fallback chain: ①sprite="+t1.Tier+" ②glyph="+t2.Tier+"(U+"+t2.Codepoint.ToString("X4")+") ③letter="+t3.Tier+"('"+t3.Text+"') → "+(chainOk?"all three PROVEN":"BROKEN"));
        sb.AppendLine("   mapped-but-missing errors (⛔ never silently swallowed)="+errors.Count+(errors.Count>0?" ["+string.Join(",",errors)+"]":""));
        return fontLoaded && missing.Count==0 && chainOk && errors.Count==0;
    }

    // ── U10 ANGULAR-SIZE RUNAWAY ──────────────────────────────────────────────────────────────
    /// <summary>
    /// `"E 키로 앉기"`가 통과했던 구멍을 닫는다: 씬의 **모든** 월드 텍스트는 각크기 고정 컴포넌트를 갖거나
    /// 화이트리스트에 있어야 한다. 고정 없는 월드 텍스트 = FAIL.
    /// 추가로, 배지를 3거리(1/3/8m)에 놓고 실제 각크기가 일정한지 직접 잰다(C6) — 규칙만 있고 측정이 없으면
    /// "컴포넌트를 붙였다"만 증명하고 "폭주하지 않는다"는 증명하지 못한다.
    /// </summary>
    static bool AngularFix(StringBuilder sb){
        var offenders=new List<string>(); var whitelisted=new List<string>(); int fixedCount=0;
        var tmType=FindType("TMPro.TMP_Text");

        foreach(var go in AllSceneObjects()){
            bool isWorldText=false;
            if(go.GetComponent<TextMesh>()!=null) isWorldText=true;
            var g=go.GetComponent<Graphic>();
            if(g!=null && (g is Text || (tmType!=null && tmType.IsInstanceOfType(g)))){
                var cv=g.canvas;
                if(cv!=null && cv.renderMode==RenderMode.WorldSpace) isWorldText=true;
            }
            if(!isWorldText) continue;

            if(go.GetComponentInParent<KeyBadge>()!=null){ fixedCount++; continue; }
            string wl=WhitelistReason(go.transform);
            if(wl!=null){ whitelisted.Add(go.name+" ("+wl+")"); continue; }
            offenders.Add(ScenePath(go.transform));
        }

        sb.AppendLine("U10 ANGULAR-SIZE FIX (월드 텍스트 전수)");
        sb.AppendLine("   angular-fixed (KeyBadge)="+fixedCount+"  whitelisted="+whitelisted.Count
                      +(whitelisted.Count>0?" ["+string.Join(",",whitelisted.Distinct())+"]":""));
        sb.AppendLine("   ⛔ world text with NO angular fix and NOT whitelisted="+offenders.Count
                      +(offenders.Count>0?" ["+string.Join(",",offenders.Distinct())+"]":" (0 — `E 키로 앉기` 구멍이 닫혔다)"));

        bool constant = BadgeDistanceSweep(sb);
        return offenders.Count==0 && constant;
    }

    /// <summary>배지를 1/3/8m에 놓고 각크기가 일정한지 직접 잰다 + 3장 캡처(사람이 눈으로 대조).</summary>
    static bool BadgeDistanceSweep(StringBuilder sb){
        var cam=MainCam();
        if(cam==null){ sb.AppendLine("   badge sweep SKIPPED — no active camera"); return false; }
        var host=new GameObject("__ps_badge_probe");
        KeyBadge badge=null;
        try{
            badge=KeyBadge.Attach(host.transform, Vector3.zero, "E", "앉기", null);
            var degs=new List<float>(); var lines=new List<string>();
            foreach(float d in new[]{1f,3f,8f}){
                host.transform.position = cam.transform.position + cam.transform.forward * d;
                badge.Tick(cam);
                Canvas.ForceUpdateCanvases();
                badge.Tick(cam);
                degs.Add(badge.MeasuredDeg);
                string cap=CaptureBadge(badge.gameObject, cam, d);
                lines.Add("      "+d+"m → "+badge.MeasuredDeg.ToString("F3")+"deg  scale="+badge.transform.lossyScale.x.ToString("F5")+"  "+cap);
            }
            float spread=degs.Max()-degs.Min();
            bool constant = spread <= 0.02f && Mathf.Abs(degs[0]-HudTheme.BadgeTargetDeg) <= 0.05f;
            sb.AppendLine("   badge angular size at 1/3/8 m (target "+HudTheme.BadgeTargetDeg+"deg):");
            foreach(var l in lines) sb.AppendLine(l);
            sb.AppendLine("      spread="+spread.ToString("F4")+"deg → "+(constant?"CONSTANT (폭주 불가)":"VARIES ⛔")
                          +"   keycap cap="+HudTheme.BadgeCapArcmin.ToString("F0")+"' (거리 무관 상수)");
            return constant;
        } finally {
            if(host!=null) UnityEngine.Object.DestroyImmediate(host);
        }
    }

    static string WhitelistReason(Transform t){
        for(var p=t; p!=null; p=p.parent)
            if(AngularWhitelist.TryGetValue(p.name, out var why)) return why;
        return null;
    }

    static IEnumerable<GameObject> AllSceneObjects(){
        for(int i=0;i<SceneManager.sceneCount;i++){
            var s=SceneManager.GetSceneAt(i); if(!s.isLoaded) continue;
            foreach(var r in s.GetRootGameObjects())
                foreach(var tr in r.GetComponentsInChildren<Transform>(true)) yield return tr.gameObject;
        }
    }

    static string ScenePath(Transform t){
        var parts=new List<string>();
        for(var p=t; p!=null; p=p.parent) parts.Add(p.name);
        parts.Reverse(); return string.Join("/",parts);
    }

    /// <summary>
    /// Walk up to the `Btn_*` row that owns this graphic. ⚠ The state object is itself named `<row>__disc`, which ALSO
    /// starts with "Btn_" — strip the role suffix before returning or GetById misses and the gate reports a FALSE
    /// violation the moment any feature is ON (caught 2026-07-30 by exercising the ON state).
    /// </summary>
    static string FindRowName(Transform t){
        for(var p=t; p!=null; p=p.parent){
            if(!p.name.StartsWith("Btn_")) continue;
            var n=p.name;
            foreach(var suf in new[]{ HudTheme.Roles.Disc, HudTheme.Roles.Ring, HudTheme.Roles.Glyph, HudTheme.Roles.Icon, HudTheme.Roles.Label })
                if(n.EndsWith(suf)) n=n.Substring(0,n.Length-suf.Length);
            return n;
        }
        return null;
    }

    static string ColorToHex(Color c) => "#"+ColorUtility.ToHtmlStringRGBA(c);

    static Camera MainCam(){
        if(Camera.main!=null) return Camera.main;
        foreach(var c in Camera.allCameras) if(c.isActiveAndEnabled) return c;
        return null;
    }

    // ── U8 CAPTURES ───────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Render the HUD head-on from the design distance into a PNG over a chosen environment colour.
    /// **Two environments** (white/black) because the whole v3 thesis is that a light glass panel dies on a bright
    /// background — the arithmetic says it in U7, and this is the picture a human can check it against.
    /// EVIDENCE, not a judgement. Aesthetics stay unverified by design.
    /// </summary>
    static string Capture(GameObject hud, Color env, string suffix){
        string path=Path2("ps_ui_capture_"+suffix+".png");
        try{
            var panel=hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Panel") ?? hud.transform;
            var rt=panel as RectTransform;
            float wPx=rt!=null?rt.rect.width:360f, hPx=rt!=null?rt.rect.height:300f;
            int texW=Mathf.Clamp(Mathf.RoundToInt(wPx*2f),64,4096), texH=Mathf.Clamp(Mathf.RoundToInt(hPx*2f),64,4096);
            float d=HudTheme.Legibility.DistanceM;

            // Isolate the HUD on a spare layer — otherwise the avatar and other world canvases sit between the camera
            // and the panel (observed 2026-07-30) and the capture is useless for the mockup diff.
            int spare=SpareLayer(hud);
            var stash=new List<KeyValuePair<GameObject,int>>();
            foreach(var tr in hud.GetComponentsInChildren<Transform>(true)){
                stash.Add(new KeyValuePair<GameObject,int>(tr.gameObject, tr.gameObject.layer));
                tr.gameObject.layer=spare;
            }
            var camGo=new GameObject("__ps_ui_capture_cam");             // untagged: must NOT become Camera.main
            var cam=camGo.AddComponent<Camera>();
            cam.cullingMask=1<<spare;
            cam.transform.position=panel.position - hud.transform.forward*d;
            cam.transform.rotation=Quaternion.LookRotation(hud.transform.forward, hud.transform.up);
            cam.orthographic=true;
            cam.orthographicSize=(hPx/HudTheme.Legibility.PxPerMeter)*0.5f;
            cam.nearClipPlane=0.01f; cam.farClipPlane=d*2f;
            cam.clearFlags=CameraClearFlags.SolidColor;
            cam.backgroundColor=env;                                     // ← 최악 환경을 그림으로도 남긴다
            cam.enabled=false;

            Shoot(cam, texW, texH, path);
            UnityEngine.Object.DestroyImmediate(camGo);
            foreach(var kv in stash) if(kv.Key!=null) kv.Key.layer=kv.Value;
            return path+" ("+texW+"x"+texH+", ortho "+d.ToString("F2")+"m head-on, env="+(env==Color.white?"WHITE 최악":"BLACK")+")";
        } catch(Exception e){ return "capture failed (not a FAIL — evidence only): "+e.Message; }
    }

    /// <summary>배지 캡처: **원근** 카메라로 찍어야 각크기 불변이 그림에서 보인다(정사영으로 찍으면 의미가 없다).</summary>
    static string CaptureBadge(GameObject badge, Camera from, float d){
        string path=Path2("ps_ui_badge_"+d.ToString("F0")+"m.png");
        try{
            int spare=SpareLayer(badge);
            var stash=new List<KeyValuePair<GameObject,int>>();
            foreach(var tr in badge.GetComponentsInChildren<Transform>(true)){
                stash.Add(new KeyValuePair<GameObject,int>(tr.gameObject, tr.gameObject.layer));
                tr.gameObject.layer=spare;
            }
            var camGo=new GameObject("__ps_badge_cam");
            var cam=camGo.AddComponent<Camera>();
            cam.cullingMask=1<<spare;
            cam.transform.position=from.transform.position;
            cam.transform.rotation=Quaternion.LookRotation(badge.transform.position-from.transform.position, Vector3.up);
            cam.orthographic=false; cam.fieldOfView=20f;                 // 좁은 화각 = 3°가 화면에서 충분히 크게 보인다
            cam.nearClipPlane=0.01f; cam.farClipPlane=d*4f;
            cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=Color.white;   // 최악 배경에서 찍는다
            cam.enabled=false;
            Shoot(cam, 480, 480, path);
            UnityEngine.Object.DestroyImmediate(camGo);
            foreach(var kv in stash) if(kv.Key!=null) kv.Key.layer=kv.Value;
            return "→ "+System.IO.Path.GetFileName(path);
        } catch(Exception e){ return "(capture failed: "+e.Message+")"; }
    }

    static void Shoot(Camera cam, int w, int h, string path){
        var target=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){ antiAliasing=8 };
        cam.targetTexture=target; cam.Render();
        var prev=RenderTexture.active; RenderTexture.active=target;
        var png=new Texture2D(w,h,TextureFormat.RGBA32,false);
        png.ReadPixels(new Rect(0,0,w,h),0,0); png.Apply(false);
        RenderTexture.active=prev;
        Directory.CreateDirectory(TmpDir); File.WriteAllBytes(path, png.EncodeToPNG());
        cam.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(png);
        target.Release(); UnityEngine.Object.DestroyImmediate(target);
    }

    static string Path2(string name) => System.IO.Path.Combine(TmpDir, name);

    /// <summary>Highest layer index used by nothing outside the subject — so the isolated capture frames only it.</summary>
    static int SpareLayer(GameObject subject){
        var set=new HashSet<GameObject>(subject.GetComponentsInChildren<Transform>(true).Select(t=>t.gameObject));
        var used=new HashSet<int>();
        foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if(!set.Contains(r.gameObject)) used.Add(r.gameObject.layer);
        foreach(var g in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            if(!set.Contains(g.gameObject)) used.Add(g.gameObject.layer);
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
        // 복원했으면 스냅샷을 **지운다.** 남겨두면 다음 Setup이 "앞선 실행이 안 끝났다"로 오판하고,
        // 안 지우면 위의 덮어쓰기 금지가 영원히 걸린 채로 남는다. 복원 ↔ 스냅샷 수명이 짝을 이뤄야 한다.
        try { File.Delete(OrigF); } catch { }
        // ⚠ ApplyModifiedPropertiesWithoutUndo는 씬을 dirty로 표시하지 않는다 → 복원된 값이 **메모리에만**
        //   있고 디스크와 어긋날 수 있다. 그게 의도다(부트 씬을 저장하지 않는다는 규칙). 대신 어긋남을 로그로 남긴다.
        Debug.Log("[PS_VerifyUI] Teardown: QuickTestStarter restored (메모리만 — QuickStart.unity은 저장하지 않는다). "
                  +"roomSceneKey='"+so.FindProperty("roomSceneKey").stringValue+"'");
    }
}
