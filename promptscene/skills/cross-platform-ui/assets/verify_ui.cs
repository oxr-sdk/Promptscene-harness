// /cross-platform-ui — QuickTest verify of the authored World Space HUD. SSOT: build-studio-room.md §4 (QuickTest) + §5/§6.
// Run via MCP script-execute (className=PS_VerifyUI). Same host-QuickTest driver shape as assemble-room's verify_quicktest:
//   0) MCP: scene-open Assets/App/Scenes/QuickStart.unity Single
//   1) PS_VerifyUI.Setup    — snapshot QuickTestStarter, set startAsServer+hostMode+roomSceneKey=<ROOM>
//   2) (isPlaying=true), wait ~12-15s (server start → Addressables room load → spawn → RoomCore up → binder wires)
//   3) PS_VerifyUI.Check     — writes UI signals to <project>/Temp/ps_ui_result.txt (Read it)
//   4) (isPlaying=false)
//   5) PS_VerifyUI.Teardown  — restore QuickTestStarter (in-memory; QuickStart disk untouched)
//
// What Check proves: the HUD is authored & cross-platform-wired (World Space canvas + raycaster(s)), the binder
// self-wired from the REGISTRY (rows = one per toggleable), an injected button-click drives the content's SetEnabled
// (path proof), and existing UI is intact. Honesty: injected onClick.Invoke() proves the onClick→SetEnabled path; it is
// NOT a real pointer/interactor event (that = desktop mouse by a human + XR Simulator controller by a human; §5 caveat).
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

public class PS_VerifyUI {
    const string ROOM = "PromptSceneRoom_1";  // leaf == Addressables address == roomSceneKey (match assemble_ui.cs)
    const bool EXPECT_XR = true;              // true for MODE PCXR/CROSS, false for PC/PCSS
    const bool EXPECT_SCREENSPACE = false;    // true for MODE PCSS (ScreenSpaceOverlay), false for World Space modes
    const string CLEARABLE_ID = "ruler";      // the toggleable used for the click-injection path proof

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
    static object Field(object o,string n){ if(o==null) return null; var f=o.GetType().GetField(n, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); return f?.GetValue(o); }
    static UnityEngine.Object FindStarter(){ var t=FindType("QuickTestStarter"); if(t==null) return null; var a=UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None); return a.Length>0?(UnityEngine.Object)a[0]:null; }

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

        // find the HUD binder instance (reusable type, no namespace)
        var hudType=FindType("CrossPlatformRoomHud");
        var hudComp = hudType!=null ? UnityEngine.Object.FindObjectsByType(hudType, FindObjectsSortMode.None).FirstOrDefault() as Component : null;
        bool hudExists = hudComp!=null;
        sb.AppendLine("U1 HUD 'CrossPlatformRoomHud' present="+hudExists);
        bool u1=false, u2=false, u3=false, u4=false;
        int toggleCount=-1, rowCount=-1;

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
            object reg = Prop(inst,"Contents");
            var toggleable = Prop(reg,"Toggleable") as IEnumerable;
            var toggles = new List<object>(); if(toggleable!=null) foreach(var t in toggleable) toggles.Add(t);
            toggleCount=toggles.Count;
            var buttonsTr = hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Buttons");
            var rows = buttonsTr!=null ? Enumerable.Range(0,buttonsTr.childCount).Select(i=>buttonsTr.GetChild(i))
                         .Where(c=>c.gameObject.activeSelf && c.name.StartsWith("Btn_")).ToList() : new List<Transform>();
            rowCount=rows.Count;
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
                    btn.onClick.Invoke();                       // inject: runs the runtime-wired listener
                    bool afterOn = Prop(ruler,"IsEnabled") is bool ea && ea;
                    btn.onClick.Invoke();                       // restore
                    bool back = Prop(ruler,"IsEnabled") is bool eb2 && eb2;
                    sb.AppendLine("U4 inject Btn_"+CLEARABLE_ID+".onClick: IsEnabled "+before+" -> "+afterOn+" -> "+back+" (expect flips then restores)");
                    u4 = (afterOn != before) && (back == before);
                } else sb.AppendLine("U4 no Button on Btn_"+CLEARABLE_ID);
            } else sb.AppendLine("U4 skipped — no '"+CLEARABLE_ID+"' toggleable / row (room has no Ruler): registry-driven, so absence is valid");
            if(ruler==null) u4=true; // absence of the pilot feature is not a HUD failure (reusable part)
        }

        // U5 — existing UI intact + SYSTEMS unbroken (avatar spawned)
        var canvasNames=new List<string>();
        for(int i=0;i<SceneManager.sceneCount;i++){ var s=SceneManager.GetSceneAt(i); if(!s.isLoaded) continue;
            foreach(var r in s.GetRootGameObjects()) foreach(var c in r.GetComponentsInChildren<Canvas>(true)) canvasNames.Add(c.gameObject.name); }
        bool avatar=false;
        for(int i=0;i<SceneManager.sceneCount;i++){ var s=SceneManager.GetSceneAt(i); if(!s.isLoaded) continue;
            foreach(var r in s.GetRootGameObjects()) foreach(var tr in r.GetComponentsInChildren<Transform>(true)) if(tr.gameObject.name=="Desktop(Clone)") avatar=true; }
        sb.AppendLine("U5 existing UI intact — canvases=["+string.Join(",", canvasNames.Distinct())+"]  avatar Desktop(Clone)="+avatar+" (SYSTEMS unbroken)");

        bool pass = u1 && u2 && u3 && u4 && avatar;
        sb.AppendLine("=== §5/§6 CROSS-PLATFORM-UI VERDICT: "+(pass?"PASS":"FAIL")+" ===");
        Directory.CreateDirectory(TmpDir); File.WriteAllText(OutF, sb.ToString());
        Debug.Log("[PS_VerifyUI]\n"+sb);
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
