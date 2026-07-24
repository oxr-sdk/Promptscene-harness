// add-component (studio) — place a FEATURE or COMPOSITION component onto an already-assembled room and
// wire its scene-embed prefab fields (contract §3b). Procedure SSOT: build-studio-room.md §2/§3/§3b/§6.5,
// migration §9–§15 (retrospective A). Run via MCP script-execute (className=PS_AddComponent, methodName=Run)
// AFTER `scene-open <ROOM> Single` (the scene MUST already be open & persistent — that is what lets the FishNet
// sceneSaving hook preserve the --PLAYER_SPAWNER SceneId; NEVER re-parent the spawner, add headers additively).
//
// What it does (mechanical placement + 3b wiring only — implementation/prefab-creation happen before this):
//   1. Ensure the target layer header exists:  FEATURE → "===== FEATURES ====="  /  COMPOSITION → "===== COMPOSITIONS ====="
//   2. Create (idempotent) a child GameObject named GO_NAME under that layer.
//   3. AddComponent TYPE_NAME (reflection — no compile-time dep on App.HotUpdate).
//   4. Scene-embed-wire each WIRE_FIELDS[i] serialized field to the project prefab asset WIRE_PREFABS[i]
//      (contract §3b: the SCENE loader fills a hot MonoBehaviour's SerializedField — a prefab-asset loader would
//      NOT — so FEATURE-root prefab fields like measurementPrefab/channelPrefab/matchPrefab are wired here).
//   5. MarkSceneDirty + SaveScene (Addressables "Use Asset Database" loads from DISK → the room MUST be saved).
//   6. Read back: component present, every field wired, layer child count, --PLAYER_SPAWNER untouched.
//
// NOTE on the 3b boundary (retrospective A / migration §11.3): the component TYPE (FEATURE root / COMPOSITION) and
//   its own PREFAB (base components: NetworkObject / Rigidbody / XR Grab Interactable / XumView / hot View with 0
//   serialized fields) are authored BEFORE this script. XRI/physics components are base-assembly → serialized
//   directly on the prefab (safe). C1 network-prefab registration (FishNet GenerateFull → DefaultPrefabObjects) is
//   a SEPARATE step (see build-studio-room §3c) run only when the component introduces a NEW NetworkObject prefab;
//   this script only wires an ALREADY-registered prefab into the scene component's field.
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class PS_AddComponent {
    // ---- set these before running (add-component fills them from the plan) ----
    const string ROOM      = "AssembleRoom";        // target room leaf (must already be scene-open Single & persistent)
    const string KIND      = "FEATURE";             // "FEATURE" (→ FEATURES layer) | "COMPOSITION" (→ COMPOSITIONS layer)
    const string TYPE_NAME = "RulerContent";        // C# component type simple name (or full name)
    const string GO_NAME   = "Ruler";               // child GameObject name under the layer
    // parallel arrays: scene-embed prefab fields to wire (§3b). Leave empty ({}) for no-prefab content (e.g. ScoreHud).
    static readonly string[] WIRE_FIELDS  = new[]{ "measurementPrefab" };
    static readonly string[] WIRE_PREFABS = new[]{ "Assets/App/Prefabs/RulerMeasurement.prefab" };
    // -------------------------------------------------------------------------

    static Type FindType(string name){
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){
            var t=a.GetType(name); if(t!=null) return t;
            try{ t = a.GetTypes().FirstOrDefault(x=>x.Name==name); }catch{ t=null; }
            if(t!=null) return t;
        }
        return null;
    }

    public static void Run(){
        var sb = new StringBuilder();
        var scn = SceneManager.GetSceneByName(ROOM);
        if(!scn.IsValid() || !scn.isLoaded){ Debug.LogError("[PS_AddComp] scene not open: "+ROOM+" — scene-open it Single first"); return; }

        string layerName = KIND=="COMPOSITION" ? "===== COMPOSITIONS =====" : "===== FEATURES =====";
        GameObject Root(string n) => scn.GetRootGameObjects().FirstOrDefault(g=>g.name==n);
        GameObject layer = Root(layerName);
        if(layer==null){
            // Skeleton should already reserve all 5 layers; create additively if missing (never touch spawner).
            layer = new GameObject(layerName); SceneManager.MoveGameObjectToScene(layer, scn);
            sb.AppendLine("WARN: layer "+layerName+" was missing — created additively (skeleton should reserve it)");
        }

        var type = FindType(TYPE_NAME);
        if(type==null){ Debug.LogError("[PS_AddComp] type not found (did it compile into App.HotUpdate?): "+TYPE_NAME); return; }

        // idempotent child GameObject
        var existing = layer.transform.Find(GO_NAME);
        GameObject host = existing!=null ? existing.gameObject : new GameObject(GO_NAME);
        if(existing==null) host.transform.SetParent(layer.transform, false);

        var comp = host.GetComponent(type);
        if(comp==null) comp = host.AddComponent(type);
        sb.AppendLine("placed "+TYPE_NAME+" under "+layerName+"/"+GO_NAME+" (component="+(comp!=null)+")");

        // §3b scene-embed wiring
        var so = new SerializedObject(comp);
        for(int i=0;i<WIRE_FIELDS.Length;i++){
            var prop = so.FindProperty(WIRE_FIELDS[i]);
            if(prop==null){ sb.AppendLine("  wire "+WIRE_FIELDS[i]+" = <FIELD NOT FOUND on "+TYPE_NAME+">"); continue; }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(WIRE_PREFABS[i]);
            if(asset==null){ sb.AppendLine("  wire "+WIRE_FIELDS[i]+" = <PREFAB NOT FOUND: "+WIRE_PREFABS[i]+">"); continue; }
            prop.objectReferenceValue = asset;
            sb.AppendLine("  wire "+WIRE_FIELDS[i]+" -> "+asset.name);
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scn);
        bool saved = EditorSceneManager.SaveScene(scn);
        sb.AppendLine("saved="+saved);

        // read-back
        var layerNow = Root(layerName);
        sb.AppendLine("layer "+layerName+" child count="+(layerNow!=null?layerNow.transform.childCount:-1));
        var so2 = new SerializedObject(host.GetComponent(type));
        bool allWired = true;
        for(int i=0;i<WIRE_FIELDS.Length;i++){
            var p = so2.FindProperty(WIRE_FIELDS[i]);
            bool w = p!=null && p.objectReferenceValue!=null;
            allWired &= w;
            sb.AppendLine("  read-back "+WIRE_FIELDS[i]+" wired="+w);
        }
        sb.AppendLine("allFieldsWired="+allWired);

        // spawner integrity: must be untouched by this add (never re-parented — retrospective A trap #4)
        var noType = FindType("FishNet.Object.NetworkObject");
        var spawner = scn.GetRootGameObjects()
            .SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject)
            .FirstOrDefault(g=>g.name.Contains("PLAYER_SPAWNER"));
        if(spawner!=null && noType!=null){
            var nob = spawner.GetComponent(noType);
            var sid = noType.GetField("SceneId", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(nob);
            var iso = noType.GetProperty("IsSceneObject", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(nob);
            bool ok = !("0".Equals(sid?.ToString())) && "True".Equals(iso?.ToString());
            sb.AppendLine("spawner "+spawner.name+" SceneId="+sid+" IsSceneObject="+iso+" => SceneId-safe="+ok);
        }

        sb.AppendLine("ADD-COMPONENT: "+(comp!=null && allWired ? "OK" : "CHECK"));
        Debug.Log("[PS_AddComp]\n"+sb);
    }
}
