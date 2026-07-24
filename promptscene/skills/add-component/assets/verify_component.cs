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
    const string CONTENT_ID = "ruler";         // FEATURE: registry Id.  COMPOSITION: ignored (use TYPE_NAME)
    const string TYPE_NAME = "RulerContent";   // C# type simple name of the placed component (both kinds)
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
    public static void Setup(){
        var starter = FindStarter();
        if(starter==null){ Debug.LogError("[PS_AddComp] QuickTestStarter not in scene — is QuickStart open?"); return; }
        var so = new SerializedObject(starter);
        string orig = string.Join("\n", new[]{
            "startAsServer="+so.FindProperty("startAsServer").boolValue,
            "hostMode="+so.FindProperty("hostMode").boolValue,
            "roomSceneKey="+so.FindProperty("roomSceneKey").stringValue,
        });
        Directory.CreateDirectory(TmpDir);
        File.WriteAllText(OrigF, orig);
        so.FindProperty("startAsServer").boolValue = true;
        so.FindProperty("hostMode").boolValue = true;         // single-editor avatar observable
        so.FindProperty("roomSceneKey").stringValue = ROOM;
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
        Debug.Log("[PS_AddComp] Teardown: QuickTestStarter restored to shipped values");
    }
}
