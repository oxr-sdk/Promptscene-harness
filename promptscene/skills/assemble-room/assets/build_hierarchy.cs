// Phase 1 step 7: organize a flat room into the standard hierarchy
// (promptscene-content-contract.md §1 / build-working-room.md §5).
// Run via MCP script-execute (className=BR_BuildHierarchy, methodName=Run) with <ROOM> active. Set ROOM below.
// Idempotent: re-running won't duplicate headers. worldPositionStays=true keeps env transforms put.
// NOTE: folder parents are left UNTAGGED — never EditorOnly on a parent that holds children (children get build-excluded).
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class BR_BuildHierarchy {
    const string ROOM = "BasicRoom_N"; // <-- set to the room name

    public static void Run(){
        var scn = SceneManager.GetSceneByName(ROOM);
        var roots = scn.GetRootGameObjects().ToList();
        GameObject Find(string n) => roots.FirstOrDefault(g => g.name == n);

        GameObject MakeHeader(string n){
            var existing = Find(n);
            if(existing != null) return existing;
            var go = new GameObject(n);
            SceneManager.MoveGameObjectToScene(go, scn);
            return go;
        }
        void Reparent(GameObject child, GameObject parent){
            if(child != null && parent != null) child.transform.SetParent(parent.transform, true);
        }

        var systems = MakeHeader("===== SYSTEMS =====");
        var env     = MakeHeader("===== ENVIRONMENT =====");
        var ui      = MakeHeader("===== UI =====");
        MakeHeader("===== FEATURES =====");
        MakeHeader("===== _DYNAMIC =====");

        // sub-folders under SYSTEMS
        GameObject Sub(string n){
            var t = systems.transform.Find(n);
            if(t != null) return t.gameObject;
            var go = new GameObject(n); go.transform.SetParent(systems.transform, false); return go;
        }
        var network = Sub("Network");
        var player  = Sub("Player");

        Reparent(Find("R-RoomServer"), network);
        Reparent(Find("R-RoomClient"), network);
        Reparent(Find("R-ConnectionToMaster"), network);
        Reparent(Find("Room-PlayerSpawner"), player);

        Reparent(Find("R-MasterCanvas"), ui);

        foreach(var n in new[]{"Floor","Wall_S","Wall_N","Wall_E","Wall_W","Directional Light","Main Camera"})
            Reparent(Find(n), env);

        EditorSceneManager.MarkSceneDirty(scn);
        Debug.Log("[BR_BuildHierarchy] organized "+ROOM+" into standard hierarchy");
    }
}
