// build_client.cs — parameterized CLIENT build for XRCollabDemo.
// Set the 4 consts, then run via ai-game-developer script-execute (full-code mode).
// IDEMPOTENT: if required scripting defines are missing it sets+saves them and RETURNS
// ("recompile then re-run"); on the second pass (defines present) it configures + builds.
// Mechanics/rationale: c:\J_0\docs\build-meta-client.md. Verified for PLATFORM="Meta" (Quest 3).
using UnityEngine; using UnityEditor; using UnityEditor.Build; using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement; using UnityEngine.SceneManagement; using System.IO; using System.Linq;

public class BuildClient {
  // ---- EDIT THESE ----
  const string PLATFORM   = "Meta";                              // Meta | XReal | Tablet | Vision
  const string MASTER_IP  = "192.168.50.49";                     // master PC LAN IP (NOT 127.0.0.1 for a device)
  const string ROOM_SCENE = "Assets/App/Scenes/BasicRoom_3.unity"; // room online scene (must be in build)
  const string APP_ID     = "com.kisti.xrcollabdemo";            // distinct id → installs beside default urpblank
  // --------------------

  public static void Main() {
    // 1) platform -> preset/target
    string preset, presetDir; BuildTarget target; BuildTargetGroup group; bool isXR;
    switch (PLATFORM) {
      case "Meta":   preset="Meta-v1.3.4";  presetDir="Android";  target=BuildTarget.Android;  group=BuildTargetGroup.Android;  isXR=true;  break;
      case "XReal":  preset="XREAL-v1.3.4"; presetDir="Android";  target=BuildTarget.Android;  group=BuildTargetGroup.Android;  isXR=true;  break;
      case "Tablet": preset="Tablet-v1.3.4";presetDir="Android";  target=BuildTarget.Android;  group=BuildTargetGroup.Android;  isXR=false; break;
      case "Vision": preset="Apple-v1.3.4"; presetDir="VisionOS"; target=BuildTarget.VisionOS; group=BuildTargetGroup.VisionOS; isXR=true;  break;
      default: Debug.LogError("[BuildClient] unknown PLATFORM="+PLATFORM); return;
    }
    string projRoot = Path.GetDirectoryName(Application.dataPath);

    // 2) gates
    if (target==BuildTarget.VisionOS && Application.platform!=RuntimePlatform.OSXEditor) {
      Debug.LogError("[BuildClient] VisionOS build requires macOS + Xcode + PolySpatial. Aborting on this OS."); return; }
    string presetPath = Path.Combine(projRoot, "XumBuildKit/CustomProjectSettings", presetDir, preset);
    if (!Directory.Exists(presetPath)) {
      Debug.LogError("[BuildClient] preset not found: "+presetPath+" (import via Xum Build Kit → Device Presets)"); return; }

    // 3) target + preset
    if (EditorUserBuildSettings.activeBuildTarget != target)
      EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
    XumBuildKit.Editor.Utility.XRSettingsUtility.LoadPreset(preset, presetDir);
    Debug.Log("[BuildClient] "+PLATFORM+" preset applied. active="+EditorUserBuildSettings.activeBuildTarget);

    // 4) persist required defines FIRST (avoids extraScriptingDefines domain-reload; doc §2.4-A/B)
    var nbt = NamedBuildTarget.FromBuildTargetGroup(group);
    string defs = PlayerSettings.GetScriptingDefineSymbols(nbt);
    bool changed=false;
    foreach (var d in new[]{"UNIXR_USE_FISHNET","EDGEGAP_PLUGIN_SERVERS"})
      if (!defs.Contains(d)) { defs += ";"+d; changed=true; }
    if (changed) {
      PlayerSettings.SetScriptingDefineSymbols(nbt, defs); AssetDatabase.SaveAssets();
      Debug.LogWarning("[BuildClient] defines set + saved. WAIT for recompile (isCompiling=false) then RE-RUN this script to build. defs="+defs);
      return;
    }

    // 5) configure
    PlayerSettings.SetApplicationIdentifier(nbt, APP_ID);
    if (target==BuildTarget.Android) PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
    string sa = Path.Combine(Application.dataPath,"StreamingAssets"); Directory.CreateDirectory(sa);
    File.WriteAllText(Path.Combine(sa,"application.cfg"),
      "-mstStartClientConnection=True\n-mstMasterIp="+MASTER_IP+"\n-mstMasterPort=5000");
    AssetDatabase.Refresh();

    // 5a) VR: disable the room scene's flat Main Camera (fights XR rig → flicker; doc §2.4-D)
    if (isXR) {
      var rs = EditorSceneManager.OpenScene(ROOM_SCENE, OpenSceneMode.Additive);
      int off=0;
      foreach (var root in rs.GetRootGameObjects())
        foreach (var cam in root.GetComponentsInChildren<Camera>(true)) {
          cam.enabled=false; var al=cam.GetComponent<AudioListener>(); if(al!=null) al.enabled=false;
          if (cam.gameObject.CompareTag("MainCamera")) cam.gameObject.tag="Untagged";
          EditorUtility.SetDirty(cam); off++;
        }
      if (off>0) { EditorSceneManager.MarkSceneDirty(rs); EditorSceneManager.SaveScene(rs); }
      EditorSceneManager.CloseScene(rs, true);
      Debug.Log("[BuildClient] disabled "+off+" room camera(s) for VR");
    }

    // 5b) inject master IP into Client.unity ClientToMasterConnector (backup→restore so repo stays clean)
    string clientScene="Assets/App/Scenes/Client.unity";
    string absClient=Path.Combine(projRoot, clientScene.Replace('/', Path.DirectorySeparatorChar));
    string bak=absClient+".metabak"; File.Copy(absClient, bak, true);
    int injected=0;
    var cs = EditorSceneManager.OpenScene(clientScene, OpenSceneMode.Single);
    foreach (var root in cs.GetRootGameObjects())
      foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true)) {
        if (mb==null || mb.GetType().Name!="ClientToMasterConnector") continue;
        var so=new SerializedObject(mb); var pIp=so.FindProperty("serverIp"); var pPort=so.FindProperty("serverPort");
        if (pIp!=null && pPort!=null) { pIp.stringValue=MASTER_IP; pPort.intValue=5000; so.ApplyModifiedProperties(); EditorUtility.SetDirty(mb); injected++; }
      }
    if (cs.isDirty) EditorSceneManager.SaveScene(cs);

    // 6) build (no extraScriptingDefines!)
    string outDir=Path.Combine("Builds","App","Client",preset); Directory.CreateDirectory(outDir);
    string outPath = target==BuildTarget.Android ? Path.Combine(outDir,"XRCollabDemo.apk") : outDir;
    var opts=new BuildPlayerOptions{
      scenes=new[]{ clientScene, ROOM_SCENE }, locationPathName=outPath,
      target=target, targetGroup=group, options=BuildOptions.None };
    Debug.Log("[BuildClient] BUILD START "+PLATFORM+" injectedIP="+injected+" -> "+outPath);
    BuildReport r=null;
    try { r=BuildPipeline.BuildPlayer(opts); }
    finally { File.Copy(bak, absClient, true); File.Delete(bak); AssetDatabase.Refresh(); }
    Debug.Log("[BuildClient] DONE result="+r.summary.result+" errors="+r.summary.totalErrors+" out="+r.summary.outputPath);
  }
}
