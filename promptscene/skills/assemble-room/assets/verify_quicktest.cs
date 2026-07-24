// Phase 3 (studio) — QuickTest §6.5 auto-judge of the skeleton. Procedure SSOT: build-studio-room.md §4.
// Run via MCP script-execute (className=PS_VerifyQuickTest). Three static entry points, driven in order:
//
//   0) MCP: `scene-open Assets/App/Scenes/QuickStart.unity` Single   (boot scene hosts NetworkManager + QuickTestStarter)
//   1) script-execute PS_VerifyQuickTest.Setup     — snapshots QuickTestStarter fields, sets server+host+roomSceneKey
//   2) script-execute (isPlaying=true) — enter Play. Wait ~12-15s for server start + Addressables room load + spawn.
//   3) script-execute PS_VerifyQuickTest.Check     — writes §6.5 signals to <project>/Temp/ps_qt_result.txt (Read it)
//   4) script-execute (isPlaying=false) — exit Play.
//   5) script-execute PS_VerifyQuickTest.Teardown  — restores QuickTestStarter (never saved to disk; shipped state kept)
//
// QuickStart is edited IN MEMORY ONLY (never scene-save) so the shipped boot scene is untouched.
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

public class PS_VerifyQuickTest {
    const string ROOM = "AssembleRoom";     // leaf == Addressables address == roomSceneKey
    static string TmpDir  => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
    static string OrigF   => Path.Combine(TmpDir, "ps_qt_orig.txt");
    static string OutF    => Path.Combine(TmpDir, "ps_qt_result.txt");

    static Type FindType(string full){
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){ var t=a.GetType(full); if(t!=null) return t; }
        return null;
    }
    static UnityEngine.Object FindStarter(){
        var t = FindType("QuickTestStarter");
        if(t==null) return null;
        var arr = UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None);
        return arr.Length>0 ? (UnityEngine.Object)arr[0] : null;
    }

    // ---- 1) Setup: snapshot + configure host server pointing at ROOM ----
    public static void Setup(){
        var starter = FindStarter();
        if(starter==null){ Debug.LogError("[PS_QT] QuickTestStarter not in scene — is QuickStart open?"); return; }
        var so = new SerializedObject(starter);
        string orig = string.Join("\n", new[]{
            "startAsServer="+so.FindProperty("startAsServer").boolValue,
            "hostMode="+so.FindProperty("hostMode").boolValue,
            "roomSceneKey="+so.FindProperty("roomSceneKey").stringValue,
        });
        Directory.CreateDirectory(TmpDir);
        File.WriteAllText(OrigF, orig);
        so.FindProperty("startAsServer").boolValue = true;    // server
        so.FindProperty("hostMode").boolValue = true;         // + local client → single-editor avatar observable
        so.FindProperty("roomSceneKey").stringValue = ROOM;   // Addressables leaf
        so.ApplyModifiedPropertiesWithoutUndo();              // in-memory only, do NOT scene-save
        Debug.Log("[PS_QT] Setup: startAsServer=true hostMode=true roomSceneKey="+ROOM+" (orig saved)");
    }

    // ---- 3) Check: §6.5 signals ----
    public static void Check(){
        var sb = new StringBuilder();
        // signal 1 — room loaded (Addressables leaf)
        var loaded = new List<string>();
        for(int i=0;i<SceneManager.sceneCount;i++){ var s=SceneManager.GetSceneAt(i); if(s.isLoaded) loaded.Add(s.name); }
        bool roomLoaded = loaded.Contains(ROOM);
        sb.AppendLine("S1 room loaded ("+ROOM+")="+roomLoaded+"  [scenes: "+string.Join(",", loaded)+"]");

        // structure signal — 5 skeleton layers present (FEATURES + COMPOSITIONS empty folders) + no Capsule (default base).
        var roomScn = SceneManager.GetSceneByName(ROOM);
        if(roomScn.IsValid() && roomScn.isLoaded){
            var rNames = roomScn.GetRootGameObjects().Select(g=>g.name).ToList();
            bool comp = rNames.Contains("===== COMPOSITIONS =====");
            bool feat = rNames.Contains("===== FEATURES =====");
            int caps = roomScn.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Count(t=>t.gameObject.name=="Capsule");
            sb.AppendLine("S1b layers: FEATURES="+feat+" COMPOSITIONS="+comp+"  ENVIRONMENT Capsule count="+caps+" (expect 0)");
        }

        // signal 2 — avatar Desktop(Clone) spawned (proves spawner SceneId valid). Studio has no lobby (boot scene).
        GameObject avatar = null;
        for(int i=0;i<SceneManager.sceneCount;i++){
            var s=SceneManager.GetSceneAt(i); if(!s.isLoaded) continue;
            foreach(var r in s.GetRootGameObjects())
                foreach(var tr in r.GetComponentsInChildren<Transform>(true))
                    if(tr.gameObject.name=="Desktop(Clone)"){ avatar=tr.gameObject; break; }
            if(avatar!=null) break;
        }
        bool avatarSpawned = avatar!=null;
        sb.AppendLine("S2 avatar Desktop(Clone) spawned="+avatarSpawned);
        if(avatar!=null){
            var noType = FindType("FishNet.Object.NetworkObject");
            var nob = noType!=null? avatar.GetComponent(noType) : null;
            object isOwner = nob!=null? noType.GetProperty("IsOwner", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(nob) : "<n/a>";
            // WASD-ready / motion rig presence (UXM). Any of these components indicates a live driveable avatar.
            var comps = avatar.GetComponentsInChildren<Component>(true).Where(c=>c!=null).Select(c=>c.GetType().Name).Distinct().ToList();
            bool motionRig = comps.Any(n=>n.Contains("Regressor")||n.Contains("Motion")||n.Contains("Controller")||n.Contains("Avatar"));
            sb.AppendLine("   IsOwner="+isOwner+"  motion/controller rig="+motionRig);
        }

        // signal 3 — RoomCore.Instance initialized, empty registry, 4 built-in services (skeleton has no features).
        var rcType = FindType("PromptScene.Core.RoomCore");
        object inst = rcType!=null? rcType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(null) : null;
        sb.AppendLine("S3 RoomCore.Instance initialized="+(inst!=null));
        if(inst!=null){
            var svcF = rcType.GetField("_services", BindingFlags.Instance|BindingFlags.NonPublic);
            var svc = svcF!=null? svcF.GetValue(inst) as IDictionary : null;
            var svcNames = new List<string>();
            if(svc!=null) foreach(var k in svc.Keys) svcNames.Add((k as Type)?.Name ?? k.ToString());
            svcNames.Sort();
            sb.AppendLine("   services=["+string.Join(",", svcNames)+"] count="+svcNames.Count+" (expect 4: IEventBus,IInteraction,INetSpawn,IRoomUserState)");
            var contents = rcType.GetProperty("Contents", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(inst);
            int regCount = -1;
            if(contents!=null){
                var all = contents.GetType().GetProperty("All")?.GetValue(contents) as IEnumerable;
                if(all!=null){ regCount=0; foreach(var _ in all) regCount++; }
            }
            sb.AppendLine("   registry Contents.All count="+regCount+" (expect 0 — skeleton, no features)");
        }

        bool pass = roomLoaded && avatarSpawned && inst!=null;
        sb.AppendLine("=== §6.5 SKELETON VERDICT: "+(pass?"PASS":"FAIL")+" ===");
        Directory.CreateDirectory(TmpDir);
        File.WriteAllText(OutF, sb.ToString());
        Debug.Log("[PS_QT]\n"+sb);
    }

    // ---- 5) Teardown: restore QuickTestStarter (in-memory; disk was never touched) ----
    public static void Teardown(){
        var starter = FindStarter();
        if(starter==null || !File.Exists(OrigF)){ Debug.Log("[PS_QT] Teardown: nothing to restore"); return; }
        var map = File.ReadAllLines(OrigF).Select(l=>l.Split(new[]{'='},2)).Where(a=>a.Length==2).ToDictionary(a=>a[0], a=>a[1]);
        var so = new SerializedObject(starter);
        if(map.ContainsKey("startAsServer")) so.FindProperty("startAsServer").boolValue = map["startAsServer"]=="True";
        if(map.ContainsKey("hostMode"))      so.FindProperty("hostMode").boolValue      = map["hostMode"]=="True";
        if(map.ContainsKey("roomSceneKey"))  so.FindProperty("roomSceneKey").stringValue = map["roomSceneKey"];
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[PS_QT] Teardown: QuickTestStarter restored to shipped values");
    }
}
