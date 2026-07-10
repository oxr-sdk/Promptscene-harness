// scaffold-content Phase B: build a RoomCore-bearing test room and place ONE feature under FEATURES.
// Run via MCP script-execute (className=SC_BuildFeatureRoom, methodName=Run). Set ROOM + FEATURE_TYPE below.
//
// Produces a complete working room (SYSTEMS incl. RoomCore + ENVIRONMENT + the feature under FEATURES)
// in ONE pass, applying the same C1/C3 invariants as build-working-room.md §1–§5, then registers it in
// EditorBuildSettings. After this: rebuild Room.exe (assemble-room build_room.cs), run servers, join, verify.
//
// Why RoomCore here (assemble-room's minimal room omits it): a FEATURE needs RoomCore.Instance to self-register.
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class SC_BuildFeatureRoom {
    const string ROOM         = "FeatureLab_1";       // <-- test room name (no extension)
    const string FEATURE_TYPE = "__FEATURE_CLASS__";  // <-- generated feature class name (e.g. LaserPointerContent)

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
            if(pf==null){ Debug.LogError("[SC] prefab NOT found: "+path); return null; }
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

        // 5) RoomCore (SYSTEMS) — the feature self-registers to RoomCore.Instance
        var rcType = ResolveType("PromptScene.Core.RoomCore");
        var roomCoreGo = new GameObject("RoomCore");
        SceneManager.MoveGameObjectToScene(roomCoreGo, scn);
        if(rcType!=null) roomCoreGo.AddComponent(rcType); else Debug.LogError("[SC] RoomCore type not found");

        // 6) FEATURE under FEATURES — one GameObject carrying the generated component
        var featType = ResolveType(FEATURE_TYPE);
        var featGo = new GameObject(FEATURE_TYPE.Replace("Content",""));
        SceneManager.MoveGameObjectToScene(featGo, scn);
        bool featAdded = false;
        if(featType!=null){ featGo.AddComponent(featType); featAdded=true; } else Debug.LogError("[SC] feature type NOT found: "+FEATURE_TYPE+" (did it compile?)");

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
        Re(featGo, feats);
        foreach(var n in new[]{"Floor","Wall_N","Wall_S","Wall_E","Wall_W","Directional Light","Main Camera"}) Re(Find(n),env);

        // 9) save + register in EditorBuildSettings
        EditorSceneManager.MarkSceneDirty(scn);
        EditorSceneManager.SaveScene(scn, roomPath);
        var scenes=EditorBuildSettings.scenes.ToList();
        void Ensure(string p){ if(!scenes.Any(s=>s.path==p)) scenes.Add(new EditorBuildSettingsScene(p,true)); }
        Ensure(CLIENT_SCENE); Ensure(roomPath);
        EditorBuildSettings.scenes=scenes.ToArray();

        Debug.Log("[SC_BuildFeatureRoom] room="+ROOM+" C1="+c1+" C3online="+c3a+" C3offline="+c3b
            +" RoomCore="+(rcType!=null)+" feature("+FEATURE_TYPE+")Added="+featAdded
            +" buildSettings=["+string.Join(",",EditorBuildSettings.scenes.Select(s=>System.IO.Path.GetFileNameWithoutExtension(s.path)))+"]");
    }
}
