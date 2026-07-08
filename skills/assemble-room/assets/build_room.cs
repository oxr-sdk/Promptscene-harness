// Phase 2: rebuild Room.exe hosting <ROOM> via XumLobbyServerBuilderWindow reflection (build doc §2-B).
// Run via MCP script-execute (className=BR_BuildRoom, methodName=Run). Set ROOM below.
// NOTE: BuildPipeline blocks the main thread; the MCP call may return "Response data is null" — that's normal.
//       Confirm success via Room/application.cfg + Room_Data/level0 fresh LastWriteTime.
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;

public class BR_BuildRoom {
    const string ROOM = "BasicRoom_N"; // <-- set to the new room name
    const bool ALSO_BUILD_MASTER = false; // set true only if machine LAN IP changed since last master build

    public static void Run(){
        var winType = System.Type.GetType("XumBuildKit.Samples.Editor.App.XumLobbyServerBuilderWindow, XumLobby.Editor");
        Debug.Log("[BR_BuildRoom] winType="+(winType!=null));
        var win = ScriptableObject.CreateInstance(winType);                 // OnEnable loads _settings
        var settings = winType.GetField("_settings", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(win);
        var st = settings.GetType();
        st.GetField("MasterScene").SetValue(settings, new List<EditorBuildSettingsScene>{
            new EditorBuildSettingsScene("Assets/App/Scenes/Master.unity", true) });
        st.GetField("SceneList").SetValue(settings, new List<EditorBuildSettingsScene>{
            new EditorBuildSettingsScene("Assets/App/Scenes/"+ROOM+".unity", true) });  // BaseBuildOptions.SceneList = room content
        st.GetField("SelectedPlatform").SetValue(settings, 0); // ServerOS.Win
        st.GetField("IsHeadless").SetValue(settings, true);
        Debug.Log("[BR_BuildRoom] SceneList="+ROOM+" -> building...");
        if(ALSO_BUILD_MASTER)
            winType.GetMethod("BuildMasterForWindows", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win, null);
        winType.GetMethod("BuildRoom", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win, null);
        Debug.Log("[BR_BuildRoom] build invoked.");
    }
}
