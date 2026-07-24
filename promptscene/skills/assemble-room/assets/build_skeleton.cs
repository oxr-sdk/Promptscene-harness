// Phase 2 (studio) — organize <ROOM> into the 4 SKELETON layers and place the RoomCore + spawner.
// Procedure SSOT: build-studio-room.md §2 (RoomCore) + §3 (5-layer + ⚠ SceneId safe reparent), contract §1.
// Run via MCP script-execute (className=PS_BuildSkeleton, methodName=Run) AFTER `scene-open <ROOM> Single`
// (the scene MUST already be open & persistent — that is what lets FishNet's sceneSaving hook preserve SceneId).
//
// SKELETON ONLY (boundary):
//   ===== SYSTEMS =====   RoomCore (auto-adds SimpleClickProvider + registers 4 services) + Player/--PLAYER_SPAWNER
//   ===== ENVIRONMENT =====  base lights / floor / primitives
//   ===== UI =====        base canvases
//   ===== FEATURES =====  EMPTY — features are add-component's job.
//   NO ===== COMPOSITIONS ===== — that layer is created only when a COMPOSITION is added (add-component). Boundary.
//
// studio deviations from contract §1 (legit): no Network sub-folder (NetworkManager = boot scene), no COMPOSITIONS,
// no _DYNAMIC (runtime-only). See build-studio-room §3 / migration §9.
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class PS_BuildSkeleton {
    const string ROOM = "AssembleTest_1"; // <-- room leaf name (must match Phase 1)

    static Type FindType(string full){
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){ var t=a.GetType(full); if(t!=null) return t; }
        return null;
    }
    static object GetFieldOrProp(object o, Type t, string name){
        var f = t.GetField(name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(f!=null) return f.GetValue(o);
        var p = t.GetProperty(name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(p!=null) return p.GetValue(o);
        return "<no-member:"+name+">";
    }

    public static void Run(){
        var sb = new StringBuilder();
        var scn = SceneManager.GetSceneByName(ROOM);
        if(!scn.IsValid() || !scn.isLoaded){ Debug.LogError("[PS_Skeleton] scene not open: "+ROOM+" — scene-open it Single first"); return; }

        GameObject Root(string n) => scn.GetRootGameObjects().FirstOrDefault(g=>g.name==n);
        GameObject Header(string n){
            var e=Root(n); if(e!=null) return e;
            var go=new GameObject(n); SceneManager.MoveGameObjectToScene(go, scn); return go;
        }
        var systems = Header("===== SYSTEMS =====");
        var env     = Header("===== ENVIRONMENT =====");
        var ui      = Header("===== UI =====");
        Header("===== FEATURES =====");   // empty on purpose (boundary)

        GameObject Sub(GameObject parent, string n){
            var t=parent.transform.Find(n); if(t!=null) return t.gameObject;
            var go=new GameObject(n); go.transform.SetParent(parent.transform, false); return go;
        }
        var player = Sub(systems, "Player");

        // RoomCore under SYSTEMS (idempotent). Added by Type via reflection so this editor script needs no
        // compile-time reference to the App.HotUpdate assembly.
        var roomCoreType = FindType("PromptScene.Core.RoomCore");
        if(roomCoreType == null){ Debug.LogError("[PS_Skeleton] PromptScene.Core.RoomCore not found — is Core imported into ContentLogic?"); return; }
        GameObject roomCore = scn.GetRootGameObjects()
            .SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject)
            .FirstOrDefault(g=>g.GetComponent(roomCoreType)!=null);
        if(roomCore == null){
            roomCore = new GameObject("RoomCore");
            roomCore.transform.SetParent(systems.transform, false);
            roomCore.AddComponent(roomCoreType);
        } else if(roomCore.transform.parent != systems.transform){
            roomCore.transform.SetParent(systems.transform, true);
        }

        // Categorize base roots. Network scene object (--PLAYER_SPAWNER) is handled by the safe reparent below.
        var noType = FindType("FishNet.Object.NetworkObject");
        GameObject spawner = null;
        foreach(var root in scn.GetRootGameObjects().ToList()){
            if(root==systems || root==env || root==ui) continue;
            if(root.name.StartsWith("=====")) continue;
            if(noType!=null && root.GetComponent(noType)!=null){ spawner = root; continue; }  // spawner: below
            if(root.GetComponentInChildren<Canvas>(true)!=null || root.name.Contains("Canvas") || root.name.Contains("Hud")){
                root.transform.SetParent(ui.transform, true); continue;
            }
            root.transform.SetParent(env.transform, true);   // lights / floor / primitives
        }

        // ⚠ FishNet scene network object SAFE reparent (build-studio-room §3, migration §9 — 4 steps):
        //   (1) reparent in the persistent open scene, (2) SaveScene fires sceneSaving hook,
        //   (3) verify SceneId!=0 && IsSceneObject (below), (4) QuickTest (Phase 3). CreateSceneId(force) NOT needed.
        if(spawner != null && spawner.transform.parent != player.transform)
            spawner.transform.SetParent(player.transform, true);

        EditorSceneManager.MarkSceneDirty(scn);
        bool saved = EditorSceneManager.SaveScene(scn);   // step 2 — hook fires here
        sb.AppendLine("saved="+saved);

        var roots = scn.GetRootGameObjects();
        sb.AppendLine("headers: SYSTEMS="+roots.Any(g=>g.name=="===== SYSTEMS =====")
                     +" ENVIRONMENT="+roots.Any(g=>g.name=="===== ENVIRONMENT =====")
                     +" UI="+roots.Any(g=>g.name=="===== UI =====")
                     +" FEATURES="+roots.Any(g=>g.name=="===== FEATURES ====="));
        sb.AppendLine("COMPOSITIONS absent="+(!roots.Any(g=>g.name.Contains("COMPOSITIONS")))+" (expect True — boundary)");
        sb.AppendLine("RoomCore under SYSTEMS="+(roomCore.transform.parent==systems.transform));
        var feat = Root("===== FEATURES =====");
        sb.AppendLine("FEATURES child count="+(feat!=null?feat.transform.childCount:-1)+" (expect 0 — boundary)");

        if(spawner!=null){
            var nob = spawner.GetComponent(noType);
            var sid = GetFieldOrProp(nob, noType, "SceneId");
            var iso = GetFieldOrProp(nob, noType, "IsSceneObject");
            bool ok = !("0".Equals(sid?.ToString())) && "True".Equals(iso?.ToString());
            sb.AppendLine("spawner="+spawner.name+" underPlayer="+(spawner.transform.parent==player.transform)
                         +" SceneId="+sid+" IsSceneObject="+iso+"  => SceneId-safe="+ok);
        } else sb.AppendLine("spawner=NOT FOUND (expected a NetworkObject root like --PLAYER_SPAWNER)");

        Debug.Log("[PS_Skeleton]\n"+sb);
    }
}
