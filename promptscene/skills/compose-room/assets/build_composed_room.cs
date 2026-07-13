// compose-room EXECUTE: build ONE RoomCore-bearing room and place N features under ===== FEATURES =====.
// Run via MCP script-execute (className=CR_BuildComposedRoom, methodName=Run). Set ROOM + FEATURE_TYPES below.
//
// This is scaffold-content's build_feature_room.cs (SC_BuildFeatureRoom) generalized from ONE feature to N.
// The scene-assembly invariants it applies are NOT re-documented here — they are owned by the referenced
// procedures (SSOT): assemble-room SKILL.md Phase 1 (4 R-prefabs, NO R-PlayerSpawner, C1/C3, std hierarchy §1)
// and scaffold-content Phase B (RoomCore under SYSTEMS so features can self-register). If either procedure
// changes, regenerate this file from build_feature_room.cs — do not let it drift.
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class CR_BuildComposedRoom {
    const string ROOM = "ComposedRoom_1";                       // <-- composed room name (no extension), from the plan
    static readonly string[] FEATURE_TYPES = new[]{ "RulerContent" }; // <-- plan's feature class names, in order

    const string ROOM_PREFABS = "Packages/com.kisti.xumlobby/Runtime/Prefabs/2) Room Scene/";
    const string SPAWNER      = "Assets/PromptScene/Prefabs/Room-PlayerSpawner.prefab";
    const string DEFAULT_PREFABS = "Assets/DefaultPrefabObjects.asset";
    const string CLIENT_SCENE = "Assets/App/Scenes/Client.unity";

    // ---- reflection helpers (private FishNet fields live on base classes) ----
    static FieldInfo FindField(Type t, string name){
        while(t!=null){ var f=t.GetField(name, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public); if(f!=null) return f; t=t.BaseType; }
        return null;
    }
    static bool SetField(object o, string name, object val){
        if(o==null) return false; var f=FindField(o.GetType(), name); if(f==null) return false; f.SetValue(o,val); return true;
    }
    static Component Comp(GameObject g, string typeName)
        => g.GetComponents<Component>().FirstOrDefault(c=>c!=null && c.GetType().Name==typeName);
    static Type ResolveType(string name)  // by full or simple name across loaded assemblies
        => AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{ try{return a.GetTypes();}catch{return new Type[0];} })
             .FirstOrDefault(t=>t.FullName==name || t.Name==name);

    public static void Run(){
        var roomPath = "Assets/App/Scenes/"+ROOM+".unity";

        // 1) fresh scene (DefaultGameObjects gives Main Camera + Directional Light)
        var scn = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject Inst(string path){
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(pf==null){ Debug.LogError("[CR] prefab NOT found: "+path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, scn);
            go.name = System.IO.Path.GetFileNameWithoutExtension(path);
            return go;
        }

        // 2) SYSTEMS — 4 R- prefabs (NO R-PlayerSpawner) + the player spawner prefab
        var server = Inst(ROOM_PREFABS+"R-RoomServer.prefab");
        Inst(ROOM_PREFABS+"R-RoomClient.prefab");
        Inst(ROOM_PREFABS+"R-ConnectionToMaster.prefab");
        Inst(ROOM_PREFABS+"R-MasterCanvas.prefab");
        Inst(SPAWNER);

        // 3) C1 — NetworkManager._spawnablePrefabs = DefaultPrefabObjects
        var nm = Comp(server, "NetworkManager");
        var dpo = AssetDatabase.LoadAssetAtPath<ScriptableObject>(DEFAULT_PREFABS);
        bool c1 = SetField(nm, "_spawnablePrefabs", dpo);

        // 4) C3 — DefaultScene online/offline
        var ds = Comp(server, "DefaultScene");
        bool c3a = SetField(ds, "_onlineScene",  roomPath);
        bool c3b = SetField(ds, "_offlineScene", CLIENT_SCENE);

        // 5) RoomCore (SYSTEMS) — every feature self-registers to RoomCore.Instance
        var rcType = ResolveType("PromptScene.Core.RoomCore");
        var roomCoreGo = new GameObject("RoomCore");
        SceneManager.MoveGameObjectToScene(roomCoreGo, scn);
        if(rcType!=null) roomCoreGo.AddComponent(rcType); else Debug.LogError("[CR] RoomCore type not found");

        // 6) N FEATURES — one GameObject per feature class, each carrying its generated component
        var featGos = new List<GameObject>();
        var featReport = new List<string>();
        foreach(var typeName in FEATURE_TYPES){
            var featType = ResolveType(typeName);
            var featGo = new GameObject(typeName.Replace("Content",""));
            SceneManager.MoveGameObjectToScene(featGo, scn);
            if(featType!=null){ featGo.AddComponent(featType); featReport.Add(typeName+"=True"); }
            else { Debug.LogError("[CR] feature type NOT found: "+typeName+" (did it compile?)"); featReport.Add(typeName+"=MISSING"); }
            featGos.Add(featGo);
        }

        // 7) ENVIRONMENT — Floor (Plane ships a MeshCollider) + 4 walls
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name="Floor";
        SceneManager.MoveGameObjectToScene(floor, scn);
        void Wall(string n, Vector3 p, Vector3 s){ var w=GameObject.CreatePrimitive(PrimitiveType.Cube); w.name=n; w.transform.position=p; w.transform.localScale=s; SceneManager.MoveGameObjectToScene(w, scn); }
        Wall("Wall_N", new Vector3(0,1,5),  new Vector3(10,2,0.2f));
        Wall("Wall_S", new Vector3(0,1,-5), new Vector3(10,2,0.2f));
        Wall("Wall_E", new Vector3(5,1,0),  new Vector3(0.2f,2,10));
        Wall("Wall_W", new Vector3(-5,1,0), new Vector3(0.2f,2,10));

        // 8) standard hierarchy (contract §1)
        var roots = scn.GetRootGameObjects().ToList();
        GameObject Find(string n)=>roots.FirstOrDefault(g=>g.name==n);
        GameObject Header(string n){ var go=new GameObject(n); SceneManager.MoveGameObjectToScene(go,scn); return go; }
        void Re(GameObject c, GameObject p){ if(c!=null&&p!=null) c.transform.SetParent(p.transform, true); }

        var systems=Header("===== SYSTEMS =====");
        var env    =Header("===== ENVIRONMENT =====");
        var ui     =Header("===== UI =====");
        var feats  =Header("===== FEATURES =====");
        Header("===== _DYNAMIC =====");

        GameObject Sub(GameObject parent, string n){ var go=new GameObject(n); go.transform.SetParent(parent.transform,false); return go; }
        var network=Sub(systems,"Network"); var player=Sub(systems,"Player");
        Re(Find("R-RoomServer"),network); Re(Find("R-RoomClient"),network); Re(Find("R-ConnectionToMaster"),network);
        Re(Find("Room-PlayerSpawner"),player);
        Re(roomCoreGo, systems);
        Re(Find("R-MasterCanvas"),ui);
        foreach(var fg in featGos) Re(fg, feats);
        foreach(var n in new[]{"Floor","Wall_N","Wall_S","Wall_E","Wall_W","Directional Light","Main Camera"}) Re(Find(n),env);

        // 9) Assign FishNet scene-object ids DETERMINISTICALLY, then save.
        //    ⚠️ A scene created+saved entirely within one script-execute does NOT reliably trigger FishNet's
        //    automatic SceneId generation (the EditorSceneManager.sceneSaving hook is flaky in that path) — the
        //    spawner's NetworkObject can end up SceneId=0 / IsSceneObject=false, so the dedicated server silently
        //    fails to spawn the player avatar (room joins, lobby dissolves, but no Desktop(Clone)). We reproduce
        //    what Tools/Fish-Networking/Utility/Reserialize NetworkObjects does: NetworkObject.CreateSceneId(force)
        //    + ReserializeEditorSetValues. Both are `internal`, so call by reflection. (Verified fix, 2026-07-13.)
        int sceneIdChanged = AssignFishNetSceneIds(scn);

        EditorSceneManager.MarkSceneDirty(scn);
        EditorSceneManager.SaveScene(scn, roomPath);
        var scenes=EditorBuildSettings.scenes.ToList();
        void Ensure(string p){ if(!scenes.Any(s=>s.path==p)) scenes.Add(new EditorBuildSettingsScene(p,true)); }
        Ensure(CLIENT_SCENE); Ensure(roomPath);
        EditorBuildSettings.scenes=scenes.ToArray();

        Debug.Log("[CR_BuildComposedRoom] room="+ROOM+" C1="+c1+" C3online="+c3a+" C3offline="+c3b
            +" RoomCore="+(rcType!=null)+" features["+string.Join(",",featReport)+"]"
            +" sceneIdsGenerated="+sceneIdChanged
            +" buildSettings=["+string.Join(",",EditorBuildSettings.scenes.Select(s=>System.IO.Path.GetFileNameWithoutExtension(s.path)))+"]");
    }

    // Reflection call into FishNet's internal SceneId generator (same as the Reserialize NetworkObjects tool).
    // Returns the number of scene NetworkObjects whose SceneId was (re)generated.
    static int AssignFishNetSceneIds(UnityEngine.SceneManagement.Scene scn){
        var nobT = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{ try{return a.GetTypes();}catch{return new Type[0];} })
            .FirstOrDefault(t=>t.FullName=="FishNet.Object.NetworkObject");
        if(nobT==null){ Debug.LogError("[CR] FishNet.Object.NetworkObject type not found — SceneIds NOT assigned"); return -1; }
        var createSceneId = nobT.GetMethod("CreateSceneId", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        var reserialize   = nobT.GetMethod("ReserializeEditorSetValues", BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
        if(createSceneId==null || reserialize==null){ Debug.LogError("[CR] FishNet CreateSceneId/Reserialize not found — SceneIds NOT assigned"); return -1; }
        var args = new object[]{ scn, true, 0 };                       // (Scene, bool force, out int changed)
        var nobs = createSceneId.Invoke(null, args) as System.Collections.IEnumerable;
        foreach(var nob in nobs){ reserialize.Invoke(nob, new object[]{true,false}); EditorUtility.SetDirty((UnityEngine.Object)nob); }
        return (int)args[2];
    }
}
