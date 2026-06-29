// Phase 5: editor-side §6.5 snapshot. Run via MCP script-execute (className=BR_VerifyClient, methodName=Run)
// after StartMatch + ~12s. Writes results to a temp file you then Read (avoids console spam). Set ROOM + OUT.
using UnityEngine;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine.SceneManagement;

public class BR_VerifyClient {
    const string ROOM = "BasicRoom_N";            // <-- set to the new room name
    const string OUT  = @"C:\J_0\br_verify.txt";  // temp output you Read, then delete

    public static void Run(){
        var sb=new StringBuilder();
        sb.AppendLine("isPlaying="+Application.isPlaying);
        sb.AppendLine("sceneCount="+SceneManager.sceneCount+" active="+SceneManager.GetActiveScene().name);
        bool clientLoaded=false, roomLoaded=false, movedHolder=false;
        for(int i=0;i<SceneManager.sceneCount;i++){
            var s=SceneManager.GetSceneAt(i);
            sb.AppendLine("  scene["+i+"] '"+s.name+"' roots="+s.rootCount);
            if(s.name=="Client") clientLoaded=true;
            if(s.name==ROOM) roomLoaded=true;
            if(s.name.Contains("MovedObjectsHolder")) movedHolder=true;   // FishNet scene-swap marker
        }
        sb.AppendLine("RESULT(2) C3_ClientUnloaded="+(!clientLoaded)+" RoomLoaded="+roomLoaded+" MovedObjectsHolderScene="+movedHolder);

        var all=Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        var avatar=all.FirstOrDefault(g=>g.name.StartsWith("Desktop"));
        if(avatar!=null){
            var no=avatar.GetComponentsInChildren<Component>(true).FirstOrDefault(c=>c!=null&&c.GetType().Name=="NetworkObject");
            object owner = no?.GetType().GetProperty("IsOwner")?.GetValue(no);
            var cam=avatar.GetComponentInChildren<Camera>(true);
            sb.AppendLine("RESULT(3) Avatar="+avatar.name+" NObj="+(no!=null)+" IsOwner="+owner+" camActive="+(cam!=null&&cam.isActiveAndEnabled));
            var locos = avatar.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(m=>m!=null).Select(m=>m.GetType().Name)
                .Where(n=>n.Contains("Controller")||n.Contains("CameraFollower")||n.Contains("Move")).Distinct();
            var hasNT = avatar.GetComponentsInChildren<Component>(true).Any(c=>c!=null&&c.GetType().Name=="NetworkTransform");
            sb.AppendLine("RESULT(4) locomotion=["+string.Join(",",locos)+"] NetworkTransform="+hasNT);
        } else sb.AppendLine("RESULT(3) NO AVATAR (check room.log for 'Failed to confirm the access' => C2 broken)");

        File.WriteAllText(OUT, sb.ToString());
        Debug.Log("[BR_VerifyClient] written to "+OUT);
    }
}
