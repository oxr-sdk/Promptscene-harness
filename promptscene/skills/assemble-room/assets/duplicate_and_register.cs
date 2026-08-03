// Phase 1 (studio) — clone a sample room into <ROOM>.unity and register it in the Content Manager (Addressables).
// Procedure SSOT: build-studio-room.md §1 (길 1). Run via MCP script-execute
// (className=PS_DuplicateAndRegister, methodName=Run). Set ROOM (and BASE if not the default sample) below.
//
// - AssetDatabase.CopyAsset is a BYTE copy → the base room's --PLAYER_SPAWNER FishNet SceneId is preserved.
// - Registration is a DIRECT Addressables write (== ContentManagerWindow.RegisterScenes): AddLabel("RoomScene")
//   → CreateOrMoveEntry(guid, group) → entry.address = leaf → entry.SetLabel("RoomScene", true, force:true). This
//   deliberately SKIPS the GUI Apply login-gate (backend scene-name dup check, 401) which a local QuickTest baseline
//   does not need (build-studio-room §1 step 2). Re-confirm with GUI Apply only before a real deploy.
// - ⛔ THE WRITE IS VERIFIED BY READ-BACK, and a mismatch is a HARD STOP. Reason (2026-08-03 incident): AssembleRoom
//   was found registered as address='Assets/App/Scenes/AssembleRoom.unity' with NO label — i.e. Addressables' DEFAULT
//   (asset-path) address, as if only CreateOrMoveEntry had run. Nothing caught it for weeks because this step never
//   re-read what it wrote. The damage is not cosmetic: `QuickTestStarterEditor.ResolveAddress()` returns the REGISTERED
//   ADDRESS, and the human's drag-and-drop writes exactly that into roomSceneKey. A non-leaf address therefore makes
//   the human's own gesture produce a key that FishNet cannot match — `UnitySceneManager.GetSceneByName(key)` fails →
//   "The following global scenes were specified but could not be found: <key>" → SendEmptyBroadcast() → the connection
//   is never added to the room scene (measured: Connection.Scenes == []). See build-studio-room §4.2.
//   The convention is upstream's, not ours: Docs/phase2-scene-authoring.md — "이 파일 이름(leaf)이 그대로 Addressables
//   주소가 되고" (L37) and "파일 이름 그대로가 권장값 … RoomScene 라벨은 자동으로 붙습니다" (L77).
// - Note: the GUI Apply cannot repair an ALREADY-registered entry — ContentManagerWindow only rewrites `address` for
//   an existing entry and does not re-apply the label (the SetLabel call sits in the newly-added branch). Repair =
//   uncheck → Apply (removes) → recheck → Apply, or this script.
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public class PS_DuplicateAndRegister {
    const string ROOM = "AssembleRoom";     // <-- new room leaf name (no extension). This is the Addressables address + QuickTest roomSceneKey.
    const string BASE = "T_RoomA";          // <-- base sample room to clone. Default T_RoomA has NO decorative Capsule in
                                            //     ENVIRONMENT (T_RoomB does — the migration §14.3 occlusion trap). Override for a user base.
    const string SCENES_DIR = "Assets/App/Scenes/";
    const string GROUP = "Default Local Group";
    const string LABEL = "RoomScene";

    public static void Run(){
        string basePath = SCENES_DIR + BASE + ".unity";
        string roomPath = SCENES_DIR + ROOM + ".unity";

        if(!File.Exists(basePath)){
            Debug.LogError("[PS_DupReg] base room not found: "+basePath+" — check BASE"); return;
        }
        // recreate: creating a room named <ROOM> replaces any prior room of that name (idempotent for the skill).
        if(File.Exists(roomPath)){
            Debug.Log("[PS_DupReg] target exists, deleting to recreate: "+roomPath);
            AssetDatabase.DeleteAsset(roomPath);
        }
        bool copied = AssetDatabase.CopyAsset(basePath, roomPath);   // byte copy preserves spawner SceneId
        AssetDatabase.Refresh();
        Debug.Log("[PS_DupReg] copied="+copied+"  "+basePath+" -> "+roomPath);
        if(!copied){ Debug.LogError("[PS_DupReg] CopyAsset failed"); return; }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if(settings == null){ Debug.LogError("[PS_DupReg] Addressables settings missing — open Content Manager once"); return; }
        settings.AddLabel(LABEL);
        var group = settings.FindGroup(GROUP);
        if(group == null){ group = settings.DefaultGroup; Debug.LogWarning("[PS_DupReg] group '"+GROUP+"' not found, using DefaultGroup: "+group.Name); }
        string guid = AssetDatabase.AssetPathToGUID(roomPath);
        if(string.IsNullOrEmpty(guid)){ Debug.LogError("[PS_DupReg] STOP: no GUID for "+roomPath+" (AssetDatabase not refreshed?)"); return; }

        // address collision: another asset already holding this address would be silently overwritten by CreateOrMoveEntry
        foreach(var g in settings.groups){
            if(g == null) continue;
            foreach(var e in g.entries)
                if(e.address == ROOM && e.guid != guid)
                    Debug.LogWarning("[PS_DupReg] address '"+ROOM+"' already held by "+e.AssetPath+" (guid "+e.guid+") — it will be shadowed");
        }

        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = ROOM;                    // LEAF address == scene name — no "Scenes/" prefix, no asset path (build-studio-room §1/§4.2)
        entry.SetLabel(LABEL, true, true);        // force:true — without it SetLabel is a NO-OP when the label is not in settings' label list
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        // ── verify by READ-BACK. Never trust the write (2026-08-03 incident — see header). ──
        AddressableAssetEntry back = null;
        foreach(var g in settings.groups){
            if(g == null) continue;
            foreach(var e in g.entries) if(e.guid == guid){ back = e; break; }
            if(back != null) break;
        }
        if(back == null){ Debug.LogError("[PS_DupReg] STOP: entry not found after write (guid "+guid+")"); return; }

        bool addrOk  = back.address == ROOM;
        bool labelOk = false;
        string labels = "";
        if(back.labels != null) foreach(var l in back.labels){ labels += (labels.Length>0?",":"")+l; if(l == LABEL) labelOk = true; }
        bool nameOk  = ROOM == Path.GetFileNameWithoutExtension(roomPath);   // address must equal the Unity scene NAME

        Debug.Log("[PS_DupReg] registered  address='"+back.address+"'  labels=["+labels+"]  group="+group.Name);
        if(!addrOk || !labelOk || !nameOk){
            Debug.LogError("[PS_DupReg] ⛔ STOP — registration does not match the convention."
                +"  address='"+back.address+"' (expected '"+ROOM+"', ok="+addrOk+")"
                +"  labels=["+labels+"] (expected to contain '"+LABEL+"', ok="+labelOk+")"
                +"  address==sceneName ok="+nameOk
                +"   → the human's drag-and-drop would write this address into roomSceneKey and FishNet's"
                +" GetSceneByName() would fail (build-studio-room §4.2). Fix the registration before continuing.");
            return;
        }
        Debug.Log("[PS_DupReg] ✅ registration verified by read-back (address == scene name, label present)");
    }
}
