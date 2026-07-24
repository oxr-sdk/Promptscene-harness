// Phase 1 (studio) — clone a sample room into <ROOM>.unity and register it in the Content Manager (Addressables).
// Procedure SSOT: build-studio-room.md §1 (길 1). Run via MCP script-execute
// (className=PS_DuplicateAndRegister, methodName=Run). Set ROOM (and BASE if not the default sample) below.
//
// - AssetDatabase.CopyAsset is a BYTE copy → the base room's --PLAYER_SPAWNER FishNet SceneId is preserved.
// - Registration is a DIRECT Addressables write (== ContentManagerWindow.RegisterScenes): AddLabel("RoomScene")
//   → CreateOrMoveEntry(guid, group) → entry.address = leaf → entry.SetLabel("RoomScene"). This deliberately
//   SKIPS the GUI Apply login-gate (backend scene-name dup check, 401) which a local QuickTest baseline does not
//   need (build-studio-room §1 step 2). Re-confirm with GUI Apply only before a real deploy.
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
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = ROOM;               // LEAF address — no "Scenes/" prefix (build-studio-room §1)
        entry.SetLabel(LABEL, true);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
        AssetDatabase.SaveAssets();
        Debug.Log("[PS_DupReg] registered  address="+entry.address+"  label="+LABEL+"  group="+group.Name);
    }
}
