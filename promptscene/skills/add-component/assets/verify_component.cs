// add-component — §5 (content) + §6.5 (SYSTEMS unbroken) auto-judge, run IN the live QuickTest room (Play mode).
// Procedure SSOT: build-studio-room.md §4 (QuickTest) + §6.5 (COMPOSITION) ; contract §5. Retrospective A (migration §9–§15).
// Run via MCP script-execute (className=PS_VerifyComponent). Three static entry points, driven in order:
//
//   0) MCP: scene-open Assets/App/Scenes/QuickStart.unity Single   (boot scene = NetworkManager + QuickTestStarter)
//   1) script-execute PS_VerifyComponent.Setup     — snapshot QuickTestStarter, set server+host+roomSceneKey=<ROOM>
//   2) script-execute (isPlaying=true) — enter Play. Wait ~12-15s (server → Addressables room load → spawn → RoomCore).
//   3) script-execute PS_VerifyComponent.Check     — writes signals to <project>/Temp/ps_addcomp_result.txt (Read it)
//   4) script-execute (isPlaying=false) — exit Play.
//   5) script-execute PS_VerifyComponent.Teardown  — restores QuickTestStarter (in memory only; disk untouched)
//
// KIND branches the acceptance (retrospective A "차이"):
//   FEATURE      — IToggleableContent that self-registers to RoomCore.Contents. Asserts §5: registered by ID +
//                  SetEnabled(true/false/double-on) exception-free + IsEnabled tracks + Meta valid.
//   COMPOSITION  — plain MonoBehaviour (NOT registered — scene-resident). Asserts: the type is present & alive in the
//                  room, and it did NOT leak into the registry (COMPOSITION must never self-register).
// Reflection-only (no compile-time dep on App.HotUpdate) — matches the sibling verify scripts. QuickStart edited in
// memory only (never scene-save) so the shipped boot scene is untouched.
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.SceneManagement;

public class PS_VerifyComponent {
    // ---- set these three before running (add-component fills them from the plan) ----
    const string ROOM = "AssembleRoom";        // leaf == Addressables address == roomSceneKey
    const string KIND = "FEATURE";             // "FEATURE" | "COMPOSITION"
    const string CONTENT_ID = "chair-sit";     // FEATURE: registry Id.  COMPOSITION: ignored (use TYPE_NAME)
    const string TYPE_NAME = "ChairSitContent";// C# type simple name of the placed component (both kinds)
    // -------------------------------------------------------------------------------

    static string TmpDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
    static string OrigF  => Path.Combine(TmpDir, "ps_addcomp_orig.txt");
    static string OutF   => Path.Combine(TmpDir, "ps_addcomp_result.txt");

    static Type FindType(string full){
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){
            var t=a.GetType(full); if(t!=null) return t;
            try{ t = a.GetTypes().FirstOrDefault(x=>x.Name==full); }catch{ t=null; }
            if(t!=null) return t;
        }
        return null;
    }
    static UnityEngine.Object FindStarter(){
        var t = FindType("QuickTestStarter"); if(t==null) return null;
        var arr = UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None);
        return arr.Length>0 ? (UnityEngine.Object)arr[0] : null;
    }
    static object Prop(object o, string n){ return o?.GetType().GetProperty(n, BindingFlags.Public|BindingFlags.Instance)?.GetValue(o); }
    static object Field(object o, string n){
        var t=o?.GetType(); while(t!=null){ var f=t.GetField(n, BindingFlags.Public|BindingFlags.Instance); if(f!=null) return f.GetValue(o); t=t.BaseType; } return null; }
    static string Inner(Exception e){ while(e.InnerException!=null) e=e.InnerException; return e.GetType().Name+": "+e.Message; }

    // ---- 1) Setup ----

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

    public static void Setup(){
        var starter = FindStarter();
        if(starter==null){ Debug.LogError("[PS_AddComp] QuickTestStarter not in scene — is QuickStart open?"); return; }
        var so = new SerializedObject(starter);
        string orig = string.Join("\n", new[]{
            "startAsServer="+so.FindProperty("startAsServer").boolValue,
            "hostMode="+so.FindProperty("hostMode").boolValue,
            "roomSceneKey="+so.FindProperty("roomSceneKey").stringValue,
        });
        string why;
        if(!RoomResolvable(ROOM, out why)){
            Debug.LogError("[PS_AddComp] STOP: ROOM='"+ROOM+"' 을 씬으로 해석할 수 없다 - "+why
                           +"  |  검증은 **방금 만든 룸**에서 돌린다: ROOM 상수를 그 룸 leaf 이름으로 맞추고, Content Manager에 Addressables 주소로 등록됐는지 확인할 것. 아무것도 변경하지 않았다.");
            return;
        }
        // 사람의 드래그앤드롭과 동일한 경로로 키를 만든다(leaf 이름을 직접 쓰지 않는다)
        string how;
        string roomKey = RoomKeyLikeHuman(ROOM, out how);
        if(string.IsNullOrEmpty(roomKey)){
            Debug.LogError("[PS_AddComp] STOP: ROOM='"+ROOM+"' 의 Addressables 주소를 사람과 같은 경로로 못 구했다 - "+how
                           +"  |  Content Manager 에서 그 룸을 Apply 해 등록하거나, 인스펙터의 Scene(드래그&드롭) 칸에 직접 끌어다 놓아 키를 채운 뒤 다시 실행할 것. 아무것도 변경하지 않았다.");
            return;
        }
        string back = RoundTripPath(roomKey);
        if(string.IsNullOrEmpty(back)){
            Debug.LogError("[PS_AddComp] STOP: 키 '"+roomKey+"' 가 SceneAsset 으로 역해석되지 않는다 -> 인스펙터의 Scene 칸이 None 이 되고 런타임에 룸이 로드되지 않는다(= 아바타/카메라 없음). 아무것도 변경하지 않았다.");
            return;
        }
        Debug.Log("[PS_AddComp] room key='"+roomKey+"'  (경로: "+how+", 역해석: "+back+")");
        string openExtra;
        if(!OnlyBootSceneOpen(out openExtra)){
            Debug.LogError("[PS_AddComp] STOP: 부트 씬 외의 씬이 에디터에 열려 있다 -> "+openExtra
                           +"  |  이 상태로 재생하면 FishNet이 룸을 Global Scene으로 소유하지 못해 플레이어 스폰이 일어나지 않고, 아바타가 없어 카메라도 없다(No cameras rendering). scene-open QuickStart 를 **Single** 로 다시 열고 실행할 것. 아무것도 변경하지 않았다.");
            return;
        }
        Directory.CreateDirectory(TmpDir);
        // 스냅샷을 덮어쓰지 않는다. 파일이 이미 있으면 앞선 Setup이 Teardown 없이 끝났다는 뜻이고,
        // 지금 덮어쓰면 사람이 넣어둔 원본이 영구히 사라진다(그러면 Teardown은 제 값을 원본이라 믿는다).
        if(File.Exists(OrigF))
            Debug.LogWarning("[PS_AddComp] Setup: 기존 스냅샷 유지(덮어쓰기 금지) - 앞선 실행이 Teardown 없이 끝났다.  |  기존: "+File.ReadAllText(OrigF).Replace(((char)10).ToString(), " / ")
                             +"  |  현재: "+orig.Replace(((char)10).ToString(), " / "));
        else
            File.WriteAllText(OrigF, orig);
        so.FindProperty("startAsServer").boolValue = true;
        so.FindProperty("hostMode").boolValue = true;         // single-editor avatar observable
        so.FindProperty("roomSceneKey").stringValue = roomKey;
        so.ApplyModifiedPropertiesWithoutUndo();              // in-memory only, do NOT scene-save
        Debug.Log("[PS_AddComp] Setup: server+host roomSceneKey="+ROOM+" (orig saved)");
    }

    // ---- 3) Check: §6.5 (SYSTEMS unbroken) + §5 (content, KIND-branched) ----
    public static void Check(){
        var sb = new StringBuilder();
        sb.AppendLine("KIND="+KIND+" TYPE="+TYPE_NAME+" CONTENT_ID="+CONTENT_ID+" ROOM="+ROOM);

        // ---------- §6.5 SYSTEMS unbroken ----------
        var loaded = new List<string>();
        for(int i=0;i<SceneManager.sceneCount;i++){ var s=SceneManager.GetSceneAt(i); if(s.isLoaded) loaded.Add(s.name); }
        bool roomLoaded = loaded.Contains(ROOM);
        sb.AppendLine("S1 room loaded ("+ROOM+")="+roomLoaded+"  [scenes: "+string.Join(",", loaded)+"]");

        GameObject avatar = null;
        for(int i=0;i<SceneManager.sceneCount && avatar==null;i++){
            var s=SceneManager.GetSceneAt(i); if(!s.isLoaded) continue;
            foreach(var r in s.GetRootGameObjects()){
                foreach(var tr in r.GetComponentsInChildren<Transform>(true))
                    if(tr.gameObject.name=="Desktop(Clone)"){ avatar=tr.gameObject; break; }
                if(avatar!=null) break;
            }
        }
        bool avatarSpawned = avatar!=null;
        sb.AppendLine("S2 avatar Desktop(Clone) spawned="+avatarSpawned);

        var rcType = FindType("PromptScene.Core.RoomCore");
        object inst = rcType!=null? rcType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(null) : null;
        sb.AppendLine("S3 RoomCore.Instance initialized="+(inst!=null));
        object registry = inst!=null? rcType.GetProperty("Contents", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(inst) : null;
        bool systemsOk = roomLoaded && avatarSpawned && inst!=null && registry!=null;

        // ---------- §5 content ----------
        bool contentOk = false;
        if(KIND=="COMPOSITION"){
            // COMPOSITION = plain MonoBehaviour, scene-resident, NOT registered. Assert present + NOT in registry.
            var compType = FindType(TYPE_NAME);
            UnityEngine.Object live = compType!=null ? UnityEngine.Object.FindObjectsByType(compType, FindObjectsSortMode.None).FirstOrDefault() : null;
            bool present = live!=null;
            sb.AppendLine("RESULT(A) COMPOSITION type '"+TYPE_NAME+"' present & alive in room="+present);
            // must NOT have leaked into the registry (COMPOSITION never self-registers — retrospective A "차이")
            bool leaked = false;
            if(registry!=null){
                var all = Prop(registry, "All") as IEnumerable;
                if(all!=null) foreach(var c in all) if(c!=null && c.GetType().Name==TYPE_NAME) leaked=true;
            }
            sb.AppendLine("RESULT(B) NOT leaked into registry (scene-resident, unregistered)="+(!leaked));
            contentOk = present && !leaked;
        } else {
            // FEATURE = IToggleableContent that self-registers. §5: registered + SetEnabled no-throw + IsEnabled tracks + Meta valid.
            object content = null;
            if(registry!=null){
                var getById = registry.GetType().GetMethod("GetById");
                content = getById?.Invoke(registry, new object[]{ CONTENT_ID });
            }
            bool registered = content!=null;
            sb.AppendLine("RESULT(A) self-registered id='"+CONTENT_ID+"' -> "+(registered?content.GetType().Name:"NOT FOUND"));
            bool okOn=false, okOff=false, okDouble=false, enOn=false, enOff=false, metaOk=false;
            if(registered){
                var setEnabled = content.GetType().GetMethod("SetEnabled", new[]{typeof(bool)});
                try{ setEnabled.Invoke(content, new object[]{true});  enOn =(bool)Prop(content,"IsEnabled"); okOn=true; }  catch(Exception e){ sb.AppendLine("SetEnabled(true) THREW: "+Inner(e)); }
                try{ setEnabled.Invoke(content, new object[]{false}); enOff=(bool)Prop(content,"IsEnabled"); okOff=true; } catch(Exception e){ sb.AppendLine("SetEnabled(false) THREW: "+Inner(e)); }
                try{ setEnabled.Invoke(content, new object[]{true}); setEnabled.Invoke(content, new object[]{true}); okDouble=true; } catch(Exception e){ sb.AppendLine("SetEnabled(true x2) THREW: "+Inner(e)); }
                try{ setEnabled.Invoke(content, new object[]{false}); }catch{}
                var meta = Prop(content, "Meta");
                var display = Field(meta,"DisplayName") as string; var cat = Field(meta,"Category") as string;
                metaOk = !string.IsNullOrEmpty(display) && !string.IsNullOrEmpty(cat);
                sb.AppendLine("RESULT(B) SetEnabled noThrow on="+okOn+" off="+okOff+" idempotent(double-on)="+okDouble+" | IsEnabled true->"+enOn+" false->"+enOff);
                sb.AppendLine("RESULT(C) Meta DisplayName='"+display+"' Category='"+cat+"' valid="+metaOk);
            }
            contentOk = registered && okOn && okOff && okDouble && enOn && !enOff && metaOk;
        }

        bool pass = systemsOk && contentOk;
        sb.AppendLine("=== §5/§6.5 ADD-COMPONENT VERDICT ("+KIND+"): "+(pass?"PASS":"FAIL")+" ===");
        Directory.CreateDirectory(TmpDir);
        File.WriteAllText(OutF, sb.ToString());
        Debug.Log("[PS_AddComp]\n"+sb);
    }

    // ---- 5) Teardown ----
    public static void Teardown(){
        var starter = FindStarter();
        if(starter==null || !File.Exists(OrigF)){ Debug.Log("[PS_AddComp] Teardown: nothing to restore"); return; }
        var map = File.ReadAllLines(OrigF).Select(l=>l.Split(new[]{'='},2)).Where(a=>a.Length==2).ToDictionary(a=>a[0], a=>a[1]);
        var so = new SerializedObject(starter);
        if(map.ContainsKey("startAsServer")) so.FindProperty("startAsServer").boolValue = map["startAsServer"]=="True";
        if(map.ContainsKey("hostMode"))      so.FindProperty("hostMode").boolValue      = map["hostMode"]=="True";
        if(map.ContainsKey("roomSceneKey"))  so.FindProperty("roomSceneKey").stringValue = map["roomSceneKey"];
        so.ApplyModifiedPropertiesWithoutUndo();
        // 복원 ↔ 스냅샷 수명이 짝을 이뤄야 한다: 지우지 않으면 다음 Setup이 영원히 "덮어쓰기 금지"에 걸린다.
        try { File.Delete(OrigF); } catch { }
        Debug.Log("[PS_AddComp] Teardown: QuickTestStarter restored to shipped values");
    }
}
