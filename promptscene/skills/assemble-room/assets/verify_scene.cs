// Phase 1 read-back + §3 EditorBuildSettings registration.
// Run via MCP script-execute (className=BR_VerifyScene, methodName=Run). Set ROOM below.
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
using System.Text;
using UnityEngine.SceneManagement;

public class BR_VerifyScene {
    const string ROOM = "BasicRoom_N"; // <-- set to the new room name

    static object GetField(object o, string name){
        if(o==null) return null;
        var t=o.GetType();
        while(t!=null){
            var f=t.GetField(name, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            if(f!=null) return f.GetValue(o);
            t=t.BaseType;
        }
        return "<no-field:"+name+">";
    }
    public static void Run(){
        var sb=new StringBuilder();
        var scn=SceneManager.GetSceneByName(ROOM);
        sb.AppendLine("Scene loaded: "+scn.IsValid()+" path="+scn.path);
        GameObject FindRoot(string n)=>scn.GetRootGameObjects().FirstOrDefault(g=>g.name==n);

        var server=FindRoot("R-RoomServer");
        var nm=server.GetComponents<Component>().FirstOrDefault(c=>c.GetType().Name=="NetworkManager");
        var spawnable=GetField(nm,"_spawnablePrefabs");
        sb.AppendLine("C1 _spawnablePrefabs = "+(spawnable==null?"NULL":spawnable.GetType().Name)+" (expect DefaultPrefabObjects)");

        var ds=server.GetComponents<Component>().FirstOrDefault(c=>c.GetType().Name=="DefaultScene");
        sb.AppendLine("C3 _onlineScene  = "+GetField(ds,"_onlineScene"));
        sb.AppendLine("C3 _offlineScene = "+GetField(ds,"_offlineScene"));

        var client=FindRoot("R-RoomClient");
        var rcm=client.GetComponents<Component>().FirstOrDefault(c=>c.GetType().Name=="RoomClientManager");
        sb.AppendLine("C3 offlineRoomScene = "+GetField(rcm,"offlineRoomScene")+" (expect Client)");

        var spawner=FindRoot("Room-PlayerSpawner");
        sb.AppendLine("C2 spawner comps=["+string.Join(",", spawner.GetComponents<Component>().Select(c=>c.GetType().Name))+"]");
        sb.AppendLine("Forbidden R-PlayerSpawner present = "+(FindRoot("R-PlayerSpawner")!=null)+" (expect False)");

        var floor=FindRoot("Floor");
        sb.AppendLine("ENV Floor collider = "+(floor?.GetComponent<Collider>()!=null));
        sb.AppendLine("ENV Camera="+(scn.GetRootGameObjects().Any(g=>g.GetComponent<Camera>()))+" Light="+(scn.GetRootGameObjects().Any(g=>g.GetComponent<Light>())));

        // §3 register in EditorBuildSettings
        var scenes=EditorBuildSettings.scenes.ToList();
        void Ensure(string p){ if(!scenes.Any(s=>s.path==p)) scenes.Add(new EditorBuildSettingsScene(p,true)); }
        Ensure("Assets/App/Scenes/Client.unity");
        Ensure("Assets/App/Scenes/"+ROOM+".unity");
        EditorBuildSettings.scenes=scenes.ToArray();
        sb.AppendLine("BuildSettings = "+string.Join(" | ", EditorBuildSettings.scenes.Select(s=>System.IO.Path.GetFileNameWithoutExtension(s.path))));

        Debug.Log("[BR_VerifyScene]\n"+sb);
    }
}
