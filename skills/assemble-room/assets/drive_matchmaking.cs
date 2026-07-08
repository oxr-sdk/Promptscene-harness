// Phase 4: from the editor client (Play mode), guest-auth -> FindGames -> StartMatch(games[0]).
// Run via MCP script-execute (className=BR_DriveMatchmaking, methodName=Run) AFTER entering Play and waiting ~12s.
// GOTCHA: GameInfoPacket has NO Ip/Port members — log Name/Id/Region/OnlinePlayers/MaxPlayers/Type only.
using UnityEngine;
using System.Linq;
using System.Reflection;
using MasterServerToolkit.MasterServer;

public class BR_DriveMatchmaking {
    public static void Run(){
        Debug.Log("[MM] connected="+Mst.Client.Connection.IsConnected+" signedIn="+Mst.Client.Auth.IsSignedIn);
        if(!Mst.Client.Auth.IsSignedIn)
            Mst.Client.Auth.SignInAsGuest((acc,err)=>{ Debug.Log("[MM] guest signin acc="+(acc?.Id??"null")+" err="+err); FindAndJoin(); });
        else FindAndJoin();
    }
    static void FindAndJoin(){
        Mst.Client.Matchmaker.FindGames(games=>{
            Debug.Log("[MM] FindGames count="+(games?.Count ?? -1));
            if(games!=null) foreach(var g in games)
                Debug.Log("[MM]   game name="+g.Name+" id="+g.Id+" region="+g.Region+" players="+g.OnlinePlayers+"/"+g.MaxPlayers+" type="+g.Type);
            if(games!=null && games.Count>0){
                var mm = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .FirstOrDefault(b=>b.GetType().Name=="MatchmakingBehaviour");
                Debug.Log("[MM] MatchmakingBehaviour="+(mm!=null)+" -> StartMatch(game[0])");
                mm?.GetType().GetMethod("StartMatch", BindingFlags.Public|BindingFlags.Instance)
                    .Invoke(mm, new object[]{ games[0] });
            }
        });
        Debug.Log("[MM] FindGames requested (async)");
    }
}
